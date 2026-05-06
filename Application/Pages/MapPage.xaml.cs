using MauiApp1.Models;
using MauiApp1.Services.Api;
using MauiApp1.Services.Guest;
using MauiApp1.Services.Narration;
using MauiApp1.Services.Sync;
using Microsoft.Maui.Controls.Maps;

namespace MauiApp1.Pages;

public partial class MapPage : ContentPage
{
    private bool _isInitialized = false;
    private readonly IGeofenceService _geofence;
    private readonly ILocationService _location;
    private readonly PoiDatabase _db;
    private readonly PoiNarrationHandler _narrationHandler;
    private readonly PoiSyncService _poiSync;
    private readonly TranslatorClient _translator;
    private readonly PoiApiClient _poiApi;
    private readonly UsageApiClient _usage;
    private readonly ProfileApiClient _profileApi;
    private readonly GuestHeartbeatService _deviceRealtime;
    private CancellationTokenSource? _searchCts;
    private List<PoiSearchResult> _searchResults = new();
    private string _currentLang = LanguageService.Current;
    private readonly List<Poi> _pois = new();
    private readonly Dictionary<int, Pin> _pinMap = new();
    private CancellationTokenSource? _cts;
    private Poi? _nearestPoi;
    private Location? _userLocation;
    private static readonly Location _hcmCenter = new(10.776889, 106.700806);
    double _sheetCollapsedOffset = -1;
    readonly double _sheetExpandedOffset = 0;
    double _sheetStartPanY = 0;
    bool _sheetReady = false;
    const double SheetPeekHeight = 140;
    private Polyline? _routePolyline;

    public MapPage(
        IGeofenceService geofence,
        ILocationService location,
        PoiDatabase db,
        PoiNarrationHandler narrationHandler,
        PoiSyncService poiSync,
        TranslatorClient translator,
        PoiApiClient poiApi,
        ProfileApiClient profileApi,
        UsageApiClient usage,
        GuestHeartbeatService deviceRealtime)
    {
        InitializeComponent();
        _geofence         = geofence         ?? throw new ArgumentNullException(nameof(geofence));
        _location         = location         ?? throw new ArgumentNullException(nameof(location));
        _db               = db               ?? throw new ArgumentNullException(nameof(db));
        _narrationHandler = narrationHandler ?? throw new ArgumentNullException(nameof(narrationHandler));
        _poiSync          = poiSync          ?? throw new ArgumentNullException(nameof(poiSync));
        _translator       = translator       ?? throw new ArgumentNullException(nameof(translator));
        _poiApi           = poiApi           ?? throw new ArgumentNullException(nameof(poiApi));
        _profileApi       = profileApi       ?? throw new ArgumentNullException(nameof(profileApi));
        _usage            = usage            ?? throw new ArgumentNullException(nameof(usage));
        _deviceRealtime   = deviceRealtime   ?? throw new ArgumentNullException(nameof(deviceRealtime));
        BtnOpenInMaps.Clicked += async (_, _) => await OpenMapsAsync(_nearestPoi);
        BtnDirections.Clicked += async (_, _) => await DrawDirectionsAsync(_nearestPoi);
        BtnListen.Clicked     += async (_, _) => await OnListenButtonClickedAsync();
        BottomSheet.SizeChanged += (_, _) => SetupBottomSheetOffsets();
        this.SizeChanged        += (_, _) => SetupBottomSheetOffsets();
        _geofence.OnPoiEvent += OnGeofenceEvent;
    }

    private async void OnTopLoginClicked(object sender, EventArgs e)
    {
        if (AuthApiClient.IsLoggedIn())
        {
            // Đã đăng nhập thì mở trang Profile
            await Shell.Current.GoToAsync("//profile");
        }
        else
        {
            // Chưa đăng nhập thì mở trang Login
            await Shell.Current.GoToAsync("//login");
        }
    }
    private async void OnQrButtonClicked(object? sender, TappedEventArgs e)
    {
        try
        {
            var camStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (camStatus != PermissionStatus.Granted)
                camStatus = await Permissions.RequestAsync<Permissions.Camera>();

            if (camStatus == PermissionStatus.Granted)
                await Shell.Current!.GoToAsync("qrscan");
            else
                await this.DisplayAlertAsync("Từ chối", "Bạn cần cấp quyền Camera để sử dụng tính năng này.", "OK");
        }
        catch (Exception ex)
        {
            await this.DisplayAlertAsync("Lỗi QR", ex.Message, "OK");
        }
    }

    /// <summary>
    /// Cập nhật avatar: hiển thị chữ viết tắt tên user hoặc "?" nếu chưa đăng nhập.
    /// Gọi sau khi đăng nhập/đăng xuất thành công.
    /// </summary>
    private void UpdateAvatarLabel(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            AvatarLabel.Text = "?";
            AvatarBorder.BackgroundColor = Color.FromArgb("#9E9E9E");
        }
        else
        {
            // Lấy chữ cái đầu tiên của tên
            AvatarLabel.Text = displayName.Trim()[0].ToString().ToUpper();
            AvatarBorder.BackgroundColor = Color.FromArgb("#5C6BC0");
        }
    }

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
            if (type == "ENTER")
            {
                MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(new Location(poi.Latitude, poi.Longitude), Distance.FromMeters(300)));
                if (_sheetReady) _ = BottomSheet.TranslateToAsync(0, _sheetExpandedOffset, 250, Easing.CubicOut);
            }
        });

        if (!await EnsureUsageAllowedAsync("POI_LISTEN", poi.Id)) return;
        await _narrationHandler.PlayAsync(poi, evType, type);
    }

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
            btn.Background = new SolidColorBrush(code == _currentLang ? Color.FromArgb("#1976D2") : Color.FromArgb("#333333"));
    }

    private async void OnLangTapped(object? sender, TappedEventArgs e)
    {
        var code = e.Parameter as string;
        if (string.IsNullOrWhiteSpace(code)) return;
        _currentLang = code;
        LanguageService.Set(code);
        RefreshLangBar();
        try { _narrationHandler.Stop(); } catch { }
        await Task.CompletedTask;
    }


    protected override void OnAppearing()
    {
        base.OnAppearing();
        _currentLang = LanguageService.Current;
        RefreshLangBar();

        var currentUser = Preferences.Get("auth_username", Preferences.Get("Username", ""));
        bool isLoggedIn = AuthApiClient.IsLoggedIn();

        if (isLoggedIn)
        {
            var avatarUrl = Preferences.Get("auth_avatar_url", "");
            var hasRealAvatar = !string.IsNullOrWhiteSpace(avatarUrl)
                             && avatarUrl != "default-avatar.png"
                             && !avatarUrl.Equals("default-avatar.png", StringComparison.OrdinalIgnoreCase);

            if (hasRealAvatar)
            {
                var baseUrl = _poiApi.BaseUrl.TrimEnd('/');
                var uri = avatarUrl.StartsWith("http")
                    ? new Uri(avatarUrl)
                    : new Uri(baseUrl + "/" + avatarUrl.TrimStart('/'));

                ImgAvatar.Source = ImageSource.FromUri(uri);
                ImgAvatar.IsVisible = true;
                AvatarLabel.IsVisible = false;
            }
            else
            {
                ImgAvatar.IsVisible = false;
                AvatarLabel.IsVisible = true;
                UpdateAvatarLabel(currentUser);
            }
        }
        else
        {
            // Chưa đăng nhập -> Hiện dấu hỏi chấm
            ImgAvatar.IsVisible = false;
            AvatarLabel.IsVisible = true;
            UpdateAvatarLabel(null);
        }

        MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(_hcmCenter, Distance.FromKilometers(3)));
        if (!_isInitialized)
        {
            _isInitialized = true;
            _ = InitializeMapAsync();
        }

        _ = HandlePendingDeepLinkAsync();
        _ = RefreshUsageBadgeAsync();
    }

    private async Task HandlePendingDeepLinkAsync()
    {
#if ANDROID
        var raw = MauiApp1.DeepLinkHandler.PendingUri;
        if (string.IsNullOrEmpty(raw)) return;
        MauiApp1.DeepLinkHandler.PendingUri = null;

        var parsed = MauiApp1.DeepLinkHandler.Parse(raw);
        if (parsed is null) return;

        var (poiId, lang) = parsed.Value;
        var poi = await _db.GetByIdAsync(poiId);
        if (poi is null) return;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            HighlightPoi(poi, "Deep link");
            ShowDetail(poi);
        });
        await _narrationHandler.PlayAsync(poi, PoiEventType.Tap, "TAP");
#endif
    }

    private async Task InitializeMapAsync()
    {
        try
        {
            await _db.InitAsync();
            try { await _poiSync.SyncOnceAsync(); } catch { }

            await ReloadPoisAsync();
            if (!await EnsureLocationPermissionsAsync()) return;

            await MainThread.InvokeOnMainThreadAsync(() => MyMap.IsShowingUser = true);
            if (_pois.Count > 0)
            {
                try { await _geofence.RegisterAsync(_pois); } catch { }
            }

            _poiSync.StartAutoSync(TimeSpan.FromMinutes(2));
            _ = Task.Run(MoveToRealLocationAsync);
            StartTracking();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapInit] Fatal error: {ex}");
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
        var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (status == PermissionStatus.Granted) return true;

        bool userAgreed = await this.DisplayAlertAsync(
            "📍 Yêu cầu định vị",
            "Smart Tourism cần quyền truy cập vị trí để tự động phát audio.",
            "Tiếp tục", "Để sau");

        if (!userAgreed) return false;

        try { status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>(); }
        catch { }

        return status == PermissionStatus.Granted;
    }

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
                MyMap.MapElements.Clear();

                foreach (var p in pois)
                {
                    _pois.Add(p);
                    var circle = new Microsoft.Maui.Controls.Maps.Circle
                    {
                        Center = new Location(p.Latitude, p.Longitude),
                        Radius = Distance.FromMeters(p.RadiusMeters),
                        StrokeWidth = 0,
                        FillColor = Color.FromRgba(255, 69, 0, 40)
                    };
                    MyMap.MapElements.Add(circle);

                    var pin = new Pin
                    {
                        Label = p.Name,
                        Address = p.Description ?? "",
                        Location = new Location(p.Latitude, p.Longitude),
                        Type = PinType.Place
                    };
                    pin.MarkerClicked += async (_, e) =>
                    {
                        e.HideInfoWindow = true;
                        HighlightPoi(p, "Đã chọn");
                        ShowDetail(p);
                        MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(new Location(p.Latitude, p.Longitude), Distance.FromMeters(300)));
                        if (_sheetReady) _ = BottomSheet.TranslateToAsync(0, _sheetExpandedOffset, 250, Easing.CubicOut);
                        if (!await EnsureUsageAllowedAsync("POI_LISTEN", p.Id)) return;
                        await _narrationHandler.PlayAsync(p, PoiEventType.Tap, "TAP");
                    };
                    _pinMap[p.Id] = pin;
                    MyMap.Pins.Add(pin);
                }
            });
        }
        catch { }
    }

    private async Task MoveToRealLocationAsync()
    {
        try
        {
            var loc = await Geolocation.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(10)));
            if (loc == null) return;
            _userLocation = loc;
            await MainThread.InvokeOnMainThreadAsync(() => MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(loc, Distance.FromKilometers(1))));
        }
        catch { }
    }

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
        int routeTickCount = 0;
        while (!token.IsCancellationRequested)
        {
            try
            {
                var loc = await Geolocation.GetLocationAsync(req, token);
                if (loc == null) { await Task.Delay(5000, token); continue; }

                if (++routeTickCount >= 6)
                {
                    routeTickCount = 0;
                    _narrationHandler.LogRoute(loc.Latitude, loc.Longitude);
                }

                _userLocation = loc;
                _ = _deviceRealtime.ReportLocationAsync(loc.Latitude, loc.Longitude);
                Poi? nearest = null;
                double nearestDist = double.MaxValue;

                foreach (var poi in _pois.ToList())
                {
                    var dist = Location.CalculateDistance(new Location(poi.Latitude, poi.Longitude), loc, DistanceUnits.Kilometers) * 1000.0;
                    if (dist > poi.RadiusMeters * 2) continue;
                    if (nearest == null || dist < nearestDist)
                    {
                        nearest = poi;
                        nearestDist = dist;
                    }
                }
                if (nearest != null)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        HighlightPoi(nearest, $"Đến gần (~{nearestDist:F0}m)");
                        ShowDetail(nearest);
                    });

                    if (GeofenceEventGate.ShouldAccept(nearest.Id, "NEAR", 3, nearest.CooldownSeconds))
                    {
                        if (!await EnsureUsageAllowedAsync("POI_LISTEN", nearest.Id)) continue;
                        await _narrationHandler.PlayAsync(nearest, PoiEventType.Near, "NEAR", token);
                    }
                }
            }
            catch { }
            try { await Task.Delay(5000, token); } catch { }
        }
    }

    private void HighlightPoi(Poi poi, string status)
    {
        ClearHighlight();
        _nearestPoi = poi;
        if (_pinMap.TryGetValue(poi.Id, out var pin)) pin.Label = $"★ {poi.Name}";
    }

    private void ClearHighlight()
    {
        if (_nearestPoi == null) return;
        if (_pinMap.TryGetValue(_nearestPoi.Id, out var pin)) pin.Label = _nearestPoi.Name;
        _nearestPoi = null;
    }

    private async void ShowDetail(Poi? poi)
    {
        if (poi == null) return;

        DetailName.Text = "Đang dịch...";
        DetailDesc.Text = "";
        DetailCoord.Text = $"📍 {poi.Latitude:F6}, {poi.Longitude:F6}";

        // Reset image area
        PoiImage.Source = null;
        PoiImage.IsVisible = true;
        PoiImageCarousel.ItemsSource = null;
        PoiImageCarousel.IsVisible = false;
        PoiImageIndicator.IsVisible = false;

        // Load images async (không block UI)
        _ = LoadPoiImagesAsync(poi);

        var lang = LanguageService.Current;
        var translatedName = lang == "vi-VN" ? poi.Name : await TranslateTextAsync(poi.Name, "vi-VN", lang);
        var translatedDesc = lang == "vi-VN" ? poi.Description : await TranslateTextAsync(poi.Description, "vi-VN", lang);

        DetailName.Text = translatedName ?? poi.Name;
        DetailDesc.Text = string.IsNullOrWhiteSpace(translatedDesc) ? "(Không có mô tả)" : translatedDesc;

        BtnDirections.IsVisible = AuthApiClient.IsPro();
    }

    private async void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        var keyword = e.NewTextValue?.Trim() ?? "";
        _searchCts?.Cancel();

        if (string.IsNullOrWhiteSpace(keyword))
        {
            SearchDropdown.IsVisible = false;
            SearchSuggestions.ItemsSource = null;
            return;
        }

        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        try
        {
            await Task.Delay(300, token);
            if (token.IsCancellationRequested) return;

            var results = await _poiApi.SearchAsync(keyword, token);
            if (token.IsCancellationRequested) return;

            _searchResults = results;
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                SearchSuggestions.ItemsSource = results;
                SearchDropdown.IsVisible = results.Count > 0;
            });
        }
        catch (TaskCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Search] {ex.Message}");
        }
    }

    private void OnSearchCompleted(object? sender, EventArgs e)
    {
        // Trigger immediate search on keyboard confirm
        var keyword = SearchEntry.Text?.Trim() ?? "";
        if (!string.IsNullOrWhiteSpace(keyword))
            _ = SearchImmediateAsync(keyword);
    }

    private async Task SearchImmediateAsync(string keyword)
    {
        try
        {
            var results = await _poiApi.SearchAsync(keyword);
            _searchResults = results;
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                SearchSuggestions.ItemsSource = results;
                SearchDropdown.IsVisible = results.Count > 0;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SearchImmediate] {ex.Message}");
        }
    }

    private async void OnSuggestionSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection?.FirstOrDefault() is not PoiSearchResult selected)
            return;

        // Clear selection so user can tap same item again
        SearchSuggestions.SelectedItem = null;
        SearchDropdown.IsVisible = false;
        SearchEntry.Text = selected.Name;

        // Find matching local POI or use search result coordinates to navigate
        var localPoi = _pois.FirstOrDefault(p => p.Id == selected.Id);

        await MainThread.InvokeOnMainThreadAsync(() =>
            MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(
                new Location(selected.Latitude, selected.Longitude),
                Distance.FromMeters(300))));
        if (localPoi != null)
        {
            HighlightPoi(localPoi, "Tìm kiếm");
            ShowDetail(localPoi);
            if (_sheetReady) _ = BottomSheet.TranslateToAsync(0, _sheetExpandedOffset, 250, Easing.CubicOut);
        }
    }

    private async Task OnListenButtonClickedAsync()
    {
        if (_nearestPoi == null) return;
        if (!await EnsureUsageAllowedAsync("POI_LISTEN", _nearestPoi.Id)) return;
        await _narrationHandler.PlayAsync(_nearestPoi, PoiEventType.Tap, "TAP");
    }

    private async Task<bool> EnsureUsageAllowedAsync(string actionType, int poiId = 0)
    {
        try
        {
            if (AuthApiClient.IsPro()) return true;

            // Logged-in user: check via /api/access/check-poi (respects Area Pack + Free quota)
            if (AuthApiClient.IsLoggedIn() && poiId > 0)
            {
                var deviceId = UsageApiClient.GetEntityId();
                var result = await _usage.CheckPoiAccessAsync(poiId, deviceId);
                if (actionType == "POI_LISTEN") _ = RefreshUsageBadgeAsync();
                if (result.AccessGranted) return true;

                if (result.AccessReason == "WRONG_AREA_PACK")
                {
                    await this.DisplayAlertAsync(
                        "Ngoài khu vực",
                        "POI này không thuộc khu vực bạn đã mua. Vui lòng mua thêm gói Area Pack cho khu vực này.",
                        "Xem gói");
                    await Shell.Current.GoToAsync("proupgrade", new Dictionary<string, object> { ["isPaywall"] = true });
                }
                else if (result.ShowPaywall)
                {
                    await Shell.Current.GoToAsync("proupgrade", new Dictionary<string, object> { ["isPaywall"] = true });
                }
                return false;
            }

            // Guest or no poiId: fall back to daily usage quota check
            var entityId = UsageApiClient.GetEntityId();
            var (allowed, _) = await _usage.CheckAsync(entityId, actionType);
            if (actionType == "POI_LISTEN") _ = RefreshUsageBadgeAsync();
            if (allowed) return true;

            await Shell.Current.GoToAsync("proupgrade", new Dictionary<string, object> { ["isPaywall"] = true });
            return false;
        }
        catch
        {
            return true; // lỗi mạng → không chặn user
        }
    }

    private async Task RefreshUsageBadgeAsync()
    {
        try
        {
            if (AuthApiClient.IsPro())
            {
                MainThread.BeginInvokeOnMainThread(() => UsageBadge.IsVisible = false);
                return;
            }

            var entityId = UsageApiClient.GetEntityId();
            var status = await _usage.GetStatusAsync(entityId, "POI_LISTEN");
            if (status != null)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    UsageBadge.IsVisible = true;
                    if (status.Allowed)
                    {
                        int remaining = status.Limit - status.Used;
                        LblUsage.Text = $"🎧 Thuyết minh: {remaining}/{status.Limit} lượt (Free)";
                        LblUsage.TextColor = Color.FromArgb("#F57F17");
                        UsageBadge.BackgroundColor = Color.FromArgb("#FFF8E1");
                        UsageBadge.Stroke = Color.FromArgb("#FFD54F");
                    }
                    else
                    {
                        LblUsage.Text = $"❌ Hết lượt. Làm mới sau {status.ResetInHours:F1}h";
                        LblUsage.TextColor = Color.FromArgb("#D32F2F");
                        UsageBadge.BackgroundColor = Color.FromArgb("#FFEBEE");
                        UsageBadge.Stroke = Color.FromArgb("#EF5350");
                    }
                });
            }
        }
        catch { }
    }

    private async Task DrawDirectionsAsync(Poi? poi)
    {
        if (poi == null) return;

        if (!AuthApiClient.IsLoggedIn())
        {
            await DisplayAlertAsync("Cần đăng nhập", "Tính năng chỉ đường chỉ dành cho tài khoản PRO. Vui lòng đăng nhập/nâng cấp.", "OK");
            return;
        }

        if (!AuthApiClient.IsPro())
        {
            await Shell.Current.GoToAsync("proupgrade");
            return;
        }

        var userLat = _userLocation?.Latitude;
        var userLng = _userLocation?.Longitude;
        if (!userLat.HasValue || !userLng.HasValue)
        {
            await DisplayAlertAsync("Chưa có vị trí", "Không lấy được vị trí hiện tại để chỉ đường. Vui lòng bật GPS và thử lại.", "OK");
            return;
        }

        var dto = await _profileApi.GetDirectionsAsync(poi.Id, userLat, userLng);
        if (dto?.RouteCoordinates == null || dto.RouteCoordinates.Count < 2)
        {
            await DisplayAlertAsync("Không lấy được tuyến đường", dto?.Message ?? "Vui lòng thử lại sau.", "OK");
            return;
        }

        if (_routePolyline != null)
            MyMap.MapElements.Remove(_routePolyline);

        _routePolyline = new Polyline
        {
            StrokeColor = Color.FromArgb("#1976D2"),
            StrokeWidth = 6
        };

        foreach (var c in dto.RouteCoordinates)
            _routePolyline.Geopath.Add(new Location(c.Lat, c.Lng));

        MyMap.MapElements.Add(_routePolyline);
    }

    private async Task LoadPoiImagesAsync(Poi poi)
    {
        try
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.None)
            {
                var imageUrls = await _poiApi.GetImagesAsync(poi.Id);
                if (imageUrls.Count > 0)
                {
                    // Tạo UriImageSource objects trực tiếp để tránh lỗi string→ImageSource trong DataTemplate
                    var sources = imageUrls
                        .Select(url => new UriImageSource
                        {
                            Uri = new Uri(ToAbsoluteUrl(url)),
                            CachingEnabled = true,
                            CacheValidity = TimeSpan.FromHours(24)
                        })
                        .Cast<ImageSource>()
                        .ToList();

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        PoiImageCarousel.ItemsSource = sources;
                        PoiImageCarousel.IsVisible = true;
                        PoiImageIndicator.IsVisible = sources.Count > 1;
                        PoiImage.IsVisible = false;
                    });
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PoiImages] {ex.Message}");
        }

        // Fallback: ảnh đơn từ PoiMedia
        if (!string.IsNullOrWhiteSpace(poi.ImageUrl))
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                PoiImage.Source = new UriImageSource
                {
                    Uri = new Uri(ToAbsoluteUrl(poi.ImageUrl!)),
                    CachingEnabled = true,
                    CacheValidity = TimeSpan.FromHours(24)
                });
        }
    }

    private string ToAbsoluteUrl(string url)
    {
        if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return url;
        var baseUrl = _poiApi.BaseUrl.TrimEnd('/');
        return $"{baseUrl}/{url.TrimStart('/')}";
    }

    private async Task OpenMapsAsync(Poi? poi)
    {
        if (poi == null) return;
        var url = !string.IsNullOrWhiteSpace(poi.MapLink) ? poi.MapLink : $"https://maps.google.com/?q={poi.Latitude},{poi.Longitude}";
        try { await Launcher.OpenAsync(new Uri(url)); } catch { }
    }

    void SetupBottomSheetOffsets()
    {
        if (BottomSheet.Height <= 0 || this.Height <= 0) return;
        _sheetCollapsedOffset = Math.Max(0, BottomSheet.Height - SheetPeekHeight);
        if (!_sheetReady) { BottomSheet.TranslationY = _sheetCollapsedOffset; _sheetReady = true; }
    }

    void BottomSheet_TapHeader(object? sender, EventArgs e)
    {
        if (!_sheetReady) return;
        var isCollapsed = Math.Abs(BottomSheet.TranslationY - _sheetCollapsedOffset) < 1;
        _ = BottomSheet.TranslateToAsync(0, isCollapsed ? _sheetExpandedOffset : _sheetCollapsedOffset, 200, Easing.CubicOut);
    }

    void BottomSheet_PanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (!_sheetReady) return;
        switch (e.StatusType)
        {
            case GestureStatus.Started: _sheetStartPanY = BottomSheet.TranslationY; break;
            case GestureStatus.Running:
                BottomSheet.TranslationY = Math.Clamp(_sheetStartPanY + e.TotalY, _sheetExpandedOffset, _sheetCollapsedOffset); break;
            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                var half = (_sheetCollapsedOffset - _sheetExpandedOffset) / 2.0;
                var curr = BottomSheet.TranslationY - _sheetExpandedOffset;
                _ = BottomSheet.TranslateToAsync(0, curr <= half ? _sheetExpandedOffset : _sheetCollapsedOffset, 180, Easing.CubicOut);
                break;
        }
    }

    private void OnLocateMeClicked(object? sender, EventArgs e)
    {
        if (_userLocation != null) MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(_userLocation, Distance.FromKilometers(1)));
        else _ = MoveToRealLocationAsync();
    }

    private void OnZoomInClicked(object? sender, EventArgs e)
    {
        if (MyMap.VisibleRegion == null) return;
        var newRadius = MyMap.VisibleRegion.Radius.Kilometers * 0.5;
        MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(MyMap.VisibleRegion.Center, Distance.FromKilometers(newRadius)));
    }

    private void OnZoomOutClicked(object? sender, EventArgs e)
    {
        if (MyMap.VisibleRegion == null) return;
        var newRadius = MyMap.VisibleRegion.Radius.Kilometers * 2.0;
        if (newRadius > 20000) newRadius = 20000;
        MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(MyMap.VisibleRegion.Center, Distance.FromKilometers(newRadius)));
    }

    private async Task<string?> TranslateTextAsync(string? text, string fromLang, string toLang, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text) || fromLang == toLang) return text;
        try { return await _translator.TryTranslateAsync(text, toLang, fromLang, ct) ?? text; }
        catch { return text; }
    }

    private async void OnMapClicked(object? sender, MapClickedEventArgs e)
    {
        var clickLocation = e.Location;

        // Tìm tất cả các POI mà điểm click nằm trong vòng tròn, sắp xếp theo khoảng cách gần nhất
        var tappedPois = _pois
            .Select(p => new { Poi = p, Distance = Location.CalculateDistance(clickLocation, new Location(p.Latitude, p.Longitude), DistanceUnits.Kilometers) * 1000 })
            .Where(x => x.Distance <= x.Poi.RadiusMeters)
            .OrderBy(x => x.Distance)
            .ToList();

        if (tappedPois.Count > 0)
        {
            // Chỉ hiển thị UI cho điểm gần nhất
            var closestPoi = tappedPois[0].Poi;
            HighlightPoi(closestPoi, "Đã chọn");
            ShowDetail(closestPoi);
            MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(new Location(closestPoi.Latitude, closestPoi.Longitude), Distance.FromMeters(300)));
            if (_sheetReady) _ = BottomSheet.TranslateToAsync(0, _sheetExpandedOffset, 250, Easing.CubicOut);
            
            if (!await EnsureUsageAllowedAsync("POI_LISTEN", closestPoi.Id))
                return;
            
            var lang = LanguageService.Current;
            
            // Đẩy TẤT CẢ các điểm POI đã chạm vào hàng đợi thuyết minh
            foreach (var item in tappedPois)
            {
                var p = item.Poi;
                _ = _narrationHandler.PlayAsync(p, PoiEventType.Tap, "TAP");
                // Delay nhỏ để CreatedAtUtc khác nhau → queue ưu tiên đúng thứ tự
                await Task.Delay(10);
            }
        }
    }
}