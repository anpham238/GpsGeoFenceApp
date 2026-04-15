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
    private bool _isProcessing;
    private bool _torchOn;
    private CameraBarcodeReaderView? _cameraView;
    private readonly TicketApiClient _ticketApi;


    public QrScanPage(PoiDatabase db, NarrationManager narration, PlaybackApiClient playback, TicketApiClient ticketApi)
    {
        InitializeComponent();
        _db = db;
        _narration = narration;
        _playback = playback;
        _ticketApi = ticketApi;
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
            // TRƯỜNG HỢP 1: KIỂM TRA MÃ QR VÉ (TICKET) HOẶC ID TRỰC TIẾP
            // =========================================================
            var extractedData = ExtractQrData(raw);

            if (!string.IsNullOrEmpty(extractedData.TicketId))
            {
                // Gọi API để kiểm tra vé
                var result = await _ticketApi.ScanTicketAsync(extractedData.TicketId);
                if (result == null)
                {
                    LblStatus.Text = "Vé không hợp lệ";
                    await DisplayAlertAsync("Lỗi", "Vé này không tồn tại hoặc đã sử dụng hết số lần cho phép (5 lần).", "OK");
                    await ResumeScanAsync();
                    return;
                }

                // Nếu vé còn hạn, phát âm thanh
                var poi = await _db.GetByIdAsync(result.PoiId);
                if (poi != null)
                {
                    LblStatus.Text = $"✓ {poi.Name} (Còn {result.Remaining} lần)";
                    await _narration.HandleAsync(new Announcement(poi, result.Language, PoiEventType.Tap, DateTime.UtcNow));
                    await DisplayAlertAsync("Thành công", $"Đang phát thuyết minh...\n(Vé còn {result.Remaining} lần quét)", "OK");
                    await CloseAsync();
                    return;
                }
            }
            else if (extractedData.PoiId.HasValue) // Mã quét kiểu ID cổ điển
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
            // TRƯỜNG HỢP 2: KIỂM TRA XEM CÓ PHẢI LÀ ĐƯỜNG LINK (URL) KHÔNG
            // =========================================================
            if (Uri.TryCreate(raw, UriKind.Absolute, out Uri? uriResult)
                && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
            {
                LblStatus.Text = "Đang mở trình duyệt...";

                // Hỏi người dùng xem có muốn mở trình duyệt ngoài không
                bool openLink = await DisplayAlertAsync("Quét được liên kết (Link)", $"Bạn có muốn mở trang web này không?\n\n{raw}", "Mở", "Hủy");

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
            await DisplayAlertAsync("Lỗi quét mã", "Mã QR này không thuộc hệ thống ứng dụng và cũng không phải là một đường dẫn hợp lệ.", "Quét lại");

            await ResumeScanAsync(); // Bật lại camera cho người dùng quét mã khác
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QR Error] {ex.Message}");
            await ResumeScanAsync();
        }
    }
    private static (string? TicketId, int? PoiId) ExtractQrData(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("ticket_id", out var ticketIdElement))
            {
                return (ticketIdElement.GetString(), null);
            }
            if (doc.RootElement.TryGetProperty("poi_id", out var poiIdElement))
            {
                int? id = poiIdElement.ValueKind == JsonValueKind.Number ? poiIdElement.GetInt32() : int.Parse(poiIdElement.GetString() ?? "0");
                return (null, id);
            }
        }
        catch { }

        if (int.TryParse(raw.Trim(), out var directId))
            return (null, directId);

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

    private async void OnPickImageClicked(object? sender, EventArgs e)
    {
        try
        {
            if (_isProcessing) return;

            _isProcessing = true;
            if (_cameraView != null) _cameraView.IsDetecting = false;
            LblStatus.Text = "Đang chọn ảnh...";

            var photos = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions
            {
                Title = "Chọn ảnh chứa QR"
            });
            var photo = photos?.FirstOrDefault();
            if (photo is null)
            {
                await ResumeScanAsync();
                return;
            }

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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QR Pick Image Error] {ex.Message}");
            await DisplayAlertAsync("Lỗi chọn ảnh", "Không thể đọc ảnh QR. Vui lòng thử lại.", "OK");
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

        var source = new RGBLuminanceSource(
            rgba,
            width,
            height,
            RGBLuminanceSource.BitmapFormat.RGBA32);
        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new DecodingOptions
            {
                TryHarder = true,
                PossibleFormats = [ZXing.BarcodeFormat.QR_CODE]
            }
        };

        var result = reader.Decode(source);
        return result?.Text;
    }
#else
    private static Task<string?> DecodeQrFromImageAsync(FileResult photo)
        => Task.FromResult<string?>(null);
#endif

    private Task CloseAsync() => Shell.Current!.GoToAsync("..");
}