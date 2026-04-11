#if ANDROID
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Gms.Location;
using AndroidX.Core.Content;
using MauiApp1.Models;
using MauiApp1.Services;
namespace MauiApp1.Platforms.Android.Services
{
    public sealed class AndroidGeofenceService : IGeofenceService
    {
        private readonly Context _ctx = global::Android.App.Application.Context;
        private readonly IGeofencingClient _client;
        private readonly PendingIntent _pendingIntent;

        private Dictionary<string, Poi> _poiLookup = new();

        public event Action<Poi, string>? OnPoiEvent;

        public AndroidGeofenceService()
        {
            _client = LocationServices.GetGeofencingClient(_ctx);
            _pendingIntent = CreatePendingIntent();

            // lắng nghe broadcast hub -> map sang OnPoiEvent
            MauiApp1.Platforms.Android.GeofenceEventHub.OnTransition += HandleTransition;
        }

        private PendingIntent CreatePendingIntent()
        {
            var intent = new Intent(_ctx, typeof(MauiApp1.Platforms.Android.GeofenceBroadcastReceiver));
            intent.SetAction("com.google.android.location.GEOFENCE_TRANSITION");

            // ✅ Android 31+ bắt buộc phải specify Immutable HOẶC Mutable
            // Geofencing cần Mutable để thêm extras
            var flags = PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Mutable;
            
            var pi = PendingIntent.GetBroadcast(_ctx, 0, intent, flags);
            return pi ?? throw new InvalidOperationException("PendingIntent not created");
        }

        public async Task RegisterAsync(IEnumerable<Poi> pois, bool initialTriggerOnEnter = true)
        {
            if (pois == null || !pois.Any()) return; // Kiểm tra rỗng để tránh lỗi
            _poiLookup = pois.ToDictionary(p => p.Id, p => p);
            var builder = new GeofencingRequest.Builder()
                .SetInitialTrigger(initialTriggerOnEnter ? 1 /*ENTER*/ : 4 /*DWELL*/);
            var list = new List<IGeofence>();
            foreach (var poi in pois)
            {
                var gf = new GeofenceBuilder()
                    .SetRequestId(poi.Id)
                    .SetCircularRegion(poi.Latitude, poi.Longitude, poi.RadiusMeters)
                    .SetExpirationDuration(Geofence.NeverExpire)
                    .SetTransitionTypes(
                          Geofence.GeofenceTransitionEnter
                        | Geofence.GeofenceTransitionExit
                        | Geofence.GeofenceTransitionDwell)
                    .SetLoiteringDelay(10_000)
                    .Build();

                list.Add(gf);
            }
            builder.AddGeofences(list);

            // BẮT BUỘC PHẢI CÓ TRY-CATCH NÀY ĐỂ APP KHÔNG BỊ VĂNG NỮA!
            try
            {
                await _client.AddGeofencesAsync(builder.Build(), _pendingIntent);
                System.Diagnostics.Debug.WriteLine("[Geofence] Đăng ký thành công!");
            }
            catch (global::Android.Gms.Common.Apis.ApiException apiEx) // <-- Đã thêm global:: ở đây
            {
                // Máy ảo/Máy thật tắt GPS hoặc chưa cấp quyền "Luôn luôn" sẽ nhảy vào đây thay vì văng app
                System.Diagnostics.Debug.WriteLine($"[Geofence Error] Lỗi API: {apiEx.StatusCode} - {apiEx.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Geofence Error] Lỗi hệ thống: {ex.Message}");
            }
        }
        public Task UnregisterAllAsync()
            => _client.RemoveGeofencesAsync(_pendingIntent);

        private void HandleTransition(string poiId, int transition)
        {
            if (!_poiLookup.TryGetValue(poiId, out var poi)) return;

            var type = transition switch
            {
                Geofence.GeofenceTransitionEnter => "ENTER",
                Geofence.GeofenceTransitionExit => "EXIT",
                Geofence.GeofenceTransitionDwell => "DWELL",
                _ => "UNKNOWN"
            };

            if (type == "UNKNOWN") return;

            if (!GeofenceEventGate.ShouldAccept(poi.Id, type, poi.DebounceSeconds, poi.CooldownSeconds))
                return;

            OnPoiEvent?.Invoke(poi, type);
        }
    }
}
#endif