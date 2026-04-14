using System;
using System.Collections.Generic;
using System.Text;
#if ANDROID
using global::Android.Content;
using global::Android.Gms.Location;
using global::Android.OS;
using global::MauiApp1.Services;
using Microsoft.Maui.Devices;

namespace MauiApp1.Platforms.Android.Services;
public sealed class AndroidLocationService : ILocationService
{
    private global::Android.Gms.Location.IFusedLocationProviderClient? _client;
    private global::Android.Gms.Location.LocationCallback? _callback;

    public AndroidLocationService()
    {
        _client = LocationServices.GetFusedLocationProviderClient(global::Android.App.Application.Context);
    }

    public void StartTracking(Action<double, double> onLocation)
    {
        if (_client == null) return;

        // Giảm tần suất GPS khi pin < 20%
        var batteryLevel = Battery.Default.ChargeLevel; // 0.0 → 1.0
        long intervalMs = batteryLevel < 0.20 ? 30_000L : 5_000L;
        float minDistM  = batteryLevel < 0.20 ? 30f : 10f;

        var request = new global::Android.Gms.Location.LocationRequest
            .Builder(global::Android.Gms.Location.Priority.PriorityBalancedPowerAccuracy, intervalMs)
            .SetMinUpdateDistanceMeters(minDistM)
            .Build();

        _callback = new CallbackImpl(onLocation);
        _client.RequestLocationUpdates(request, _callback, global::Android.OS.Looper.MainLooper);
        System.Diagnostics.Debug.WriteLine($"[GPS] interval={intervalMs}ms, battery={batteryLevel:P0}");
    }

    public void StopTracking()
    {
        if (_client != null && _callback != null)
            _client.RemoveLocationUpdates(_callback);
    }

    private sealed class CallbackImpl : global::Android.Gms.Location.LocationCallback
    {
        private readonly Action<double, double> _onLocation;
        public CallbackImpl(Action<double, double> onLocation) => _onLocation = onLocation;

        public override void OnLocationResult(global::Android.Gms.Location.LocationResult? result)
        {
            var loc = result?.LastLocation;
            if (loc != null) _onLocation(loc.Latitude, loc.Longitude);
        }
    }
}
#endif