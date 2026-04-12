using System.Text.Json;
using MauiApp1.Data;
using MauiApp1.Services.Api;
using MauiApp1.Services.Narration;
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace MauiApp1.Pages;

public partial class QrScanPage : ContentPage
{
    private readonly PoiDatabase _db;
    private readonly NarrationManager _narration;
    private readonly PlaybackApiClient _playback;

    private bool _isProcessing;
    private bool _torchOn;
    private CameraBarcodeReaderView? _cameraView;

    public QrScanPage(PoiDatabase db, NarrationManager narration, PlaybackApiClient playback)
    {
        InitializeComponent();
        _db = db;
        _narration = narration;
        _playback = playback;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _isProcessing = false;

        // Bơm Camera bằng code C# để tránh lỗi vòng đời của Android
        if (_cameraView == null)
        {
            _cameraView = new CameraBarcodeReaderView
            {
                Options = new BarcodeReaderOptions { Formats = BarcodeFormats.TwoDimensional, AutoRotate = true },
                IsDetecting = true
            };
            _cameraView.BarcodesDetected += OnBarcodesDetected;
            CameraContainer.Content = _cameraView;
        }
        else
        {
            _cameraView.IsDetecting = true;
        }
    }

    protected override void OnDisappearing()
    {
        if (_cameraView != null)
        {
            _cameraView.IsDetecting = false;
            _cameraView.IsTorchOn = false; // Tắt đèn khi rời khỏi trang
            _torchOn = false;
        }
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

            // =========================================================
            // TRƯỜNG HỢP 1: KIỂM TRA MÃ QR CỦA HỆ THỐNG THUYẾT MINH APP
            // =========================================================
            var poiId = ParsePoiId(raw);
            if (poiId.HasValue)
            {
                var poi = await _db.GetByIdAsync(poiId.Value);
                if (poi != null)
                {
                    // Mã hợp lệ của App -> Phát thuyết minh
                    LblStatus.Text = $"✓ {poi.Name}";
                    var started = DateTime.UtcNow;
                    await _narration.HandleAsync(new Announcement(poi, PoiEventType.Tap, started));
                    await DisplayAlert(poi.Name, "Đang phát thuyết minh...", "OK");
                    await CloseAsync();
                    return; // Kết thúc
                }
            }

            // =========================================================
            // TRƯỜNG HỢP 2: KIỂM TRA XEM CÓ PHẢI LÀ ĐƯỜNG LINK (URL) KHÔNG
            // =========================================================
            if (Uri.TryCreate(raw, UriKind.Absolute, out Uri? uriResult)
                && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
            {
                LblStatus.Text = "Đang mở trình duyệt...";

                // Hỏi người dùng xem có muốn mở trình duyệt ngoài không
                bool openLink = await DisplayAlert("Quét được liên kết (Link)", $"Bạn có muốn mở trang web này không?\n\n{raw}", "Mở", "Hủy");

                if (openLink)
                {
                    await Launcher.Default.OpenAsync(uriResult);
                }

                await ResumeScanAsync(); // Quét xong cho phép quét tiếp
                return; // Kết thúc
            }

            // =========================================================
            // TRƯỜNG HỢP 3: MÃ KHÔNG HỢP LỆ (KHÔNG PHẢI POI, KHÔNG PHẢI LINK)
            // =========================================================
            LblStatus.Text = "Mã QR không hợp lệ";
            await DisplayAlert("Lỗi quét mã", "Mã QR này không thuộc hệ thống ứng dụng và cũng không phải là một đường dẫn hợp lệ.", "Quét lại");

            await ResumeScanAsync(); // Bật lại camera cho người dùng quét mã khác
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QR Error] {ex.Message}");
            await ResumeScanAsync();
        }
    }

    // Hàm hỗ trợ bóc tách ID địa điểm (int) từ QR Code
    private static int? ParsePoiId(string raw)
    {
        const string prefix = "smarttourism://poi/";
        string? idStr = null;

        if (raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            idStr = raw[prefix.Length..].Trim('/');
        else
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.TryGetProperty("poi_id", out var poiIdElement))
                    idStr = poiIdElement.ValueKind == System.Text.Json.JsonValueKind.Number
                        ? poiIdElement.GetInt32().ToString()
                        : poiIdElement.GetString()?.Trim();
            }
            catch { }
        }

        if (idStr is not null && int.TryParse(idStr, out var result))
            return result;

        // Fallback: maybe the whole raw string is just a number
        if (int.TryParse(raw.Trim(), out var directId))
            return directId;

        return null;
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
        try
        {
            _torchOn = !_torchOn;
            if (_cameraView != null)
            {
                _cameraView.IsTorchOn = _torchOn;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Flash Error] {ex.Message}");
        }
    }

    private async void OnCloseClicked(object? sender, EventArgs e) => await CloseAsync();

    private Task CloseAsync() => Shell.Current!.GoToAsync("..");
}