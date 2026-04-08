using MauiApp1.Data;
using MauiApp1.Models;
using MauiApp1.Services;
using MauiApp1.Services.Api;
using MauiApp1.Services.Narration;
using MauiApp1.Services.Sync;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Maps;
using Microsoft.Maui.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MauiApp1.Pages;

public partial class MapPage : ContentPage
{
    // ✅ Thêm field để track initialization
    private bool _isInitialized = false;

    private readonly IGeofenceService _geofence;
    private readonly ILocationService _location;
    private readonly PoiDatabase _db;
    private readonly NarrationManager _narration;
    private readonly PoiSyncService _poiSync;
    private readonly PlaybackApiClient _playback;
    private readonly PoiNarrationApiClient _narrationApi;
    private readonly PoiNarrationCache _narrationCache;
    private readonly TranslatorClient _translator; // ✅ THÊM

    private string _currentLang = LanguageService.Current;

    private readonly List<Poi> _pois = new();
    private readonly Dictionary<string, Pin> _pinMap = new();

    private CancellationTokenSource? _cts;
    private Poi? _nearestPoi;
    private Location? _userLocation;

    private static readonly Location _hcmCenter = new(10.776889, 106.700806);

    // BottomSheet
    double _sheetCollapsedOffset = -1;
    readonly double _sheetExpandedOffset = 0;
    double _sheetStartPanY = 0;
    bool _sheetReady = false;
    const double SheetPeekHeight = 140;

    public MapPage(
        IGeofenceService geofence,
        ILocationService location,
        PoiDatabase db,
        NarrationManager narration,
        PoiSyncService poiSync,
        PlaybackApiClient playback,
        PoiNarrationApiClient narrationApi,
        PoiNarrationCache narrationCache,
        TranslatorClient translator) // ✅ THÊM
    {
        InitializeComponent();

        _geofence = geofence ?? throw new ArgumentNullException(nameof(geofence));
        _location = location ?? throw new ArgumentNullException(nameof(location));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _narration = narration ?? throw new ArgumentNullException(nameof(narration));
        _poiSync = poiSync ?? throw new ArgumentNullException(nameof(poiSync));
        _playback = playback ?? throw new ArgumentNullException(nameof(playback));
        _narrationApi = narrationApi ?? throw new ArgumentNullException(nameof(narrationApi));
        _narrationCache = narrationCache ?? throw new ArgumentNullException(nameof(narrationCache));
        _translator = translator ?? throw new ArgumentNullException(nameof(translator)); // ✅ THÊM

        // Toolbar
        ToolbarItems.Add(new ToolbarItem
        {
            Text = "Reset",
            Order = ToolbarItemOrder.Primary,
            Command = new Command(() => MyMap.MoveToRegion(
                MapSpan.FromCenterAndRadius(_hcmCenter, Distance.FromKilometers(3))))
        });

        ToolbarItems.Add(new ToolbarItem
        {
            Text = "QR",
            Order = ToolbarItemOrder.Primary,
            Command = new Command(async () => await Shell.Current.GoToAsync("qrscan"))
        });

        ToolbarItems.Add(new ToolbarItem
        {
            Text = "Sync",
            Order = ToolbarItemOrder.Secondary,
            Command = new Command(async () =>
            {
                await _poiSync.SyncOnceAsync();
                await ReloadPoisAsync();

                if (_pois.Count > 0)
                    await _geofence.RegisterAsync(_pois);
                else
                    System.Diagnostics.Debug.WriteLine("[Geofence] Skip register: no POIs after sync.");
            })
        });

        // Bottom sheet
        BtnOpenInMaps.Clicked += async (_, _) => await OpenMapsAsync(_nearestPoi);
        BottomSheet.SizeChanged += (_, _) => SetupBottomSheetOffsets();
        this.SizeChanged += (_, _) => SetupBottomSheetOffsets();

        // Geofence events
        // ✅ Thay vì lambda, dùng method reference
        _geofence.OnPoiEvent += OnGeofenceEvent;
    }

    // ✅ Extract lambda thành method
    private async void OnGeofenceEvent(Poi poi, string type)
    {
        if (type == "EXIT")
        {
            await MainThread.InvokeOnMainThreadAsync(ClearHighlight);
            return;
        }

        var evType = type == "ENTER" ? PoiEventType.Enter : PoiEventType.Near;
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            HighlightPoi(poi, $"Vào vùng {type}");
            ShowDetail(poi);
        });

        var started = DateTime.UtcNow;
        var lang = LanguageService.Current;
        
        // ✅ HƯỚNG 3: Lấy cả narration từ API + dịch tên địa điểm
        var narrationText = await GetNarrationTextAsync(poi.Id, evType, lang);
        var poiText = await GetTranslatedPoiTextAsync(poi, lang);
        
        // Kết hợp: tên địa điểm (dịch) + narration text
        var fullText = string.IsNullOrWhiteSpace(narrationText)
            ? poiText  // Nếu không có narration, chỉ đọc tên + mô tả
            : $"{poiText}. {narrationText}"; // Nếu có, đọc tên + mô tả + narration

        await _narration.HandleAsync(
            new Announcement(poi, evType, started, PreferredLanguage: lang),
            overrideText: fullText);

        var dur = (int)(DateTime.UtcNow - started).TotalSeconds;
        _ = _playback.LogAsync(poi.Id, type, dur > 0 ? dur : null);
    }

    // ====== Language bar ======
    private void RefreshLangBar()
    {
        var map = new Dictionary<string, Border>
        {
            ["vi-VN"] = BtnVi,
            ["en-US"] = BtnEn,
            ["ja-JP"] = BtnJa,
            ["ko-KR"] = BtnKo,
            ["de-DE"] = BtnDe,
        };

        foreach (var (code, btn) in map)
            btn.Background = new SolidColorBrush(code == _currentLang
                ? Color.FromArgb("#1976D2")
                : Color.FromArgb("#333333"));
    }

    private async void OnLangTapped(object? sender, TappedEventArgs e)
    {
        var code = e.Parameter as string;
        if (string.IsNullOrWhiteSpace(code)) return;

        _currentLang = code;
        LanguageService.Set(code);            // ✅ đổi ngôn ngữ thật
        RefreshLangBar();

        try { _narration.Stop(); } catch { }

        await Task.CompletedTask;
    }

    // ====== Narration fetch/cache (HƯỚNG 2) ======
    private static byte ToEventByte(PoiEventType t) => t switch
    {
        PoiEventType.Enter => 0,
        PoiEventType.Near => 1,
        PoiEventType.Tap => 2,
        _ => 0
    };

    private static string ToEventName(PoiEventType t) => t switch
    {
        PoiEventType.Enter => "Enter",
        PoiEventType.Near => "Near",
        PoiEventType.Tap => "Tap",
        _ => "Enter"
    };

    private async Task<string?> GetNarrationTextAsync(string poiId, PoiEventType evType, string lang, CancellationToken ct = default)
    {
        try
        {
            var evByte = ToEventByte(evType);

            // 1) cache
            var cached = await _narrationCache.GetAsync(poiId, evByte, lang);
            if (!string.IsNullOrWhiteSpace(cached))
                return cached;

            // 2) online fetch
            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                var dto = await _narrationApi.GetNarrationAsync(poiId, lang, ToEventName(evType), ct);
                var text = dto?.NarrationText;

                if (!string.IsNullOrWhiteSpace(text))
                {
                    await _narrationCache.UpsertAsync(poiId, dto!.EventType, dto.Language, text);
                    return text;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NarrationFetch] {ex.Message}");
        }

        return null; // fallback -> NarrationManager dùng Poi.NarrationText
    }
    // ====== Lifecycle ======
    protected override void OnAppearing()
    {
        base.OnAppearing();

        _currentLang = LanguageService.Current;
        RefreshLangBar();

        MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(_hcmCenter, Distance.FromKilometers(3)));

        // ✅ Chỉ chạy initialization một lần
        if (!_isInitialized)
        {
            _isInitialized = true;
            // ✅ Chạy background, không block lifecycle
            _ = InitializeMapAsync();
        }
    }

    // ✅ Extract tất cả async operations vào method riêng
    private async Task InitializeMapAsync()
    {
        try
        {
            // Init DB + Sync
            await _db.InitAsync();
            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
                await _poiSync.SyncOnceAsync();

            // Load POIs
            await ReloadPoisAsync();

            // ✅ Request permissions với timeout
            if (!await EnsureLocationPermissionsWithTimeoutAsync())
            {
                System.Diagnostics.Debug.WriteLine("[Map] Location permission denied");
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() => MyMap.IsShowingUser = true);

            // Register geofence
            if (_pois.Count > 0)
            {
                try { await _geofence.RegisterAsync(_pois); }
                catch (Exception ex) 
                { 
                    System.Diagnostics.Debug.WriteLine($"[Geofence] Register error: {ex.Message}"); 
                }
            }

            // Start tracking
            _poiSync.StartAutoSync(TimeSpan.FromMinutes(2));
            _ = Task.Run(MoveToRealLocationAsync);
            StartTracking();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapInit] Fatal error: {ex}");
        }
    }

    // ✅ Thêm timeout cho permission request
    private async Task<bool> EnsureLocationPermissionsWithTimeoutAsync()
    {
        try
        {
            // ✅ Timeout 30 giây - nếu user không response, return false
            var task = Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            var result = await task.WaitAsync(TimeSpan.FromSeconds(30));
            return result == PermissionStatus.Granted;
        }
        catch (TimeoutException)
        {
            System.Diagnostics.Debug.WriteLine("[Permissions] Request timeout");
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Permissions] Error: {ex.Message}");
            return false;
        }
    }

    protected override void OnDisappearing()
    {
        _geofence.OnPoiEvent -= OnGeofenceEvent;
        StopTracking();
        _poiSync.StopAutoSync();
        base.OnDisappearing();
    }
    private async Task<bool> EnsureLocationPermissionsAsync()
    {
        // ✅ Chỉ xin WhenInUse để tránh cảnh báo "only one set of permissions at a time"
        var when = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        return when == PermissionStatus.Granted;
    }
    // ====== Load POIs ======
    private async Task ReloadPoisAsync()
    {
        try
        {
            var pois = await _db.GetActivePoisAsync();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _pois.Clear();
                _pinMap.Clear();
                MyMap.Pins.Clear();

                foreach (var p in pois)
                {
                    _pois.Add(p);

                    var pin = new Pin
                    {
                        Label = p.Name,
                        Address = p.Description,
                        Location = new Location(p.Latitude, p.Longitude),
                        Type = PinType.Place
                    };

                    pin.MarkerClicked += async (_, e) =>
                    {
                        e.HideInfoWindow = false;
                        HighlightPoi(p, "Đã chọn");
                        ShowDetail(p);

                        var started = DateTime.UtcNow;
                        var lang = LanguageService.Current;

                        var narrationText = await GetNarrationTextAsync(p.Id, PoiEventType.Tap, lang);
                        var poiText = await GetTranslatedPoiTextAsync(p, lang);
                        
                        var fullText = string.IsNullOrWhiteSpace(narrationText)
                            ? poiText
                            : $"{poiText}. {narrationText}";

                        await _narration.HandleAsync(
                            new Announcement(p, PoiEventType.Tap, started, PreferredLanguage: lang),
                            overrideText: fullText);

                        var dur = (int)(DateTime.UtcNow - started).TotalSeconds;
                        _ = _playback.LogAsync(p.Id, "TAP", dur > 0 ? dur : null);
                    };

                    _pinMap[p.Id] = pin;
                    MyMap.Pins.Add(pin);
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Map] Reload lỗi: {ex.Message}");
        }
    }

    // ====== GPS ======
    private async Task MoveToRealLocationAsync()
    {
        try
        {
            var req = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(10));
            var loc = await Geolocation.GetLocationAsync(req);
            if (loc == null) return;

            _userLocation = loc;

            await MainThread.InvokeOnMainThreadAsync(() =>
                MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(loc, Distance.FromKilometers(1))));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GPS] {ex.Message}");
        }
    }

    // ====== Tracking loop ======
    private void StartTracking()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _ = TrackLoopAsync(_cts.Token);

        _location.StartTracking((lat, lng) => _userLocation = new Location(lat, lng));
    }

    private void StopTracking()
    {
        _cts?.Cancel();
        _cts = null;
        _location.StopTracking();
    }

    private async Task TrackLoopAsync(CancellationToken token)
    {
        var req = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));

        while (!token.IsCancellationRequested)
        {
            try
            {
                var loc = await Geolocation.GetLocationAsync(req, token);
                if (loc == null) { await Task.Delay(5000, token); continue; }

                _userLocation = loc;

                Poi? nearest = null;
                double nearestDist = double.MaxValue;

                foreach (var poi in _pois.ToList())
                {
                    var dist = Location.CalculateDistance(
                        new Location(poi.Latitude, poi.Longitude),
                        loc, DistanceUnits.Kilometers) * 1000.0;

                    if (dist > poi.NearRadiusMeters) continue;

                    var thisPri = poi.Priority ?? 999;
                    var bestPri = nearest?.Priority ?? 999;

                    if (nearest == null || thisPri < bestPri || (thisPri == bestPri && dist < nearestDist))
                    {
                        nearest = poi;
                        nearestDist = dist;
                    }
                }

                if (nearest != null)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        HighlightPoi(nearest, $"Đến gần (~{nearestDist:F0}m) • Ưu tiên #{nearest.Priority}");
                        ShowDetail(nearest);
                    });

                    if (GeofenceEventGate.ShouldAccept(nearest.Id, "NEAR", nearest.DebounceSeconds, nearest.CooldownSeconds))
                    {
                        var started = DateTime.UtcNow;
                        var lang = LanguageService.Current;
                        
                        var narrationText = await GetNarrationTextAsync(nearest.Id, PoiEventType.Near, lang, token);
                        var poiText = await GetTranslatedPoiTextAsync(nearest, lang, token);
                        
                        var fullText = string.IsNullOrWhiteSpace(narrationText)
                            ? poiText
                            : $"{poiText}. {narrationText}";

                        await _narration.HandleAsync(
                            new Announcement(nearest, PoiEventType.Near, started, PreferredLanguage: lang),
                            overrideText: fullText,
                            ct: token);

                        var dur = (int)(DateTime.UtcNow - started).TotalSeconds;
                        _ = _playback.LogAsync(nearest.Id, "NEAR", dur > 0 ? dur : null);
                    }
                }
                else
                {
                    await MainThread.InvokeOnMainThreadAsync(ClearHighlight);
                }
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                System.Diagnostics.Debug.WriteLine($"[Loop] {ex.Message}");
            }

            try { await Task.Delay(5000, token); } catch { }
        }
    }

    // ====== Highlight ======
    private void HighlightPoi(Poi poi, string status)
    {
        ClearHighlight();
        _nearestPoi = poi;

        if (_pinMap.TryGetValue(poi.Id, out var pin))
            pin.Label = $"★ {poi.Name}";
    }

    private void ClearHighlight()
    {
        if (_nearestPoi == null) return;

        if (_pinMap.TryGetValue(_nearestPoi.Id, out var pin))
            pin.Label = _nearestPoi.Name;

        _nearestPoi = null;
    }

    // ====== Bottom sheet ======
    private void ShowDetail(Poi? poi)
    {
        if (poi == null) return;

        DetailName.Text = poi.Name;
        DetailDesc.Text = string.IsNullOrWhiteSpace(poi.Description) ? "(Không có mô tả)" : poi.Description;
        DetailCoord.Text = $"📍 {poi.Latitude:F6}, {poi.Longitude:F6}";
        DetailRadius.Text = $"🔵 Bán kính: {poi.RadiusMeters}m | Gần: {poi.NearRadiusMeters}m";
        DetailImage.Source = !string.IsNullOrWhiteSpace(poi.ImageUrl) ? poi.ImageUrl : null;

        var link = !string.IsNullOrWhiteSpace(poi.MapLink)
            ? poi.MapLink
            : $"https://maps.google.com/?q={poi.Latitude},{poi.Longitude}";
        LblPoiLink.Text = link;

        _ = ExpandSheetAsync();

        MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(
            new Location(poi.Latitude, poi.Longitude), Distance.FromMeters(400)));
    }

    private async Task OpenMapsAsync(Poi? poi)
    {
        if (poi == null) return;

        var url = !string.IsNullOrWhiteSpace(poi.MapLink)
            ? poi.MapLink
            : $"https://maps.google.com/?q={poi.Latitude},{poi.Longitude}";

        try { await Launcher.OpenAsync(new Uri(url)); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Maps] {ex.Message}"); }
    }

    void SetupBottomSheetOffsets()
    {
        if (BottomSheet.Height <= 0 || this.Height <= 0) return;

        _sheetCollapsedOffset = Math.Max(0, BottomSheet.Height - SheetPeekHeight);

        if (!_sheetReady)
        {
            BottomSheet.TranslationY = _sheetCollapsedOffset;
            _sheetReady = true;
        }
    }

    Task ExpandSheetAsync() =>
        BottomSheet.TranslateToAsync(0, _sheetExpandedOffset, 180, Easing.CubicOut);

    Task CollapseSheetAsync() =>
        BottomSheet.TranslateToAsync(0, _sheetCollapsedOffset, 180, Easing.CubicOut);

    void BottomSheet_TapHeader(object? sender, EventArgs e)
    {
        if (!_sheetReady) return;

        var isCollapsed = Math.Abs(BottomSheet.TranslationY - _sheetCollapsedOffset) < 1;
        _ = BottomSheet.TranslateToAsync(0,
            isCollapsed ? _sheetExpandedOffset : _sheetCollapsedOffset,
            200, Easing.CubicOut);
    }

    void BottomSheet_PanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (!_sheetReady) return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _sheetStartPanY = BottomSheet.TranslationY;
                break;

            case GestureStatus.Running:
                var newY = Math.Clamp(_sheetStartPanY + e.TotalY,
                    _sheetExpandedOffset, _sheetCollapsedOffset);
                BottomSheet.TranslationY = newY;
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                var half = (_sheetCollapsedOffset - _sheetExpandedOffset) / 2.0;
                var curr = BottomSheet.TranslationY - _sheetExpandedOffset;
                _ = BottomSheet.TranslateToAsync(0,
                    curr <= half ? _sheetExpandedOffset : _sheetCollapsedOffset,
                    180, Easing.CubicOut);
                break;
        }
    }

    // ✅ Thêm method mới để dịch tên + mô tả POI
    private async Task<string?> GetTranslatedPoiTextAsync(Poi poi, string lang, CancellationToken ct = default)
    {
        try
        {
            // 1) Nếu là tiếng Việt, không cần dịch
            if (lang == "vi-VN")
            {
                return BuildPoiText(poi.Name, poi.Description);
            }

            // 2) Dịch tên địa điểm
            var translatedName = await TranslateTextAsync(poi.Name, "vi-VN", lang, ct);
            
            // 3) Dịch mô tả
            var translatedDesc = string.IsNullOrWhiteSpace(poi.Description) 
                ? null 
                : await TranslateTextAsync(poi.Description, "vi-VN", lang, ct);

            return BuildPoiText(translatedName, translatedDesc);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TranslatePoi] Error: {ex.Message}");
            return BuildPoiText(poi.Name, poi.Description); // Fallback tiếng Việt
        }
    }

    // ✅ Helper: dùng TranslatorClient để dịch
    private async Task<string?> TranslateTextAsync(string text, string fromLang, string toLang, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        if (fromLang == toLang) return text;

        try
        {
            // ✅ Gọi TranslatorClient thực
            var translated = await _translator.TryTranslateAsync(text, toLang, fromLang, ct);
            return translated ?? text; // Fallback to original text nếu dịch fail
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TranslateTextAsync] Error: {ex.Message}");
            return text; // Fallback to original
        }
    }

    // ✅ Helper: format text (tên + mô tả)
    private static string BuildPoiText(string? name, string? desc)
    {
        var parts = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(name))
            parts.Add(name);
        
        if (!string.IsNullOrWhiteSpace(desc))
            parts.Add(desc);
        
        return string.Join(". ", parts);
    }
}
