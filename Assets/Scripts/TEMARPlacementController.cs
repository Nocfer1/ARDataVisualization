using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Passive presentation component that generates 3D TEM model geometry.
/// Does not perform I/O or automatic initialization on Awake.
/// </summary>
public class TEMARPlacementController : MonoBehaviour
{
    [SerializeField] private Material baseMaterial; // Assign a URP/Unlit material in the Inspector
    
    [Header("Model Geometry Config")]
    [SerializeField] private int interpolationSteps = 0;
    [SerializeField] private float stationSpacing = 0.14f;
    [SerializeField] private float stationWidth = 0.07f;
    [SerializeField] private float sectionDepth = 0.12f;
    [SerializeField] private float verticalScale = 0.0015f;
    [SerializeField] private float minResistivity = 1f;
    [SerializeField] private float maxResistivity = 1000f;

    private MaterialPropertyBlock _propBlock;
    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorID = Shader.PropertyToID("_Color");
    private Transform _modelRoot;
    private bool _modelReady;

    public string status { get; private set; } = "Idle";
    public bool isModelReady => _modelReady;
    
    private void Awake()
    {
        _propBlock = new MaterialPropertyBlock();
    }

    /// <summary>
    /// Generates a synthetic model using fallback inspector values.
    /// </summary>
    public void CreateDemoModel()
    {
        var layerThicknesses = new[] { 0.025f, 0.035f, 0.05f, 0.07f };
        var resistivities = new[] { 25f, 80f, 220f, 600f };
        
        int layerCount = Mathf.Min(layerThicknesses.Length, resistivities.Length);

        List<TEMStationData> demoStations = new List<TEMStationData>();
        for (int station = 0; station < 8; station++)
        {
            float variation = 0.78f + 0.22f * Mathf.Sin(station * 0.9f);
            List<float> stationResistivities = new List<float>();
            List<float> stationThicknesses = new List<float>();

            for (int layer = 0; layer < layerCount; layer++)
            {
                stationResistivities.Add(resistivities[layer] * variation);
                stationThicknesses.Add(layerThicknesses[layer]);
            }

            demoStations.Add(new TEMStationData
            {
                stationNumber = station + 1,
                resistivities = stationResistivities,
                thicknesses = stationThicknesses,
                doi = 0f
            });
        }

        BuildModel(demoStations);
    }
    
    /// <summary>
    /// Builds 3D block geometry from parsed station data.
    /// </summary>
    public void BuildModel(List<TEMStationData> stations)
    {
        if (stations == null || stations.Count == 0)
        {
            status = "Error: Provided station data is null or empty.";
            Debug.LogError(status, this);
            return;
        }

        if (_modelRoot != null) Destroy(_modelRoot.gameObject);
        _modelRoot = new GameObject("TEM_Model_Root").transform;
        _modelRoot.SetParent(transform, false);

        int visualIndex = 0;
        float lineCenterOffset = (stations.Count - 1) * stationSpacing * 0.5f;

        for (int i = 0; i < stations.Count - 1; i++)
        {
            DrawStation(stations[i], visualIndex * stationSpacing - lineCenterOffset);
            for (int step = 1; step <= interpolationSteps; step++)
            {
                float t = step / (float)(interpolationSteps + 1);
                DrawStation(Interpolate(stations[i], stations[i + 1], t), (visualIndex + t) * stationSpacing - lineCenterOffset);
            }
            visualIndex++;
        }
        DrawStation(stations[stations.Count - 1], visualIndex * stationSpacing - lineCenterOffset);

        // Calculate maximum depth for height calculation and offsetting
        float maximumDepth = 0f;
        foreach (TEMStationData station in stations)
        {
            float stationDepth = 0f;
            foreach (float thickness in station.thicknesses)
                stationDepth += thickness;

            if (station.resistivities.Count > station.thicknesses.Count)
                stationDepth += Mathf.Max(station.doi - stationDepth, 1f);

            maximumDepth = Mathf.Max(maximumDepth, stationDepth);
        }

        float columnSpacing = stationSpacing / Mathf.Max(1, interpolationSteps + 1);
        float renderedWidth = interpolationSteps > 0
            ? Mathf.Min(stationWidth, columnSpacing * 0.9f)
            : stationWidth;

        float width = Mathf.Max(renderedWidth, (stations.Count - 1) * stationSpacing + renderedWidth);
        float height = maximumDepth * verticalScale;

        // Shift root up so the bottom layer of the model sits on top of the AR plane
        _modelRoot.localPosition = new Vector3(0f, height, 0f);

        // Position collider bounds to match the shifted geometry
        SetModelCollider(width, height, sectionDepth, height * 0.5f);

        status = "TEM model ready.";
        _modelReady = true;
    }

    private TEMStationData Interpolate(TEMStationData a, TEMStationData b, float t)
    {
        TEMStationData result = new TEMStationData
        {
            stationNumber = -1,
            doi = Mathf.Lerp(a.doi, b.doi, t)
        };

        int resCount = Mathf.Min(a.resistivities.Count, b.resistivities.Count);
        for (int i = 0; i < resCount; i++)
            result.resistivities.Add(Mathf.Lerp(a.resistivities[i], b.resistivities[i], t));

        int thickCount = Mathf.Min(a.thicknesses.Count, b.thicknesses.Count);
        for (int i = 0; i < thickCount; i++)
            result.thicknesses.Add(Mathf.Lerp(a.thicknesses[i], b.thicknesses[i], t));

        return result;
    }

    private void DrawStation(TEMStationData station, float x)
    {
        float depth = 0f;
        int layerCount = Mathf.Min(station.thicknesses.Count, station.resistivities.Count);
        
       Debug.Log($"Layer count is: {layerCount}"); 

        for (int layer = 0; layer < layerCount; layer++)
        {
            float thickness = station.thicknesses[layer];
            CreateBlock(x, depth, thickness, station.resistivities[layer], $"Station_{station.stationNumber}_Layer_{layer + 1}");
            depth += thickness;
        }

        if (station.resistivities.Count > station.thicknesses.Count)
        {
            float thickness = Mathf.Max(station.doi - depth, 1f);
            CreateBlock(x, depth, thickness, station.resistivities[^1], $"Station_{station.stationNumber}_HalfSpace");
        }
    }

    private void CreateBlock(float x, float depth, float thickness, float resistivity, string objectName)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = objectName;
        block.transform.SetParent(_modelRoot, false);
        block.transform.localPosition = new Vector3(x, -(depth + thickness * 0.5f) * verticalScale, 0f);

        float renderedColumnSpacing = stationSpacing / Mathf.Max(1, interpolationSteps + 1);
        float renderedStationWidth = interpolationSteps > 0
            ? Mathf.Min(stationWidth, renderedColumnSpacing * 0.9f)
            : stationWidth;

        block.transform.localScale = new Vector3(renderedStationWidth, thickness * verticalScale, sectionDepth);

        Renderer renderer = block.GetComponent<Renderer>();

        // Overwrite the primitive's default legacy material
        if (baseMaterial != null)
        {
            renderer.sharedMaterial = baseMaterial;
        }

        // Apply color without material instantiation
        Color color = ResistivityColor(resistivity);
        renderer.GetPropertyBlock(_propBlock);
        
        // Target both properties to ensure compatibility with URP or Unlit shaders
        _propBlock.SetColor(BaseColorID, color);
        _propBlock.SetColor(ColorID, color);
        
        renderer.SetPropertyBlock(_propBlock);

        Collider collider = block.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
    }

    private void SetModelCollider(float width, float height, float depth, float centerY)
    {
        BoxCollider modelCollider = GetComponent<BoxCollider>();
        if (modelCollider == null) 
        {
            modelCollider = gameObject.AddComponent<BoxCollider>();
        }
    
        modelCollider.center = new Vector3(0f, centerY, 0f);
        modelCollider.size = new Vector3(width, height, depth);
    }

    private Color ResistivityColor(float value)
    {
        float min = Mathf.Max(minResistivity, 0.0001f);
        float max = Mathf.Max(maxResistivity, min + 0.0001f);
        float t = Mathf.InverseLerp(Mathf.Log10(min), Mathf.Log10(max), Mathf.Log10(Mathf.Max(value, 0.0001f)));
        return JetPalette.Evaluate(t);
    }
}