using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using MauiApp1.Data;
using MauiApp1.Services.Api;
using MauiApp1.Services.Narration;
using ZXing.Net.Maui;

namespace MauiApp1.Pages;

public partial class QrScanPage : ContentPage
{
    private readonly PoiDatabase _db;
    private readonly NarrationManager _narration;
    private readonly PlaybackApiClient _playback;

    private bool _isProcessing;

    public QrScanPage(PoiDatabase db, NarrationManager narration, PlaybackApiClient playback)
    {
        InitializeComponent();
        _db = db;
        _narration = narration;
        _playback = playback;

        // (Tuỳ chọn) chỉ quét QR để nhanh hơn
        BarcodeReader.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormats.TwoDimensional, // QR thuộc nhóm 2D
            AutoRotate = true,
            Multiple = false
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Xin quyền camera runtime
        var status = await Permissions.RequestAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
        {
            await DisplayAlertAsync("Cần quyền Camera", "Vui lòng cấp quyền camera để quét mã QR.", "OK");
            await CloseAsync();
            return;
        }

        _isProcessing = false;
        BarcodeReader.IsDetecting = true;
        LblStatus.Text = "Sẵn sàng quét";
    }

    protected override void OnDisappearing()
    {
        BarcodeReader.IsDetecting = false;
        base.OnDisappearing();
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        if (_isProcessing) return;

        var raw = e.Results?.FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(raw)) return;

        _isProcessing = true;

        // chạy về UI thread
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

            // Hỗ trợ 2 format:
            // 1) smarttourism://poi/<id>
            // 2) <id> thuần
            var poiId = raw;
            const string prefix = "smarttourism://poi/";
            if (raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                poiId = raw.Substring(prefix.Length).Trim('/');

            var poi = await _db.GetByIdAsync(poiId);

            if (poi == null)
            {
                LblStatus.Text = "Không tìm thấy điểm này";
                await DisplayAlertAsync("Không tìm thấy",
                    $"Mã QR không hợp lệ hoặc POI chưa được tải.\nID: {poiId}",
                    "OK");

                AllowRescan();
                return;
            }

            LblStatus.Text = $"✓ {poi.Name}";

            // Phát thuyết minh (dùng NarrationManager)
            var started = DateTime.UtcNow;
            await _narration.HandleAsync(new Announcement(poi, PoiEventType.Tap, started));
            var dur = (int)(DateTime.UtcNow - started).TotalSeconds;

            // Log playback kiểu QR
            _ = _playback.LogAsync(poi.Id, "QR", dur > 0 ? dur : null);

            await DisplayAlertAsync(poi.Name,
                string.IsNullOrWhiteSpace(poi.Description) ? "Đang phát thuyết minh..." : poi.Description,
                "OK");

            await CloseAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QR] {ex}");
            LblStatus.Text = "Lỗi xử lý mã QR";
            AllowRescan();
        }
    }

    private void AllowRescan()
    {
        _isProcessing = false;
        BarcodeReader.IsDetecting = true;
        LblStatus.Text = "Sẵn sàng quét";
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        await CloseAsync();
    }

    private Task CloseAsync()
    {
        // Nếu bạn dùng Shell routing, đi lùi:
        return Shell.Current.GoToAsync("..");
    }
}