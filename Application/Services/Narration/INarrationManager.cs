using System.Threading;
using System.Threading.Tasks;

namespace MauiApp1.Services.Narration
{
    public interface INarrationManager
    {
        Task HandleAsync(Announcement ann, string? overrideText = null, CancellationToken ct = default);

        /// <summary>Dừng phát (audio/TTS) hiện tại, nếu có.</summary>
        void Stop();
    }
}