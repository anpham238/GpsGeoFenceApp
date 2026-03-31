using System;
using System.Collections.Generic;
using System.Text;
namespace MauiApp1.Services.Audio;
public sealed class NoopAudioPlayer : IAudioPlayer
{
    public Task PlayFileAsync(string filePath, CancellationToken ct = default)
        => Task.CompletedTask;
}
