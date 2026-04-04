using System.Threading;
using System.Threading.Tasks;

namespace MauiApp1.Services.Audio
{
    public sealed class NoopAudioPlayer : IAudioPlayer
    {
        public Task PlayFileAsync(string filePath, CancellationToken ct = default)
            => Task.CompletedTask;

        public void Stop()
        {
            // No-op
        }
    }
}