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
    
    private GameObject _currentModel;
    
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

    private void OnModelSpawned(GameObject spawnedObject)
    {
        _currentModel = spawnedObject;

        // 1. Build the 3D grid and programmatic BoxCollider
        if (_currentModel.TryGetComponent<TEMARPlacementController>(out var controller))
        {
            controller.CreateDemoModel();
        }
        
        Debug.Log($"InteractionManager is null? {interactionManager == null}");

        if (interactionManager == null)
        {
            interactionManager = FindFirstObjectByType<XRInteractionManager>();
            Debug.Log($"InteractionManager is still null? {interactionManager == null}");
        }
        
        if (interactionManager != null && screenInteractor != null && _currentModel.TryGetComponent<IXRSelectInteractable>(out var interactable))
        {
            interactionManager.SelectEnter(screenInteractor, interactable);
        }
        
    }
}