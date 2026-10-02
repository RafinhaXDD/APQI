namespace BookExchange.Domain.Shared;

/// <summary>
/// Location privacy rules (R-14, PLAN Q5): a fixed jittered public point per location, and distances
/// rounded to 0.5 km with "&lt; 1 km" as the floor, so repeated searches can't pin down a home.
/// </summary>
public static class PublicLocation
{
    public const double MinJitterMeters = 300;
    public const double MaxJitterMeters = 700;
    private const double EarthRadiusMeters = 6_371_008.8;

    /// <param name="unitDistance">0..1, chooses the shift between the min and max jitter.</param>
    /// <param name="unitBearing">0..1, chooses the direction.</param>
    public static GeoPoint Jitter(GeoPoint exact, double unitDistance, double unitBearing)
    {
        ArgumentNullException.ThrowIfNull(exact);
        var distance = MinJitterMeters + (Math.Clamp(unitDistance, 0, 1) * (MaxJitterMeters - MinJitterMeters));
        return Offset(exact, distance, Math.Clamp(unitBearing, 0, 1) * 360);
    }

    /// <summary>Destination point given distance and bearing on a sphere.</summary>
    public static GeoPoint Offset(GeoPoint start, double meters, double bearingDegrees)
    {
        ArgumentNullException.ThrowIfNull(start);
        var angular = meters / EarthRadiusMeters;
        var bearing = DegreesToRadians(bearingDegrees);
        var lat1 = DegreesToRadians(start.Latitude);
        var lon1 = DegreesToRadians(start.Longitude);

        var lat2 = Math.Asin((Math.Sin(lat1) * Math.Cos(angular)) + (Math.Cos(lat1) * Math.Sin(angular) * Math.Cos(bearing)));
        var lon2 = lon1 + Math.Atan2(
            Math.Sin(bearing) * Math.Sin(angular) * Math.Cos(lat1),
            Math.Cos(angular) - (Math.Sin(lat1) * Math.Sin(lat2)));

        var longitude = ((RadiansToDegrees(lon2) + 540) % 360) - 180;
        return new GeoPoint(Math.Clamp(RadiansToDegrees(lat2), -90, 90), longitude);
    }

    /// <summary>
    /// Public form of a distance: kilometres rounded to the nearest 0.5. Under 1 km returns
    /// (1, true), meaning "less than 1 km".
    /// </summary>
    public static (double Km, bool IsUpperBound) RoundDistance(double meters)
    {
        if (meters < 1000)
        {
            return (1, true);
        }

        return (Math.Round(meters / 500, MidpointRounding.AwayFromZero) / 2, false);
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;

    private static double RadiansToDegrees(double radians) => radians * 180 / Math.PI;
}
