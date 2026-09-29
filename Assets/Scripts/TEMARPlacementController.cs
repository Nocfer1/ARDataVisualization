using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>First AR interaction pass: tap a detected plane to place the demo TEM block model.</summary>
public class TEMARPlacementController : MonoBehaviour
{
    [SerializeField] private ARRaycastManager raycastManager;
    [SerializeField] private Camera arCamera;
    [SerializeField] private int stationCount = 8;
    [SerializeField] private float stationSpacing = 0.045f;
    [SerializeField] private float sectionWidth = 0.12f;
    [SerializeField] private float[] layerThicknesses = { 0.025f, 0.035f, 0.05f, 0.07f };
    [SerializeField] private float[] resistivities = { 25f, 80f, 220f, 600f };
    [SerializeField] private float minResistivity = 10f;
    [SerializeField] private float maxResistivity = 1000f;
    [SerializeField] private float modelScale = 1f;

    private static readonly List<ARRaycastHit> Hits = new List<ARRaycastHit>();
    private Transform modelRoot;
    private bool placed;
    private bool scanStarted;
    private string status = "Pulsa Iniciar y apunta a una mesa.";
    private bool dragging;

    private void Awake()
    {
        if (arCamera == null) arCamera = Camera.main;
        CreateDemoModel();
        modelRoot.position = new Vector3(0f, -100f, 0f);
    }

    private void Update()
    {
        if (!scanStarted) return;

        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                if (!placed)
                {
                    TryPlace(touch.position);
                    return;
                }
                dragging = true;
            }
            else if (touch.phase == TouchPhase.Moved && placed && dragging)
            {
                MoveOnDetectedPlane(touch.position);
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                dragging = false;
            }
        }
        else if (Input.touchCount >= 2 && placed)
        {
            Touch a = Input.GetTouch(0);
            Touch b = Input.GetTouch(1);
            Vector2 deltaA = a.position - a.deltaPosition;
            Vector2 deltaB = b.position - b.deltaPosition;
            float oldDistance = Vector2.Distance(deltaA, deltaB);
            float newDistance = Vector2.Distance(a.position, b.position);
            float oldAngle = Mathf.Atan2(deltaB.y - deltaA.y, deltaB.x - deltaA.x) * Mathf.Rad2Deg;
            float newAngle = Mathf.Atan2(b.position.y - a.position.y, b.position.x - a.position.x) * Mathf.Rad2Deg;

            if (oldDistance > 1f)
                modelRoot.localScale = Vector3.one * Mathf.Clamp(modelRoot.localScale.x * newDistance / oldDistance, 0.35f, 3f);
            modelRoot.Rotate(Vector3.up, Mathf.DeltaAngle(oldAngle, newAngle), Space.World);
            dragging = false;
        }

#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0) && !placed) TryPlace(Input.mousePosition);
#endif
    }

    private void TryPlace(Vector2 screenPosition)
    {
        if (raycastManager == null || !raycastManager.Raycast(screenPosition, Hits, TrackableType.PlaneWithinPolygon))
        {
            status = "Buscando superficie… mueve el móvil lentamente.";
            return;
        }

        Pose pose = Hits[0].pose;
        modelRoot.SetPositionAndRotation(pose.position, pose.rotation);
        modelRoot.localScale = Vector3.one * modelScale;
        placed = true;
        status = "Modelo colocado. Arrastra para mover; pellizca y gira con dos dedos.";
    }

    private void MoveOnDetectedPlane(Vector2 screenPosition)
    {
        if (raycastManager != null && raycastManager.Raycast(screenPosition, Hits, TrackableType.PlaneWithinPolygon))
            modelRoot.position = Hits[0].pose.position;
    }

    private void CreateDemoModel()
    {
        modelRoot = new GameObject("TEM model (synthetic demo)").transform;
        modelRoot.SetParent(transform, false);
        float totalDepth = 0f;
        foreach (float thickness in layerThicknesses) totalDepth += thickness;
        float startX = -((stationCount - 1) * stationSpacing) * 0.5f;

        for (int station = 0; station < stationCount; station++)
        {
            float x = startX + station * stationSpacing;
            float variation = 0.78f + 0.22f * Mathf.Sin(station * 0.9f);
            float y = 0f;
            for (int layer = 0; layer < layerThicknesses.Length; layer++)
            {
                float thickness = layerThicknesses[layer];
                float rho = resistivities[Mathf.Min(layer, resistivities.Length - 1)] * variation;
                GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = $"Station_{station + 1}_Layer_{layer + 1}";
                block.transform.SetParent(modelRoot, false);
                block.transform.localPosition = new Vector3(x, -(y + thickness * 0.5f), 0f);
                block.transform.localScale = new Vector3(stationSpacing * 0.96f, thickness * 0.96f, sectionWidth);
                Renderer renderer = block.GetComponent<Renderer>();
                renderer.material.color = ResistivityColor(rho);
                Collider collider = block.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                y += thickness;
            }
        }
    }

    private Color ResistivityColor(float value)
    {
        float min = Mathf.Max(minResistivity, 0.0001f);
        float max = Mathf.Max(maxResistivity, min + 0.0001f);
        float t = Mathf.InverseLerp(Mathf.Log10(min), Mathf.Log10(max), Mathf.Log10(Mathf.Max(value, 0.0001f)));
        return Color.Lerp(new Color(0.05f, 0.28f, 0.85f), new Color(0.95f, 0.2f, 0.08f), t);
    }

    private void OnGUI()
    {
        float scale = Mathf.Max(1f, Screen.width / 390f);
        GUIStyle label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(15 * scale), wordWrap = true };
        GUIStyle button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(16 * scale) };
        GUI.Box(new Rect(12, 12, Screen.width - 24, 88 * scale), GUIContent.none);
        GUI.Label(new Rect(24, 20, Screen.width - 48, 42 * scale), status, label);

        if (!scanStarted)
        {
            if (GUI.Button(new Rect(24, 54 * scale, Screen.width - 48, 38 * scale), "Iniciar escaneo", button))
            {
                scanStarted = true;
                status = "Apunta a la mesa y mueve el móvil lentamente.";
            }
        }
        else if (placed && GUI.Button(new Rect(24, Screen.height - 64 * scale, Screen.width - 48, 48 * scale), "Quitar modelo", button))
        {
            placed = false;
            status = "Apunta a la mesa y toca para colocar el modelo.";
            modelRoot.position = new Vector3(0f, -100f, 0f);
        }

        GUI.Label(new Rect(16, Screen.height - 32 * scale, Screen.width - 32, 28 * scale),
            "DEMO SINTÉTICA · resistividad en Ω·m · profundidades ilustrativas", label);
    }
}
