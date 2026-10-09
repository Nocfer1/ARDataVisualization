using System;
using UnityEngine;

public static class UtmToLatLonConverter
{
    // WGS84 Ellipsoid Constants
    private const double a = 6378137.0; // Semi-major axis
    private const double f = 1.0 / 298.257223563; // Flattening
    private const double b = a * (1.0 - f);
    private const double e2 = (a * a - b * b) / (a * a);
    private const double ePrime2 = (a * a - b * b) / (b * b);
    private const double k0 = 0.9996;

    /// <summary>
    /// Converts UTM Easting/Northing to WGS84 Latitude and Longitude.
    /// </summary>
    /// <param name="easting">UTM Easting (X in meters)</param>
    /// <param name="northing">UTM Northing (Y in meters)</param>
    /// <param name="zoneNumber">UTM Zone (e.g. 32)</param>
    /// <param name="isNorthernHemisphere">True if Northern Hemisphere, false if Southern</param>
    public static (double latitude, double longitude) UtmToLatLon(
        double easting, 
        double northing, 
        int zoneNumber, 
        bool isNorthernHemisphere = true)
    {
        double x = easting - 500000.0; // Remove false easting
        double y = isNorthernHemisphere ? northing : northing - 10000000.0; // Remove false northing if southern

        double centralMeridian = (zoneNumber - 1) * 6 - 180 + 3; // Central meridian of the zone in degrees

        double M = y / k0;
        double mu = M / (a * (1.0 - e2 / 4.0 - 3.0 * e2 * e2 / 64.0 - 5.0 * e2 * e2 * e2 / 256.0));

        double e1 = (1.0 - Math.Sqrt(1.0 - e2)) / (1.0 + Math.Sqrt(1.0 - e2));

        double phi1Rad = mu + (3.0 * e1 / 2.0 - 27.0 * Math.Pow(e1, 3) / 32.0) * Math.Sin(2.0 * mu)
                            + (21.0 * e1 * e1 / 16.0 - 55.0 * Math.Pow(e1, 4) / 32.0) * Math.Sin(4.0 * mu)
                            + (151.0 * Math.Pow(e1, 3) / 96.0) * Math.Sin(6.0 * mu);

        double N1 = a / Math.Sqrt(1.0 - e2 * Math.Sin(phi1Rad) * Math.Sin(phi1Rad));
        double T1 = Math.Tan(phi1Rad) * Math.Tan(phi1Rad);
        double C1 = ePrime2 * Math.Cos(phi1Rad) * Math.Cos(phi1Rad);
        double R1 = a * (1.0 - e2) / Math.Pow(1.0 - e2 * Math.Sin(phi1Rad) * Math.Sin(phi1Rad), 1.5);
        double D = x / (N1 * k0);

        double lat = phi1Rad - (N1 * Math.Tan(phi1Rad) / R1) * (
            D * D / 2.0 
            - (5.0 + 3.0 * T1 + 10.0 * C1 - 4.0 * C1 * C1 - 9.0 * ePrime2) * Math.Pow(D, 4) / 24.0
            + (61.0 + 90.0 * T1 + 298.0 * C1 + 45.0 * T1 * T1 - 252.0 * ePrime2 - 3.0 * C1 * C1) * Math.Pow(D, 6) / 720.0
        );

        double lon = (D - (1.0 + 2.0 * T1 + C1) * Math.Pow(D, 3) / 6.0
                      + (5.0 - 2.0 * C1 + 28.0 * T1 - 3.0 * C1 * C1 + 8.0 * ePrime2 + 24.0 * T1 * T1) * Math.Pow(D, 5) / 120.0)
                     / Math.Cos(phi1Rad);

        double latitude = lat * (180.0 / Math.PI);
        double longitude = centralMeridian + lon * (180.0 / Math.PI);

        return (latitude, longitude);
    }
}