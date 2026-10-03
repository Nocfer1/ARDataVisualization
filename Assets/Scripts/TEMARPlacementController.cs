using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>Loads a plotline model and lets the user place it on a detected AR plane.</summary>
public class TEMARPlacementController : MonoBehaviour
{
    [SerializeField] private ARRaycastManager raycastManager;
    [SerializeField] private Camera arCamera;
    [Header("Model data")]
    [SerializeField] private bool loadCsvModel = true;
    [SerializeField] private string csvFileName = "test3.csv";
    [SerializeField] private int interpolationSteps = 4;
    [SerializeField] private float stationWidth = 0.04f;
    [SerializeField] private float sectionDepth = 0.12f;
    [SerializeField] private float verticalScale = 0.0015f;
    [SerializeField] private float minResistivity = 1f;
    [SerializeField] private float maxResistivity = 1000f;

    [Header("Synthetic fallback model (used when Load Csv Model is off)")]
    [SerializeField] private int stationCount = 8;
    [SerializeField] private float stationSpacing = 0.045f;
    [SerializeField] private float sectionWidth = 0.12f;
    [SerializeField] private float[] layerThicknesses = { 0.025f, 0.035f, 0.05f, 0.07f };
    [SerializeField] private float[] resistivities = { 25f, 80f, 220f, 600f };
    [SerializeField] private float modelScale = 1f;

    private static readonly List<ARRaycastHit> Hits = new List<ARRaycastHit>();
    private Transform modelRoot;
    private bool placed;
    private bool scanStarted;
    private string status = "Add the model CSV to Assets/StreamingAssets, then start scanning.";
    private bool dragging;
    private bool modelReady;

    private void Awake()
    {
        if (arCamera == null) arCamera = Camera.main;
        modelRoot = new GameObject("TEM model").transform;
        modelRoot.SetParent(transform, false);
        modelRoot.gameObject.SetActive(false);
        if (loadCsvModel)
            StartCoroutine(LoadCsvModel());
        else
        {
            CreateDemoModel();
            FinishModelLoad("Synthetic demo model ready. Tap a detected surface to place it.");
        }
    }

    private void Update()
    {
        if (!scanStarted) return;

        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                if (!placed && modelReady)
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
                modelRoot.localScale = Vector3.one * Mathf.Clamp(modelRoot.localScale.x * newDistance / oldDistance, 0.05f, 3f);
            modelRoot.Rotate(Vector3.up, Mathf.DeltaAngle(oldAngle, newAngle), Space.World);
            dragging = false;
        }

#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0) && !placed) TryPlace(Input.mousePosition);
#endif
    }

    private void TryPlace(Vector2 screenPosition)
    {
        if (!modelReady)
        {
            status = "The model is still loading.";
            return;
        }

        if (raycastManager == null || !raycastManager.Raycast(screenPosition, Hits, TrackableType.PlaneWithinPolygon))
        {
            status = "Looking for a surface… move the phone slowly.";
            return;
        }

        Pose pose = Hits[0].pose;
        modelRoot.gameObject.SetActive(true);
        // Lay the cross-section on the detected surface so its depth axis stays visible.
        modelRoot.SetPositionAndRotation(pose.position, pose.rotation * Quaternion.Euler(90f, 0f, 0f));
        modelRoot.localScale = Vector3.one * modelScale;
        placed = true;
        status = "Model placed. Drag to move; pinch and twist with two fingers.";
    }

    private void MoveOnDetectedPlane(Vector2 screenPosition)
    {
        if (raycastManager != null && raycastManager.Raycast(screenPosition, Hits, TrackableType.PlaneWithinPolygon))
            modelRoot.position = Hits[0].pose.position;
    }

    private void CreateDemoModel()
    {
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

    private System.Collections.IEnumerator LoadCsvModel()
    {
        string path = Path.Combine(Application.streamingAssetsPath, csvFileName);
        string csv;

#if UNITY_ANDROID && !UNITY_EDITOR
        using (UnityWebRequest request = UnityWebRequest.Get(path))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                status = $"Could not load {csvFileName}: {request.error}. Add it to Assets/StreamingAssets.";
                Debug.LogError(status);
                yield break;
            }
            csv = request.downloadHandler.text;
        }
#else
        if (!File.Exists(path))
        {
            status = $"CSV not found: {path}. Add the model file to Assets/StreamingAssets.";
            Debug.LogError(status);
            yield break;
        }
        csv = File.ReadAllText(path);
#endif

        csv = csv.TrimStart('\uFEFF');
        if (!TryParseStations(csv, out List<ModelStation> stations, out string error))
        {
            status = error;
            Debug.LogError(error);
            yield break;
        }

        BuildCsvModel(stations);
        FinishModelLoad($"Loaded {stations.Count} stations from {csvFileName}. Tap a detected surface to place it.");
    }

    private void FinishModelLoad(string message)
    {
        modelReady = true;
        status = message;
    }

    private class ModelStation
    {
        public int number;
        public List<float> resistivities;
        public List<float> thicknesses;
        public float doi;
    }

    private bool TryParseStations(string csv, out List<ModelStation> stations, out string error)
    {
        stations = new List<ModelStation>();
        error = "";
        string[] lines = csv.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
        {
            error = "The model CSV is empty or has no data rows.";
            return false;
        }

        string[] headers = SplitCsvLine(lines[0]);
        int stationIndex = System.Array.IndexOf(headers, "StationNumber");
        int resistivityIndex = System.Array.IndexOf(headers, "Resistivities");
        int thicknessIndex = System.Array.IndexOf(headers, "Thicknesses");
        int doiIndex = System.Array.IndexOf(headers, "DOI");
        if (stationIndex < 0 || resistivityIndex < 0 || thicknessIndex < 0 || doiIndex < 0)
        {
            error = "CSV needs StationNumber, Resistivities, Thicknesses, and DOI columns.";
            return false;
        }

        for (int row = 1; row < lines.Length; row++)
        {
            string[] columns = SplitCsvLine(lines[row]);
            int required = Mathf.Max(Mathf.Max(stationIndex, resistivityIndex), Mathf.Max(thicknessIndex, doiIndex));
            if (columns.Length <= required) continue;
            if (!int.TryParse(columns[stationIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) ||
                !float.TryParse(columns[doiIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out float doi))
                continue;

            List<float> rho = ParseFloatList(columns[resistivityIndex]);
            List<float> thickness = ParseFloatList(columns[thicknessIndex]);
            if (rho.Count == 0 || thickness.Count == 0) continue;
            stations.Add(new ModelStation { number = number, resistivities = rho, thicknesses = thickness, doi = doi });
        }

        if (stations.Count == 0)
        {
            error = "No valid model stations were found in the CSV.";
            return false;
        }
        return true;
    }

    private void BuildCsvModel(List<ModelStation> stations)
    {
        int visualIndex = 0;
        for (int i = 0; i < stations.Count - 1; i++)
        {
            DrawCsvStation(stations[i], visualIndex * stationSpacing);
            for (int step = 1; step <= interpolationSteps; step++)
            {
                float t = step / (float)(interpolationSteps + 1);
                DrawCsvStation(Interpolate(stations[i], stations[i + 1], t), (visualIndex + t) * stationSpacing);
            }
            visualIndex++;
        }
        DrawCsvStation(stations[stations.Count - 1], visualIndex * stationSpacing);
    }

    private ModelStation Interpolate(ModelStation a, ModelStation b, float t)
    {
        ModelStation result = new ModelStation { number = -1, doi = Mathf.Lerp(a.doi, b.doi, t), resistivities = new List<float>(), thicknesses = new List<float>() };
        for (int i = 0; i < Mathf.Min(a.resistivities.Count, b.resistivities.Count); i++)
            result.resistivities.Add(Mathf.Lerp(a.resistivities[i], b.resistivities[i], t));
        for (int i = 0; i < Mathf.Min(a.thicknesses.Count, b.thicknesses.Count); i++)
            result.thicknesses.Add(Mathf.Lerp(a.thicknesses[i], b.thicknesses[i], t));
        return result;
    }

    private void DrawCsvStation(ModelStation station, float x)
    {
        float depth = 0f;
        int layerCount = Mathf.Min(station.thicknesses.Count, station.resistivities.Count);
        for (int layer = 0; layer < layerCount; layer++)
        {
            float thickness = station.thicknesses[layer];
            CreateCsvBlock(x, depth, thickness, station.resistivities[layer], $"Station_{station.number}_Layer_{layer + 1}");
            depth += thickness;
        }
        if (station.resistivities.Count > station.thicknesses.Count)
        {
            float thickness = Mathf.Max(station.doi - depth, 1f);
            CreateCsvBlock(x, depth, thickness, station.resistivities[station.resistivities.Count - 1], $"Station_{station.number}_HalfSpace");
        }
    }

    private void CreateCsvBlock(float x, float depth, float thickness, float resistivity, string objectName)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = objectName;
        block.transform.SetParent(modelRoot, false);
        block.transform.localPosition = new Vector3(x, -(depth + thickness * 0.5f) * verticalScale, 0f);
        block.transform.localScale = new Vector3(stationWidth, thickness * verticalScale, sectionDepth);
        block.GetComponent<Renderer>().material.color = ResistivityColor(resistivity);
        Collider collider = block.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
    }

    private List<float> ParseFloatList(string text)
    {
        text = text.Trim().Trim('"').Trim('[', ']');
        List<float> values = new List<float>();
        foreach (string part in text.Split(','))
            if (float.TryParse(part.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)) values.Add(value);
        return values;
    }

    private string[] SplitCsvLine(string line)
    {
        List<string> fields = new List<string>();
        bool quoted = false;
        System.Text.StringBuilder field = new System.Text.StringBuilder();
        foreach (char c in line)
        {
            if (c == '"') quoted = !quoted;
            if (c == ',' && !quoted)
            {
                fields.Add(field.ToString().Trim());
                field.Clear();
            }
            else field.Append(c);
        }
        fields.Add(field.ToString().Trim());
        return fields.ToArray();
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
            if (GUI.Button(new Rect(24, 54 * scale, Screen.width - 48, 38 * scale), "Start scanning", button))
            {
                scanStarted = true;
                if (modelReady) status = "Point at a surface and move the phone slowly.";
            }
        }
        else if (placed && GUI.Button(new Rect(24, Screen.height - 64 * scale, Screen.width - 48, 48 * scale), "Remove model", button))
        {
            placed = false;
            status = "Point at a surface and tap to place the model.";
            modelRoot.gameObject.SetActive(false);
        }

        GUI.Label(new Rect(16, Screen.height - 32 * scale, Screen.width - 32, 28 * scale),
            loadCsvModel ? "Resistivity (Ω·m) · depth is shown relative to the section" : "SYNTHETIC DEMO · illustrative resistivity and depth", label);
    }
}
