using System.Text.Json;
using MauiApp1.Data;
using MauiApp1.Services.Api;
using MauiApp1.Services.Narration;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using ZXing.Net.Maui;

namespace MauiApp1.Pages;

public partial class QrScanPage : ContentPage
{
    private readonly PoiDatabase _db;
    private readonly NarrationManager _narration;
    private readonly PlaybackApiClient _playback;

    private bool _isProcessing;
    private bool _cameraStarted;
    private bool _torchOn;

    public QrScanPage(PoiDatabase db, NarrationManager narration, PlaybackApiClient playback)
    {
        InitializeComponent();

        _db = db;
        _narration = narration;
        _playback = playback;

        BarcodeReader.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormats.TwoDimensional,
            AutoRotate = true,
            Multiple = false
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // 1) Ensure page rendered before camera start
        await Task.Delay(120);

        // 2) Ask camera permission
        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
            status = await Permissions.RequestAsync<Permissions.Camera>();

        if (status != PermissionStatus.Granted)
        {
            LblStatus.Text = "Chưa có quyền camera";
            var openSettings = await DisplayAlertAsync(
                "Cần quyền Camera",
                "Vui lòng cấp quyền camera để quét mã QR.",
                "Mở cài đặt",
                "Đóng");

            if (openSettings)
            {
                AppInfo.ShowSettingsUI();
            }

            await CloseAsync();
            return;
        }

        _isProcessing = false;
        await StartCameraSafelyAsync();
    }

    protected override void OnDisappearing()
    {
        StopCamera();
        base.OnDisappearing();
    }

    private async Task StartCameraSafelyAsync()
    {
        try
        {
            // reset before start to avoid black preview on some devices
            BarcodeReader.IsDetecting = false;
            await Task.Delay(150);

            BarcodeReader.IsTorchOn = false;
            _torchOn = false;

            BarcodeReader.IsDetecting = true;
            _cameraStarted = true;

            LblStatus.Text = "Sẵn sàng quét";
        }
        catch (Exception ex)
        {
            _cameraStarted = false;
            LblStatus.Text = "Không mở được camera";
            System.Diagnostics.Debug.WriteLine($"[QR] Start camera error: {ex}");
        }
    }

    private void StopCamera()
    {
        try
        {
            BarcodeReader.IsTorchOn = false;
            BarcodeReader.IsDetecting = false;
        }
        catch
        {
            // ignore
        }

        _cameraStarted = false;
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        if (!_cameraStarted || _isProcessing) return;

        var raw = e.Results?.FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(raw)) return;

        _isProcessing = true;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await HandleQrValueAsync(raw.Trim());
        });
    }

    private async Task HandleQrValueAsync(string raw)
    {
        try
        {
            BarcodeReader.IsDetecting = false;
            LblStatus.Text = "Đang xử lý...";

            var poiId = ParsePoiId(raw);
            if (string.IsNullOrWhiteSpace(poiId))
            {
                LblStatus.Text = "QR không hợp lệ";
                await DisplayAlertAsync("QR lỗi", "Không đọc được POI ID từ mã QR.", "OK");
                await ResumeScanAsync();
                return;
            }

            var poi = await _db.GetByIdAsync(poiId);
            if (poi == null)
            {
                LblStatus.Text = "Không tìm thấy POI";
                await DisplayAlertAsync("Không tìm thấy", $"POI chưa có offline.\nID: {poiId}", "OK");
                await ResumeScanAsync();
                return;
            }

            LblStatus.Text = $"✓ {poi.Name}";

            var started = DateTime.UtcNow;
            await _narration.HandleAsync(new Announcement(poi, PoiEventType.Tap, started));
            var dur = (int)(DateTime.UtcNow - started).TotalSeconds;

            _ = _playback.LogAsync(poi.Id, "QR", dur > 0 ? dur : null);

            await DisplayAlertAsync(
                poi.Name,
                string.IsNullOrWhiteSpace(poi.Description) ? "Đang phát thuyết minh..." : poi.Description,
                "OK");

            await CloseAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QR] Handle error: {ex}");
            LblStatus.Text = "Lỗi xử lý QR";
            await ResumeScanAsync();
        }
    }

    private static string? ParsePoiId(string raw)
    {
        // 1) deep link format: smarttourism://poi/<id>
        const string prefix = "smarttourism://poi/";
        if (raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return raw[prefix.Length..].Trim('/');

        // 2) JSON payload: { "poi_id": "KH-001", "version": 1 }
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("poi_id", out var poiIdElement))
                return poiIdElement.GetString()?.Trim();
        }
        catch
        {
            // not json, ignore
        }

        // 3) plain id
        return raw.Trim();
    }

    private async Task ResumeScanAsync()
    {
        _isProcessing = false;
        await Task.Delay(200);
        BarcodeReader.IsDetecting = true;
        LblStatus.Text = "Sẵn sàng quét";
    }

    private void OnToggleTorchClicked(object? sender, EventArgs e)
    {
        try
        {
            _torchOn = !_torchOn;
            BarcodeReader.IsTorchOn = _torchOn;
        }
        catch
        {
            // device may not support torch
        }
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        await CloseAsync();
    }

    private Task CloseAsync() => Shell.Current.GoToAsync("..");
}