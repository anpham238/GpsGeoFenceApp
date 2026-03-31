using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Maui.Storage;
namespace MauiApp1.Services;

public static class GeofenceEventGate
{
    static string Key(string poiId, string eventType) => $"geo_{poiId}_{eventType}_last";

    public static bool ShouldAccept(string poiId, string eventType, int debounceSec, int cooldownSec)
    {
        var now = DateTimeOffset.UtcNow;
        var key = Key(poiId, eventType);

        var lastTicks = Preferences.Get(key, 0L);
        if (lastTicks != 0)
        {
            var last = new DateTimeOffset(lastTicks, TimeSpan.Zero);
            var diff = (now - last).TotalSeconds;
            if (diff < debounceSec) return false;   // Debounce
            if (diff < cooldownSec) return false;   // Cooldown
        }
        Preferences.Set(key, now.Ticks);
        return true;
    }
}