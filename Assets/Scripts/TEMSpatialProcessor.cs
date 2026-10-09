using System.Collections.Generic;
using UnityEngine;

public static class TEMSpatialProcessor
{
    public struct MapLayout
    {
        public float WidthMeters;
        public float HeightMeters;
        public Vector2 OffsetFromCentroid;
        public float PaddedMinX;
        public float PaddedMinY;
        public float PaddedMaxX;
        public float PaddedMaxY;
    }

    public static List<TEMStationData> NormalizeStationPositions(List<TEMStationData> stations, float horizontalScale)
    {
        if (stations == null || stations.Count == 0) return stations;

        Vector3 centroid = Vector3.zero;
        foreach (var station in stations)
        {
            centroid += station.worldPosition;
        }
        centroid /= stations.Count;

        foreach (var station in stations)
        {
            Vector3 relativePos = station.worldPosition - centroid;
            station.localPosition = new Vector3(
                relativePos.x * horizontalScale,
                0f,
                relativePos.y * horizontalScale
            );
        }

        return stations;
    }

public static MapLayout GetMapLayout(List<TEMStationData> stations, float paddingMeters = 20f)
    {
        if (stations == null || stations.Count == 0) return new MapLayout();

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        Vector2 sumPosition = Vector2.zero;

        foreach (var station in stations)
        {
            if (station.worldPosition.x < minX) minX = station.worldPosition.x;
            if (station.worldPosition.x > maxX) maxX = station.worldPosition.x;
            if (station.worldPosition.y < minY) minY = station.worldPosition.y;
            if (station.worldPosition.y > maxY) maxY = station.worldPosition.y;
            
            sumPosition += new Vector2(station.worldPosition.x, station.worldPosition.y);
        }

        Vector2 averageCentroid = sumPosition / stations.Count;

        // 1. Calculate rectangular bounds first
        float rectMinX = minX - paddingMeters;
        float rectMaxX = maxX + paddingMeters;
        float rectMinY = minY - paddingMeters;
        float rectMaxY = maxY + paddingMeters;

        float rectWidth = rectMaxX - rectMinX;
        float rectHeight = rectMaxY - rectMinY;

        // 2. FORCE SQUARE: Mapbox 512x512 represents a square geographical area.
        // Take the largest dimension to ensure all stations fit.
        float maxDim = Mathf.Max(rectWidth, rectHeight);
        
        float centerX = (rectMinX + rectMaxX) * 0.5f;
        float centerY = (rectMinY + rectMaxY) * 0.5f;

        float squareMinX = centerX - (maxDim * 0.5f);
        float squareMaxX = centerX + (maxDim * 0.5f);
        float squareMinY = centerY - (maxDim * 0.5f);
        float squareMaxY = centerY + (maxDim * 0.5f);

        Vector2 squareBoxCenter = new Vector2(centerX, centerY);

        return new MapLayout
        {
            WidthMeters = maxDim, // Both width and height are now maxDim
            HeightMeters = maxDim, 
            OffsetFromCentroid = squareBoxCenter - averageCentroid,
            PaddedMinX = squareMinX,
            PaddedMinY = squareMinY,
            PaddedMaxX = squareMaxX,
            PaddedMaxY = squareMaxY
        };
    }
}