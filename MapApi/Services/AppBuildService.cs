using System.Diagnostics;
using System.Net.Sockets;

namespace MapApi.Services;

public enum AppBuildStatus { Idle, Building, Success, Failed }

public sealed class AppBuildService
{
    private readonly string _mauiProjectPath;
    private readonly string _apkDestPath;
    private readonly ILogger<AppBuildService> _logger;
    private readonly IConfiguration _config;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly List<string> _log = new();

    public AppBuildStatus Status { get; private set; } = AppBuildStatus.Idle;
    public string? LastError { get; private set; }
    public DateTime? LastBuiltAt { get; private set; }

    public bool ApkReady => File.Exists(_apkDestPath);

    public IReadOnlyList<string> GetLogTail(int count = 40)
    {
        lock (_log) return _log.TakeLast(count).ToList().AsReadOnly();
    }

    public AppBuildService(IWebHostEnvironment env, ILogger<AppBuildService> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
        var repoRoot = Path.GetFullPath(Path.Combine(env.ContentRootPath, ".."));
        _mauiProjectPath = Path.Combine(repoRoot, "Application", "MauiApp1.csproj");
        _apkDestPath = Path.Combine(env.WebRootPath, "downloads", "app.apk");
    }

    /// <summary>
    /// Tìm URL server để bake vào APK. Thứ tự ưu tiên:
    ///   1. Build:ServerUrl trong appsettings.json (explicit)
    ///   2. LAN IP của máy đang chạy server (auto-detect)
    ///   3. Build:FallbackUrl hoặc 10.0.2.2:5150 (emulator localhost)
    /// </summary>
    private string ResolveServerUrl()
    {
        // 1. Explicit config: thêm "Build": { "ServerUrl": "http://192.168.x.x:5150/" } vào appsettings.json
        var configUrl = _config["Build:ServerUrl"];
        if (!string.IsNullOrWhiteSpace(configUrl))
            return configUrl.TrimEnd('/') + '/';

        // 2. Auto-detect LAN IP của máy đang chạy backend
        try
        {
            var allUrls = (_config["ASPNETCORE_URLS"] ?? "http://0.0.0.0:5150").Split(';');
            var httpUrl = allUrls.FirstOrDefault(u => u.StartsWith("http://")) ?? "http://0.0.0.0:5150";
            var port = new Uri(httpUrl.Replace("0.0.0.0", "localhost").Replace("+", "localhost")).Port;

            var host = System.Net.Dns.GetHostName();
            var lanIp = System.Net.Dns.GetHostAddresses(host)
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.ToString())
                .FirstOrDefault(a => a.StartsWith("192.168.") || a.StartsWith("10.") || a.StartsWith("172."));

            if (!string.IsNullOrWhiteSpace(lanIp))
                return $"http://{lanIp}:{port}/";
        }
        catch { /* ignore — fall through */ }

        // 3. Fallback: Dev Tunnels hoặc emulator localhost
        return _config["Build:FallbackUrl"] ?? "http://10.0.2.2:5150/";
    }

    public bool TriggerBuild()
    {
        if (Status == AppBuildStatus.Building) return false;
        _ = RunBuildAsync();
        return true;
    }

    private async Task RunBuildAsync()
    {
        if (!await _lock.WaitAsync(0)) return;
        try
        {
            Status = AppBuildStatus.Building;
            lock (_log) { _log.Clear(); AddLog("Build bắt đầu..."); }
            LastError = null;

            if (!File.Exists(_mauiProjectPath))
            {
                Status = AppBuildStatus.Failed;
                LastError = $"Không tìm thấy project: {_mauiProjectPath}";
                AddLog("❌ " + LastError);
                return;
            }

            // ── Bake server URL vào APK ────────────────────────────────────────
            // Ghi URL hiện tại vào Resources/Raw/server_url.txt trước khi build
            // App sẽ đọc file này lúc khởi động để biết server đang ở đâu.
            var serverUrl = ResolveServerUrl();
            var serverUrlFile = Path.Combine(
                Path.GetDirectoryName(_mauiProjectPath)!,
                "Resources", "Raw", "server_url.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(serverUrlFile)!);
            await File.WriteAllTextAsync(serverUrlFile, serverUrl);
            AddLog($"📡 Server URL baked: {serverUrl}");

            var psi = new ProcessStartInfo("dotnet")
            {
                Arguments = $"publish \"{_mauiProjectPath}\" -f net10.0-android -c Release",
                WorkingDirectory = Path.GetDirectoryName(_mauiProjectPath)!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = new Process { StartInfo = psi };
            proc.OutputDataReceived += (_, e) => { if (e.Data != null) AddLog(e.Data); };
            proc.ErrorDataReceived  += (_, e) => { if (e.Data != null) AddLog("[ERR] " + e.Data); };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            await proc.WaitForExitAsync();

            if (proc.ExitCode == 0)
            {
                var outputDir = Path.Combine(Path.GetDirectoryName(_mauiProjectPath)!, "bin", "Release", "net10.0-android");
                var signed = Directory.Exists(outputDir)
                    ? Directory.GetFiles(outputDir, "*-Signed.apk").FirstOrDefault()
                    : null;

                if (signed != null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_apkDestPath)!);
                    File.Copy(signed, _apkDestPath, overwrite: true);
                    Status = AppBuildStatus.Success;
                    LastBuiltAt = DateTime.UtcNow;
                    AddLog("✅ Build xong! APK đã được sao chép vào downloads/app.apk");
                }
                else
                {
                    Status = AppBuildStatus.Failed;
                    LastError = "Build thành công nhưng không tìm thấy Signed APK trong thư mục output.";
                    AddLog("❌ " + LastError);
                }
            }
            else
            {
                Status = AppBuildStatus.Failed;
                LastError = $"dotnet publish thoát với exit code {proc.ExitCode}";
                AddLog("❌ " + LastError);
            }
        }
        catch (Exception ex)
        {
            Status = AppBuildStatus.Failed;
            LastError = ex.Message;
            AddLog("❌ Exception: " + ex.Message);
            _logger.LogError(ex, "App build failed");
        }
        finally
        {
            _lock.Release();
        }
    }

    private void AddLog(string line)
    {
        var entry = $"[{DateTime.Now:HH:mm:ss}] {line}";
        lock (_log)
        {
            _log.Add(entry);
            if (_log.Count > 300) _log.RemoveAt(0);
        }
        _logger.LogInformation("[Build] {line}", line);
    }
}
