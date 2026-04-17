using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;

namespace MauiApp1.Services.Guest;

public sealed class GuestDeviceService
{
    private const string DeviceIdKey = "guest_device_id";
    private string? _cachedId;
    public async Task<string> GetOrCreateDeviceIdAsync()
    {
        if (!string.IsNullOrEmpty(_cachedId)) return _cachedId;

        try
        {
            var existing = await SecureStorage.Default.GetAsync(DeviceIdKey);
            if (!string.IsNullOrEmpty(existing))
            {
                _cachedId = existing;
                return existing;
            }
        }
        catch
        {
            var fallback = Preferences.Get(DeviceIdKey, "");
            if (!string.IsNullOrEmpty(fallback))
            {
                _cachedId = fallback;
                return fallback;
            }
        }
        var id = Guid.NewGuid().ToString();
        try { await SecureStorage.Default.SetAsync(DeviceIdKey, id); }
        catch { Preferences.Set(DeviceIdKey, id); }

        _cachedId = id;
        return id;
    }

    public string GetPlatform() => DeviceInfo.Current.Platform.ToString();

    public string GetAppVersion() => AppInfo.Current.VersionString;
}
