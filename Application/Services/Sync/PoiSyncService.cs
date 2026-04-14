using Microsoft.Maui.Networking;
using Microsoft.Maui.Storage;
using MauiApp1.Data;
using MauiApp1.Models;
using MauiApp1.Services.Api;

namespace MauiApp1.Services.Sync;

public sealed class PoiSyncService
{
    private readonly PoiApiClient _api;
    private readonly PoiDatabase _db;
    private readonly SyncMetadataRepository _meta;

    private const string VersionPrefKey = "poi_sync_version";

    private CancellationTokenSource? _cts;
    private Task? _loop;

    public PoiSyncService(PoiApiClient api, PoiDatabase db, SyncMetadataRepository meta)
    {
        _api = api;
        _db = db;
        _meta = meta;
    }

    public void StartAutoSync(TimeSpan interval)
    {
        if (_loop is not null) return;

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        _loop = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (Connectivity.Current.NetworkAccess != NetworkAccess.None)
                        await SyncOnceAsync(ct);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PoiSync] {ex}");
                }

                try { await Task.Delay(interval, ct); } catch { }
            }
        }, ct);
    }

    public void StopAutoSync()
    {
        try { _cts?.Cancel(); } catch { }
        _cts?.Dispose();
        _cts = null;
        _loop = null;
    }

    public async Task SyncOnceAsync(CancellationToken ct = default)
    {
        // Kiểm tra version trước khi tải toàn bộ POI
        var serverVersion = await _api.GetServerVersionAsync(ct);
        if (serverVersion is null)
        {
            System.Diagnostics.Debug.WriteLine("[PoiSync] Server không phản hồi, bỏ qua sync.");
            return;
        }
        var localVersion = Preferences.Get(VersionPrefKey, "");
        if (serverVersion == localVersion)
        {
            System.Diagnostics.Debug.WriteLine("[PoiSync] Version đã cập nhật, bỏ qua sync.");
            return;
        }

        var remote = await _api.GetAllAsync(lang: null, ct: ct);
        System.Diagnostics.Debug.WriteLine($"[PoiSync] Remote count = {remote.Count}");

        int saved = 0;
        foreach (var r in remote)
        {
            var poi = new Poi
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description ?? "",
                Latitude = r.Latitude,
                Longitude = r.Longitude,
                RadiusMeters = r.RadiusMeters,
                CooldownSeconds = r.CooldownSeconds,
                NarrationText = r.NarrationText,
                ImageUrl = r.ImageUrl,
                MapLink = r.MapLink,
                AudioUrl = r.AudioUrl,
                Language = r.Language ?? "vi-VN",
                IsActive = r.IsActive,
                UpdatedAt = r.UpdatedAt
            };
            await _db.SaveAsync(poi);
            saved++;
        }
        await _meta.SetLastSyncUtcAsync("pois", DateTime.UtcNow);
        Preferences.Set(VersionPrefKey, serverVersion);
        System.Diagnostics.Debug.WriteLine($"[PoiSync] Saved/Upserted = {saved}");
    }
}