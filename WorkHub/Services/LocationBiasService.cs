namespace WorkHub.Services;

/// <summary>
/// Fetches the device's coarse location once per session and snaps it to a 0.1° grid
/// (~11 km cells) before any caller sees it. The API only ever receives the rounded
/// center, never the precise device coordinates.
/// </summary>
public class LocationBiasService
{
    // ~11 km grid at the equator; coarser near the poles.
    private const double GridDegrees = 0.1;
    private const double BiasRadiusMeters = 50_000;

    private (double Lat, double Lng)? _cachedCenter;
    private bool _attempted;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public double RadiusMeters => BiasRadiusMeters;

    public async Task<(double Lat, double Lng)?> GetCenterAsync()
    {
        if (_attempted) return _cachedCenter;

        await _lock.WaitAsync();
        try
        {
            if (_attempted) return _cachedCenter;
            _attempted = true;

            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted) return null;
            }

            // Coarse accuracy is plenty — we round to a ~11 km grid anyway.
            var request = new GeolocationRequest(GeolocationAccuracy.Lowest, TimeSpan.FromSeconds(8));
            var location = await Geolocation.Default.GetLastKnownLocationAsync()
                ?? await Geolocation.Default.GetLocationAsync(request);
            if (location is null) return null;

            _cachedCenter = (
                Math.Round(location.Latitude / GridDegrees) * GridDegrees,
                Math.Round(location.Longitude / GridDegrees) * GridDegrees);
            return _cachedCenter;
        }
        catch
        {
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }
}
