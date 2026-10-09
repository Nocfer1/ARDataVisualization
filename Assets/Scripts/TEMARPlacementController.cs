using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Passive presentation component that generates 3D TEM model geometry.
/// Combines all layers of each station into a single GameObject per station using Mesh.CombineMeshes.
/// </summary>
public class TEMARPlacementController : MonoBehaviour
{
    [Header("Map Overlay Config")]
    [SerializeField] private Material unlitMapMaterial; // Assigned in Inspector (URP/Unlit)
    [Header("Station Material")]
    [SerializeField] private Material baseMaterial;
    
    [Header("Model Geometry Config")]
    [SerializeField] private float cylinderDiameter = 0.07f;
    [SerializeField] private float verticalScale = 0.0015f;
    [SerializeField] private float minResistivity = 1f;
    [SerializeField] private float maxResistivity = 1000f;

    private Transform _modelRoot;
    private Mesh _primitiveCylinderMesh;
    private bool _modelReady;
    private float _modelBaseY;
    
    private Texture2D _activeMapTexture; // Tracked to prevent native GPU memory leaks

    public string status { get; private set; } = "Idle";
    public bool isModelReady => _modelReady;
    public float VerticalScale => verticalScale;
    
    private void Awake()
    {
        // Cache Unity's built-in primitive cylinder mesh template once
        GameObject tempCylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _primitiveCylinderMesh = tempCylinder.GetComponent<MeshFilter>().sharedMesh;
        Destroy(tempCylinder);
    }

    /// <summary>
    /// Builds 3D cylindrical station geometry using pre-calculated local positions.
    /// Creates 1 GameObject per station containing a combined layer mesh.
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
        if (_activeMapTexture != null) Destroy(_activeMapTexture);

        _modelRoot = new GameObject("TEM_Model_Root").transform;
        _modelRoot.SetParent(transform, false);

        Bounds modelBounds = new Bounds();
        bool boundsInitialized = false;

        // Build 1 GameObject per station
        foreach (var station in stations)
        {
            float totalDepth = CreateStationGameObject(station);

            Vector3 top = station.localPosition;
            Vector3 bottom = station.localPosition + Vector3.down * (totalDepth * verticalScale);

            if (!boundsInitialized)
            {
                modelBounds = new Bounds(top, Vector3.one * cylinderDiameter);
                modelBounds.Encapsulate(bottom);
                boundsInitialized = true;
            }
            else
            {
                modelBounds.Encapsulate(top);
                modelBounds.Encapsulate(bottom);
            }
        }

        modelBounds.Expand(new Vector3(cylinderDiameter, 0f, cylinderDiameter));

        // Shift root up so lowest geometry point sits flush on top of the AR plane
        float lowestY = modelBounds.min.y;
        
        _modelBaseY = lowestY; // <--- ADD THIS LINE to save the base position

        float heightOffset = -lowestY;
        _modelRoot.localPosition = new Vector3(0f, heightOffset, 0f);

        // Set parent BoxCollider
        SetModelCollider(
            modelBounds.size.x, 
            modelBounds.size.y, 
            modelBounds.size.z, 
            modelBounds.center + new Vector3(0f, heightOffset, 0f)
        );

        status = $"TEM model ready ({stations.Count} stations built).";
        _modelReady = true;
    }

    private float CreateStationGameObject(TEMStationData station)
    {
        GameObject stationGO = new GameObject($"Station_{station.stationNumber}");
        stationGO.transform.SetParent(_modelRoot, false);
        stationGO.transform.localPosition = station.localPosition;

        List<CombineInstance> combineList = new List<CombineInstance>();
        float depth = 0f;
        int layerCount = Mathf.Min(station.thicknesses.Count, station.resistivities.Count);

        for (int layer = 0; layer < layerCount; layer++)
        {
            float thickness = station.thicknesses[layer];
            Color color = ResistivityColor(station.resistivities[layer]);
            
            combineList.Add(CreateLayerCombineInstance(depth, thickness, color));
            depth += thickness;
        }

        if (station.resistivities.Count > station.thicknesses.Count)
        {
            float thickness = Mathf.Max(station.doi - depth, 1f);
            Color color = ResistivityColor(station.resistivities[^1]);
            
            combineList.Add(CreateLayerCombineInstance(depth, thickness, color));
            depth += thickness;
        }

        // Bake layers into a single combined mesh for this station
        Mesh stationMesh = new Mesh { name = $"Station_{station.stationNumber}_Mesh" };
        stationMesh.CombineMeshes(combineList.ToArray(), true, true);

        // Clean up temporary instantiated layer meshes to prevent memory leaks
        foreach (var instance in combineList)
        {
            if (instance.mesh != null) Destroy(instance.mesh);
        }

        MeshFilter mf = stationGO.AddComponent<MeshFilter>();
        MeshRenderer mr = stationGO.AddComponent<MeshRenderer>();
        
        mf.sharedMesh = stationMesh;
        mr.sharedMaterial = baseMaterial;

        return depth;
    }

    private CombineInstance CreateLayerCombineInstance(float depth, float thickness, Color color)
    {
        // 1. Clone primitive cylinder mesh and assign vertex colors
        Mesh layerMesh = Instantiate(_primitiveCylinderMesh);
        Color[] colors = new Color[layerMesh.vertexCount];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = color;
        }
        layerMesh.colors = colors;

        // 2. Compute local transform relative to the station origin
        float yCenter = -(depth + thickness * 0.5f) * verticalScale;
        float scaledHeight = (thickness * verticalScale) * 0.5f; // Primitive cylinder height is 2 units

        Vector3 localPos = new Vector3(0f, yCenter, 0f);
        Vector3 localScale = new Vector3(cylinderDiameter, scaledHeight, cylinderDiameter);
        Matrix4x4 transformMatrix = Matrix4x4.TRS(localPos, Quaternion.identity, localScale);

        return new CombineInstance
        {
            mesh = layerMesh,
            transform = transformMatrix
        };
    }

    private void SetModelCollider(float sizeX, float sizeY, float sizeZ, Vector3 centerOffset)
    {
        BoxCollider modelCollider = GetComponent<BoxCollider>();
        if (modelCollider == null) 
        {
            modelCollider = gameObject.AddComponent<BoxCollider>();
        }
    
        modelCollider.center = centerOffset;
        modelCollider.size = new Vector3(sizeX, sizeY, sizeZ);
    }
    
    /// <summary>
    /// Instantiates and scales a flat satellite map overlay Quad beneath the station geometry.
    /// </summary>
    public void AttachMapOverlay(
        Texture2D mapTexture, 
        float mapWidthMeters, 
        float mapHeightMeters, 
        Vector2 offsetFromCentroid, 
        float horizontalScale)
    {
        if (_modelRoot == null)
        {
            Debug.LogError("Cannot attach map overlay: _modelRoot is null. BuildModel first.", this);
            return;
        }

        if (mapTexture == null)
        {
            Debug.LogError("Cannot attach map overlay: Provided texture is null.", this);
            return;
        }

        _activeMapTexture = mapTexture;

        // 1. Instantiate Quad under _modelRoot
        GameObject mapQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        mapQuad.name = "TEM_Mapbox_Overlay";
        mapQuad.transform.SetParent(_modelRoot, false);

        // 2. Remove MeshCollider so it doesn't block AR raycasts or touch gestures
        Collider quadCollider = mapQuad.GetComponent<Collider>();
        if (quadCollider != null) Destroy(quadCollider);

        // 3. Orient flat on XZ plane
        mapQuad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        // 4. Position at surface baseline (Y = 0) with minor offset to prevent z-fighting
        mapQuad.transform.localPosition = new Vector3(
            offsetFromCentroid.x, 
            _modelBaseY + 0.001f,
            offsetFromCentroid.y
        );

        // 5. Scale Quad to match physical UTM meter bounds * horizontalScale
        mapQuad.transform.localScale = new Vector3(
            mapWidthMeters * horizontalScale,
            mapHeightMeters * horizontalScale,
            1f
        );

        // 6. Apply material and downloaded texture
        MeshRenderer renderer = mapQuad.GetComponent<MeshRenderer>();
        Material instanceMat = new Material(unlitMapMaterial)
        {
            mainTexture = _activeMapTexture
        };
        renderer.sharedMaterial = instanceMat;
    }

    private Color ResistivityColor(float value)
    {
        float min = Mathf.Max(minResistivity, 0.0001f);
        float max = Mathf.Max(maxResistivity, min + 0.0001f);
        float t = Mathf.InverseLerp(Mathf.Log10(min), Mathf.Log10(max), Mathf.Log10(Mathf.Max(value, 0.0001f)));
        return JetPalette.Evaluate(t);
    }
}