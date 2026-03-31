using System;
using System.Collections.Generic;
using System.Text;
using MauiApp1.Models;

namespace MauiApp1.Services;
public interface ILocationService
{
    void StartTracking(Action<double, double> onLocation);
    void StopTracking();
}

public interface IGeofenceService
{
    event Action<Poi, string /*ENTER/EXIT/DWELL*/>? OnPoiEvent;
    Task RegisterAsync(IEnumerable<Poi> pois, bool initialTriggerOnEnter = true);
    Task UnregisterAllAsync();
}
