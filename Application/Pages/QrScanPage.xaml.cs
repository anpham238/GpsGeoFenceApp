using System.Text.Json;
using MauiApp1.Data;
using MauiApp1.Services.Api;
using MauiApp1.Services.Narration;
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;
#if ANDROID
using Android.Graphics;
using ZXing;
using ZXing.Common;
#endif

namespace MauiApp1.Pages;

public partial class QrScanPage : ContentPage
{
    private readonly PoiDatabase _db;
    private readonly NarrationManager _narration;
    private readonly PlaybackApiClient _playback;
    private readonly TicketApiClient _ticketApi; // Thêm API gọi vé
    private readonly UsageApiClient _usage;
    private bool _isProcessing;
    private bool _torchOn;
    private CameraBarcodeReaderView? _cameraView;

    public QrScanPage(
        PoiDatabase db,
        NarrationManager narration,
        PlaybackApiClient playback,
        TicketApiClient ticketApi,
        UsageApiClient usage)
    {
        InitializeComponent();
        _db = db;
        _narration = narration;
        _playback = playback;
        _ticketApi = ticketApi;
        _usage = usage;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _isProcessing = false;
        if (_cameraView == null)
        {
            try
            {
                _cameraView = new CameraBarcodeReaderView
                {
                    Options = new BarcodeReaderOptions { Formats = BarcodeFormats.TwoDimensional, AutoRotate = true },
                    IsDetecting = true
                };
                _cameraView.BarcodesDetected += OnBarcodesDetected;
                CameraContainer.Content = _cameraView;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CameraInit ERROR] {ex}");
                LblStatus.Text = "Lỗi khởi động camera. Vui lòng thử lại.";
            }
        }
        else
        {
            _cameraView.IsDetecting = true;
        }

        _ = LoadQuotaBadgeAsync();
    }

    private async Task LoadQuotaBadgeAsync()
    {
        try
        {
            if (AuthApiClient.IsPro())
            {
                LblQuota.Text = "🌟 PRO: Quét không giới hạn";
                QuotaBadge.BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#CC006400");
                return;
            }
            var entityId = UsageApiClient.GetEntityId();
            var (allowed, resetInHours) = await _usage.CheckAsync(entityId, "QR_SCAN");
            LblQuota.Text = allowed
                ? "⏱️ Còn lượt quét hôm nay"
                : $"❌ Hết lượt. Làm mới sau {resetInHours:F1}h";
        }
        catch
        {
            QuotaBadge.IsVisible = false;
        }
    }

    protected override void OnDisappearing()
    {
        if (_cameraView != null)
        {
            _cameraView.IsDetecting = false;
            _cameraView.IsTorchOn = false;
            _cameraView.BarcodesDetected -= OnBarcodesDetected;
            CameraContainer.Content = null;
            _cameraView = null;
        }
        _torchOn = false;
        base.OnDisappearing();
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        if (_isProcessing) return;
        var raw = e.Results?.FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(raw)) return;

        _isProcessing = true;
        MainThread.BeginInvokeOnMainThread(async () => await HandleQrValueAsync(raw.Trim()));
    }

    private async Task HandleQrValueAsync(string raw)
    {
        try
        {
            if (_cameraView != null) _cameraView.IsDetecting = false;
            LblStatus.Text = "Đang xử lý...";

            // Paywall (Freemium Quota) cho hành động quét QR
            if (!AuthApiClient.IsPro())
            {
                var entityId = UsageApiClient.GetEntityId();
                var (allowed, resetInHours) = await _usage.CheckAsync(entityId, "QR_SCAN");
                if (!allowed)
                {
                    LblStatus.Text = "Hết lượt miễn phí";
                    var goUpgrade = await DisplayAlertAsync(
                        "Hết lượt quét QR miễn phí",
                        $"Bạn đã dùng hết lượt quét QR. Lượt sẽ được làm mới sau khoảng {resetInHours:F1} giờ.\n\nNâng cấp PRO để dùng không giới hạn.",
                        "Nâng cấp PRO",
                        "Đóng");

                    if (goUpgrade)
                        await Shell.Current.GoToAsync("proupgrade");
                    await ResumeScanAsync();
                    return;
                }
            }

            // =========================================================
            // TRƯỜNG HỢP 1: KIỂM TRA MÃ QR VÉ (TICKET) HOẶC ID TRỰC TIẾP
            // =========================================================
            var extractedData = ExtractQrData(raw);

            // Nếu đây là mã Vé (có giới hạn số lần quét)
            if (!string.IsNullOrEmpty(extractedData.TicketId))
            {
                var result = await _ticketApi.ScanTicketAsync(extractedData.TicketId);
                if (result == null)
                {
                    LblStatus.Text = "Vé không hợp lệ";
                    await DisplayAlertAsync("Lỗi", "Vé này không tồn tại hoặc đã sử dụng hết số lần cho phép (5 lần).", "OK");
                    await ResumeScanAsync();
                    return;
                }

                var poi = await _db.GetByIdAsync(result.PoiId);
                if (poi != null)
                {
                    LblStatus.Text = $"✓ {poi.Name} (Còn {result.Remaining} lần)";
                    await _narration.HandleAsync(new Announcement(poi, result.Language, PoiEventType.Tap, DateTime.UtcNow));
                    await DisplayAlertAsync("Thành công", $"Đang phát thuyết minh...\n(Vé của bạn còn {result.Remaining} lần quét)", "OK");
                    await CloseAsync();
                    return;
                }
            }
            // Nếu là QR quét ID địa điểm bình thường (không giới hạn)
            else if (extractedData.PoiId.HasValue)
            {
                var poi = await _db.GetByIdAsync(extractedData.PoiId.Value);
                if (poi != null)
                {
                    LblStatus.Text = $"✓ {poi.Name}";
                    var lang = MauiApp1.Services.LanguageService.Current;
                    await _narration.HandleAsync(new Announcement(poi, lang, PoiEventType.Tap, DateTime.UtcNow));
                    await DisplayAlertAsync(poi.Name, "Đang phát thuyết minh...", "OK");
                    await CloseAsync();
                    return;
                }
            }

            // =========================================================
            // TRƯỜNG HỢP 2: KIỂM TRA LINK WEB
            // =========================================================
            if (Uri.TryCreate(raw, UriKind.Absolute, out Uri? uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
            {
                LblStatus.Text = "Đang mở trình duyệt...";
                bool openLink = await DisplayAlertAsync("Quét được liên kết", $"Bạn có muốn mở trang web này không?\n\n{raw}", "Mở", "Hủy");
                if (openLink) await Launcher.Default.OpenAsync(uriResult);
                await ResumeScanAsync();
                return;
            }

            LblStatus.Text = "Mã QR không hợp lệ";
            await DisplayAlertAsync("Lỗi quét mã", "Mã QR này không thuộc hệ thống ứng dụng.", "Quét lại");
            await ResumeScanAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QR Error] {ex.Message}");
            await ResumeScanAsync();
        }
    }

    // Hàm bóc tách ID hoặc Ticket từ JSON
    private static (string? TicketId, int? PoiId) ExtractQrData(string raw)
    {
        const string prefix = "smarttourism://poi/";
        if (raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(raw[prefix.Length..].Trim('/'), out int id)) return (null, id);
        }
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("ticket_id", out var ticketIdElement))
                return (ticketIdElement.GetString(), null);

            if (doc.RootElement.TryGetProperty("poi_id", out var poiIdElement))
            {
                int? id = poiIdElement.ValueKind == JsonValueKind.Number ? poiIdElement.GetInt32() : int.Parse(poiIdElement.GetString() ?? "0"); return (null, id);
            }
        }
        catch { }

        if (int.TryParse(raw.Trim(), out var directId)) return (null, directId);
        return (null, null);
    }

    private async Task ResumeScanAsync()
    {
        _isProcessing = false;
        await Task.Delay(500);
        if (_cameraView != null) _cameraView.IsDetecting = true;
        LblStatus.Text = "Sẵn sàng quét";
    }

    private void OnToggleTorchClicked(object? sender, EventArgs e)
    {
        _torchOn = !_torchOn;
        if (_cameraView != null) _cameraView.IsTorchOn = _torchOn;
    }

    private async void OnCloseClicked(object? sender, EventArgs e) => await CloseAsync();

    private async void OnPickImageClicked(object? sender, EventArgs e)
    {
        try
        {
            if (_isProcessing) return;
            _isProcessing = true;
            if (_cameraView != null) _cameraView.IsDetecting = false;
            LblStatus.Text = "Đang chọn ảnh...";

            var photos = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions { Title = "Chọn ảnh chứa QR" });
            var photo = photos?.FirstOrDefault();
            if (photo is null) { await ResumeScanAsync(); return; }

            LblStatus.Text = "Đang đọc QR từ ảnh...";
            var raw = await DecodeQrFromImageAsync(photo);
            if (string.IsNullOrWhiteSpace(raw))
            {
                await DisplayAlertAsync("Không tìm thấy mã QR", "Ảnh bạn chọn không có QR hoặc QR không rõ.", "OK");
                await ResumeScanAsync();
                return;
            }
            await HandleQrValueAsync(raw.Trim());
        }
        catch (Exception)
        {
            await DisplayAlertAsync("Lỗi", "Không thể đọc ảnh QR.", "OK");
            await ResumeScanAsync();
        }
    }

#if ANDROID
    private static async Task<string?> DecodeQrFromImageAsync(FileResult photo)
    {
        await using var stream = await photo.OpenReadAsync();
        using var bitmap = BitmapFactory.DecodeStream(stream);
        if (bitmap is null) return null;
        int width = bitmap.Width;
        int height = bitmap.Height;
        var pixels = new int[width * height];
        bitmap.GetPixels(pixels, 0, width, 0, 0, width, height);
        var rgba = new byte[width * height * 4];
        for (int i = 0; i < pixels.Length; i++)
        {
            var color = pixels[i];
            var offset = i * 4;
            rgba[offset] = (byte)((color >> 16) & 0xFF);      // R
            rgba[offset + 1] = (byte)((color >> 8) & 0xFF);   // G
            rgba[offset + 2] = (byte)(color & 0xFF);          // B
            rgba[offset + 3] = (byte)((color >> 24) & 0xFF);  // A
        }
        var source = new RGBLuminanceSource(rgba, width, height, RGBLuminanceSource.BitmapFormat.RGBA32);
        var reader = new BarcodeReaderGeneric { AutoRotate = true, Options = new DecodingOptions { TryHarder = true, PossibleFormats = [ZXing.BarcodeFormat.QR_CODE] } };
        return reader.Decode(source)?.Text;
    }
#else
    private static Task<string?> DecodeQrFromImageAsync(FileResult photo) => Task.FromResult<string?>(null);
#endif

    private Task CloseAsync() => Shell.Current!.GoToAsync("..");
}