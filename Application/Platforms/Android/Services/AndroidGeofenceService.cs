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
            // ✅ Guard: nếu chưa có ACCESS_FINE_LOCATION thì không làm gì (tránh crash)
            var perm = ContextCompat.CheckSelfPermission(_ctx, Manifest.Permission.AccessFineLocation);
            if (perm != Permission.Granted)
            {
                System.Diagnostics.Debug.WriteLine("[Geofence] Skip register: missing ACCESS_FINE_LOCATION runtime permission.");
                return;
            }

            _poiLookup = pois.ToDictionary(p => p.Id, p => p);

            var builder = new GeofencingRequest.Builder()
                .SetInitialTrigger(initialTriggerOnEnter
                    ? GeofencingRequest.InitialTriggerEnter
                    : GeofencingRequest.InitialTriggerDwell);

            var list = new List<IGeofence>();

            foreach (var poi in pois)
            {
                var gf = new GeofenceBuilder()
                    .SetRequestId(poi.Id)
                    .SetCircularRegion((float)poi.Latitude, (float)poi.Longitude, poi.RadiusMeters)
                    .SetExpirationDuration(Geofence.NeverExpire)
                    .SetTransitionTypes(
                        Geofence.GeofenceTransitionEnter |
                        Geofence.GeofenceTransitionExit |
                        Geofence.GeofenceTransitionDwell)
                    .SetLoiteringDelay(10_000)
                    .Build();

                list.Add(gf);
            }

            builder.AddGeofences(list);

            try
            {
                await _client.AddGeofencesAsync(builder.Build(), _pendingIntent);
                System.Diagnostics.Debug.WriteLine($"[Geofence] Registered: {list.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Geofence] AddGeofences error: {ex}");
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