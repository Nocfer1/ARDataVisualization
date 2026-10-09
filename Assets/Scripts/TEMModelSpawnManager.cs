using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

public class TEMSpawnManager : MonoBehaviour
{
    [SerializeField] private ObjectSpawner objectSpawner;
    [SerializeField] private XRInteractionManager interactionManager;
    [SerializeField] private XRBaseInteractor screenInteractor;
    
    [Header("Data Settings")]
    [SerializeField] private string csvFileName = "test3.csv";
    [SerializeField] private float horizontalScale = 0.01f;
    
    [Header("Mapbox Settings")]
    [SerializeField] private string mapboxToken;
    [SerializeField] private int utmZone = 32; // Default to 32 for Denmark
    [SerializeField] private bool isNorthernHemisphere = true;

    private GameObject _currentModel;
    private readonly TEMCsvLoader _TEMCsvLoader = new TEMCsvLoader();
    
    private void OnEnable()
    {
        if (objectSpawner != null) objectSpawner.objectSpawned += OnModelSpawned;
    }

    private void OnDisable()
    {
        if (objectSpawner != null) objectSpawner.objectSpawned -= OnModelSpawned;
    }

    private async void OnModelSpawned(GameObject spawnedObject)
    {
        if (_currentModel != null && _currentModel != spawnedObject)
        {
            Destroy(_currentModel);
        }
        _currentModel = spawnedObject;

        try
        {
            if (_currentModel.TryGetComponent<TEMARPlacementController>(out var controller))
            {
                List<TEMStationData> rawStations = await _TEMCsvLoader.LoadCsvAsync(csvFileName);
                
                // 1. Get complete map layout (Bounds, Dimensions, and Offsets)
                TEMSpatialProcessor.MapLayout mapLayout = TEMSpatialProcessor.GetMapLayout(rawStations, 20f);
                
                if (this == null || _currentModel == null || _currentModel != spawnedObject) return;
                
                // 2. Build 3D Model
                List<TEMStationData> processedStations = TEMSpatialProcessor.NormalizeStationPositions(rawStations, horizontalScale);
                controller.BuildModel(processedStations);
                
                // 3. Request Satellite Texture
                string mapUrl = BuildMapboxUrl(mapLayout);
                
                if (!string.IsNullOrEmpty(mapUrl))
                {
                    StartCoroutine(TEMMapboxDownloader.DownloadSatelliteTexture(
                        mapUrl,
                        onSuccess: (texture) => 
                        {
                            controller.AttachMapOverlay(
                                texture, 
                                mapLayout.WidthMeters, 
                                mapLayout.HeightMeters, 
                                mapLayout.OffsetFromCentroid * horizontalScale, // Apply scale to the offset!
                                horizontalScale
                            );
                        },
                        onError: (error) => 
                        {
                            Debug.LogWarning($"[TEM Manager] Proceeding without satellite overlay: {error}");
                        }
                    ));
                }
            }

            if (this == null || _currentModel == null) return;

            if (interactionManager == null)
            {
                interactionManager = FindFirstObjectByType<XRInteractionManager>();
            }

            if (interactionManager != null && screenInteractor != null && _currentModel.TryGetComponent<IXRSelectInteractable>(out var interactable))
            {
                interactionManager.SelectEnter(screenInteractor, interactable);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TEMSpawnManager] Failed to setup spawned model: {ex.Message}", this);
        }
    }

    private string BuildMapboxUrl(TEMSpatialProcessor.MapLayout layout)
    {
        if (string.IsNullOrEmpty(mapboxToken))
        {
            Debug.LogError("Mapbox token is missing!");
            return null;
        }

        // Convert Bottom-Left (Min) and Top-Right (Max) corners to WGS84
        var (swLat, swLon) = UtmToLatLonConverter.UtmToLatLon(layout.PaddedMinX, layout.PaddedMinY, utmZone, isNorthernHemisphere);
        var (neLat, neLon) = UtmToLatLonConverter.UtmToLatLon(layout.PaddedMaxX, layout.PaddedMaxY, utmZone, isNorthernHemisphere);

        // Mapbox Bounding Box Format: [minLon,minLat,maxLon,maxLat]
        return $"https://api.mapbox.com/styles/v1/mapbox/satellite-v9/static/" +
               $"[{swLon:F6},{swLat:F6},{neLon:F6},{neLat:F6}]/" +
               $"512x512@2x?access_token={mapboxToken}";
    }
}