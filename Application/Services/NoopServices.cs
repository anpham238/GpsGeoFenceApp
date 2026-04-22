using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using MauiApp1.Models;
namespace MauiApp1.Services;
public class NoopLocationService : ILocationService
{
    public void StartTracking(Action<double, double> onLocation) { }
    public void StopTracking() { }
}

public class NoopGeofenceService : IGeofenceService
{
    public event Action<Poi, string>? OnPoiEvent;
    public Task RegisterAsync(IEnumerable<Poi> pois, bool initialTriggerOnEnter = true) => Task.CompletedTask;
    public Task UnregisterAllAsync() => Task.CompletedTask;
}

public sealed class NoopBackgroundLocationRuntime : IBackgroundLocationRuntime
{
    public Task StartAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken ct = default) => Task.CompletedTask;
}


