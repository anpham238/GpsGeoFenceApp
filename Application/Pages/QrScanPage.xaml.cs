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
    private bool _torchOn; // ✅ Biến lưu trạng thái bật/tắt đèn flash
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

            var poiId = raw.Replace("smarttourism://poi/", "").Trim('/');
            var poi = await _db.GetByIdAsync(poiId);

            if (poi == null)
            {
                LblStatus.Text = "Không tìm thấy POI";
                await DisplayAlert("Lỗi", "POI chưa có offline.", "OK");
                await ResumeScanAsync();
                return;
            }

            LblStatus.Text = $"✓ {poi.Name}";
            var started = DateTime.UtcNow;
            await _narration.HandleAsync(new Announcement(poi, PoiEventType.Tap, started));
            await DisplayAlert(poi.Name, "Đang phát thuyết minh...", "OK");
            await CloseAsync();
        }
        catch (Exception)
        {
            await ResumeScanAsync();
        }
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
            _torchOn = !_torchOn; // Đảo ngược trạng thái
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