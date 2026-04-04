using System;
using System.Collections.Generic;
using System.Text;
namespace MauiApp1.Services.Audio;
public interface IAudioPlayer
{
    Task PlayFileAsync(string filePath, CancellationToken ct = default);
    void Stop();
}
