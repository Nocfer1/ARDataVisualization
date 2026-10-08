using System.Collections.Generic;
using UnityEngine;

public static class TEMSpatialProcessor
{
    /// <summary>
    /// Computes the 3D centroid of all stations and populates their localPosition fields 
    /// mapped to Unity space (X = Easting, Z = Northing, Y = Elevation * verticalScale).
    /// </summary>
    public static List<TEMStationData> NormalizeStationPositions(
        List<TEMStationData> stations, 
        float horizontalScale, 
        float verticalScale)
    {
        if (stations == null || stations.Count == 0)
            return stations;

        // 1. Calculate centroid across all raw survey coordinates
        Vector3 centroid = Vector3.zero;
        foreach (var station in stations)
        {
            centroid += station.worldPosition;
        }
        centroid /= stations.Count;

        // 2. Map survey coordinates to Unity local space relative to centroid
        foreach (var station in stations)
        {
            Vector3 relativePos = station.worldPosition - centroid;

            // Apply horizontalScale to Easting (X) and Northing (Y)
            station.localPosition = new Vector3(
                relativePos.x * horizontalScale,
                relativePos.z * verticalScale,
                relativePos.y * horizontalScale
            );
        }

        return stations;
    }
}