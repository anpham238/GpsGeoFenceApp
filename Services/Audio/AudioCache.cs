using System.Security.Cryptography;
using System.Text;
using Microsoft.Maui.Storage;
namespace MauiApp1.Services.Audio;
public sealed class AudioCache
{
    private readonly string _dir;

    public AudioCache()
    {
        _dir = Path.Combine(FileSystem.AppDataDirectory, "audio");
        Directory.CreateDirectory(_dir);
    }

    public static string Sha256Hex(string s)
    {
        using var sha = SHA256.Create();
        var b = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
        var sb = new StringBuilder(b.Length * 2);
        foreach (var x in b) sb.Append(x.ToString("x2"));
        return sb.ToString();
    }

    public string GetLocalPathFromId(string id) => Path.Combine(_dir, id + ".mp3");

    public async Task<string?> GetOrAddFromUrlAsync(string url, CancellationToken ct = default)
    {
        var id = Sha256Hex(url);
        var path = GetLocalPathFromId(id);

        if (File.Exists(path)) return path;

        try
        {
            using var http = new HttpClient();
            using var resp = await http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();
            await using var fs = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
            await resp.Content.CopyToAsync(fs, ct);
            return path;
        }
        catch
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
            return null; // cho NarrationManager fallback sang TTS
        }
    }
    public static void CleanupOldFiles(TimeSpan olderThan)
    {
        var root = Path.Combine(FileSystem.AppDataDirectory, "audio");
        if (!Directory.Exists(root)) return;

        foreach (var f in Directory.GetFiles(root, "*.mp3"))
        {
            try
            {
                if (DateTime.UtcNow - File.GetLastWriteTimeUtc(f) > olderThan)
                    File.Delete(f);
            }
            catch { /* bỏ qua lỗi IO */ }
        }
    }
}