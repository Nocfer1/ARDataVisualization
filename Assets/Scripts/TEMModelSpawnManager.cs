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
    [SerializeField] private string csvFileName = "test3.csv";
    
    private GameObject _currentModel;
    private readonly TEMCsvLoader _TEMCsvLoader = new TEMCsvLoader();
    
    private void OnEnable()
    {
        if (objectSpawner != null)
            objectSpawner.objectSpawned += OnModelSpawned;
    }

    private void OnDisable()
    {
        if (objectSpawner != null)
            objectSpawner.objectSpawned -= OnModelSpawned;
    }

    private async void OnModelSpawned(GameObject spawnedObject)
    {
        // 1. EXPLICIT DELETION: Destroy the old model if it exists
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
            
                // Guard against destruction if a new tap occurred during the async load
                if (this == null || _currentModel == null || _currentModel != spawnedObject) return;
                
                List<TEMStationData> processedStations = TEMSpatialProcessor.NormalizeStationPositions(rawStations, 0.01f, controller.VerticalScale);
                controller.BuildModel(processedStations);
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
}