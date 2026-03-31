using System;
using System.Collections.Generic;
using System.Text;
#if ANDROID
using Android.Media;

namespace MauiApp1.Services.Audio;

public sealed class AndroidAudioPlayer : IAudioPlayer
{
    public async Task PlayFileAsync(string filePath, CancellationToken ct = default)
    {
        using var player = new MediaPlayer();
        player.SetDataSource(filePath);
        player.Prepare();
        player.Start();

        var tcs = new TaskCompletionSource();
        player.Completion += (_, __) => tcs.TrySetResult();

        using var reg = ct.Register(() =>
        {
            try { if (player.IsPlaying) player.Stop(); } catch { }
            tcs.TrySetCanceled();
        });

        await tcs.Task.ConfigureAwait(false);
    }
}
#endif
