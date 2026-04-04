#if ANDROID
using System;
using System.Threading;
using System.Threading.Tasks;
using Android.Media;

namespace MauiApp1.Services.Audio
{
    public sealed class AndroidAudioPlayer : IAudioPlayer
    {
        private readonly object _gate = new();
        private MediaPlayer? _player;
        private CancellationTokenSource? _cts;

        public async Task PlayFileAsync(string filePath, CancellationToken ct = default)
        {
            // Hủy phát trước đó nếu có
            Stop();

            // Tạo token liên kết để có thể cancel từ Stop() hoặc từ ct bên ngoài
            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var token = _cts.Token;

            var tcs = new TaskCompletionSource();

            try
            {
                var player = new MediaPlayer();

                // (Tuỳ chọn) mô tả loại nội dung là SPEECH để hệ thống xử lý phù hợp
                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Lollipop)
                {
                    var attrs = new AudioAttributes.Builder()
                        .SetContentType(AudioContentType.Speech)
                        .SetUsage(AudioUsageKind.Media)
                        .Build();
                    player.SetAudioAttributes(attrs);
                }

                player.SetDataSource(filePath);
                player.Completion += (_, __) => tcs.TrySetResult();

                // Lưu vào field sau khi tạo xong
                lock (_gate)
                {
                    _player = player;
                }

                player.Prepare();  // có thể dùng PrepareAsync() nếu muốn non-blocking
                player.Start();

                using var reg = token.Register(() =>
                {
                    try
                    {
                        lock (_gate)
                        {
                            if (_player != null && _player.IsPlaying)
                                _player.Stop();
                        }
                    }
                    catch { /* ignore */ }
                    tcs.TrySetCanceled();
                });

                await tcs.Task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // bị cancel từ Stop() hoặc ct ngoài
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AndroidAudioPlayer] {ex.Message}");
            }
            finally
            {
                // Dọn dẹp player/tokens
                Stop();
            }
        }

        public void Stop()
        {
            lock (_gate)
            {
                try
                {
                    _cts?.Cancel();
                }
                catch { /* ignore */ }

                try
                {
                    if (_player != null)
                    {
                        if (_player.IsPlaying)
                            _player.Stop();
                        _player.Reset();
                        _player.Release();
                    }
                }
                catch { /* ignore */ }

                _player = null;

                _cts?.Dispose();
                _cts = null;
            }
        }
    }
}
#endif