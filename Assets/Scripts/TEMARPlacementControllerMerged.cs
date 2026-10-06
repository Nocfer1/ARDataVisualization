using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Networking;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>Loads a plotline model and lets the user place it on a detected AR plane.</summary>
public class TEMARPlacementControllerMerged : MonoBehaviour
{
    [SerializeField] private ARRaycastManager raycastManager;
    [SerializeField] private Camera arCamera;
    [Header("Model data")]
    [SerializeField] private bool loadCsvModel = true;
    [SerializeField] private string csvFileName = "anothertest.csv";
    [SerializeField] private bool previewImmediatelyInEditor = true;
    [SerializeField] private float editorPreviewDistance = 0.65f;
    [SerializeField] private int interpolationSteps = 0;
    [SerializeField] private float stationWidth = 0.07f;
    [SerializeField] private float sectionDepth = 0.12f;
    [SerializeField] private float verticalScale = 0.0015f;
    [SerializeField] private float minResistivity = 1f;
    [SerializeField] private float maxResistivity = 1000f;

    [Header("Synthetic fallback model (used when Load Csv Model is off)")]
    [SerializeField] private int stationCount = 8;
    [SerializeField] private float stationSpacing = 0.14f;
    [SerializeField] private float sectionWidth = 0.12f;
    [SerializeField] private float[] layerThicknesses = { 0.025f, 0.035f, 0.05f, 0.07f };
    [SerializeField] private float[] resistivities = { 25f, 80f, 220f, 600f };
    [SerializeField] private float modelScale = 1f;

    private static readonly List<ARRaycastHit> Hits = new List<ARRaycastHit>();
    private Transform modelRoot;
    private bool placed;
    private bool scanStarted;
    private string status = "Loading model and starting AR scan...";
    private bool dragging;
    private bool modelReady;
    private Texture2D resistivityLegendTexture;
#if UNITY_EDITOR
    private bool editorMoveDragging;
    private bool editorRotateDragging;
#endif

    private void Awake()
    {
        if (arCamera == null) arCamera = Camera.main;
#if UNITY_EDITOR
        // Keep the temporary editor preview aligned with the current comparison dataset,
        // even if Unity still has an older serialized scene instance loaded.
        if (previewImmediatelyInEditor)
        {
            csvFileName = "anothertest.csv";
            minResistivity = 1f;
            maxResistivity = 1000f;
        }
#endif
        scanStarted = true;
        resistivityLegendTexture = CreateResistivityLegendTexture();
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

        HandleTouchInput();

#if UNITY_EDITOR
        HandleEditorMouseInput();
#endif
    }

#if UNITY_EDITOR
    private void HandleEditorMouseInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !previewImmediatelyInEditor || !placed || arCamera == null) return;

        if (mouse.leftButton.wasPressedThisFrame) editorMoveDragging = true;
        if (mouse.leftButton.wasReleasedThisFrame) editorMoveDragging = false;
        if (mouse.rightButton.wasPressedThisFrame) editorRotateDragging = true;
        if (mouse.rightButton.wasReleasedThisFrame) editorRotateDragging = false;

        Vector2 delta = mouse.delta.ReadValue();
        if (editorMoveDragging && mouse.leftButton.isPressed)
        {
            float worldHeight = arCamera.orthographic
                ? arCamera.orthographicSize * 2f
                : 2f * editorPreviewDistance * Mathf.Tan(arCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float unitsPerPixel = worldHeight / Mathf.Max(1, Screen.height);
            modelRoot.position += (arCamera.transform.right * delta.x + arCamera.transform.up * delta.y) * unitsPerPixel;
        }

        if (editorRotateDragging && mouse.rightButton.isPressed)
            modelRoot.Rotate(arCamera.transform.up, delta.x * 0.5f, Space.World);
    }
#endif

    private void HandleTouchInput()
    {
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen == null) return;

        TouchControl firstTouch = null;
        TouchControl secondTouch = null;
        foreach (TouchControl touch in touchscreen.touches)
        {
            if (!touch.press.isPressed) continue;
            if (firstTouch == null) firstTouch = touch;
            else
            {
                secondTouch = touch;
                break;
            }
        }

        if (firstTouch == null)
        {
            dragging = false;
            return;
        }

        Vector2 firstPosition = firstTouch.position.ReadValue();
        if (secondTouch != null && placed)
        {
            Vector2 secondPosition = secondTouch.position.ReadValue();
            Vector2 previousFirst = firstPosition - firstTouch.delta.ReadValue();
            Vector2 previousSecond = secondPosition - secondTouch.delta.ReadValue();
            float oldDistance = Vector2.Distance(previousFirst, previousSecond);
            float newDistance = Vector2.Distance(firstPosition, secondPosition);
            float oldAngle = Mathf.Atan2(previousSecond.y - previousFirst.y, previousSecond.x - previousFirst.x) * Mathf.Rad2Deg;
            float newAngle = Mathf.Atan2(secondPosition.y - firstPosition.y, secondPosition.x - firstPosition.x) * Mathf.Rad2Deg;
            if (oldDistance > 1f)
                modelRoot.localScale = Vector3.one * Mathf.Clamp(modelRoot.localScale.x * newDistance / oldDistance, 0.05f, 3f);
            modelRoot.Rotate(Vector3.up, Mathf.DeltaAngle(oldAngle, newAngle), Space.World);
            dragging = false;
            return;
        }

        if (firstTouch.press.wasPressedThisFrame)
        {
            if (!placed && modelReady)
                TryPlace(firstPosition);
            else if (placed)
                dragging = true;
            return;
        }

        if (placed && dragging && firstTouch.delta.ReadValue().sqrMagnitude > 0f)
            MoveOnDetectedPlane(firstPosition);
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
        FinishModelLoad($"Loaded {stations.Count} stations from {csvFileName}.");
    }

    private void FinishModelLoad(string message)
    {
        modelReady = true;
        status = message;
#if UNITY_EDITOR
        if (previewImmediatelyInEditor && arCamera != null)
        {
            Transform cameraTransform = arCamera.transform;
            modelRoot.SetPositionAndRotation(
                cameraTransform.position + cameraTransform.forward * editorPreviewDistance,
                cameraTransform.rotation
            );
            modelRoot.localScale = Vector3.one * modelScale;
            modelRoot.gameObject.SetActive(true);
            placed = true;
            status = $"Editor preview · {message} · left-drag moves, right-drag rotates.";
        }
#endif
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
        float lineCenterOffset = (stations.Count - 1) * stationSpacing * 0.5f;
        for (int i = 0; i < stations.Count - 1; i++)
        {
            DrawCsvStation(stations[i], visualIndex * stationSpacing - lineCenterOffset);
            for (int step = 1; step <= interpolationSteps; step++)
            {
                float t = step / (float)(interpolationSteps + 1);
                DrawCsvStation(Interpolate(stations[i], stations[i + 1], t), (visualIndex + t) * stationSpacing - lineCenterOffset);
            }
            visualIndex++;
        }
        DrawCsvStation(stations[stations.Count - 1], visualIndex * stationSpacing - lineCenterOffset);
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
        float renderedColumnSpacing = stationSpacing / Mathf.Max(1, interpolationSteps + 1);
        float renderedStationWidth = interpolationSteps > 0
            ? Mathf.Min(stationWidth, renderedColumnSpacing * 0.9f)
            : stationWidth;
        block.transform.localScale = new Vector3(renderedStationWidth, thickness * verticalScale, sectionDepth);
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
        return ColorForScale(t);
    }

    private Color ColorForScale(float t)
    {
        return JetPalette.Evaluate(t);
    }

    private Texture2D CreateResistivityLegendTexture()
    {
        Texture2D texture = new Texture2D(256, 1, TextureFormat.RGBA32, false)
        {
            name = "Resistivity color scale",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        for (int x = 0; x < texture.width; x++)
            texture.SetPixel(x, 0, ColorForScale(x / (float)(texture.width - 1)));
        texture.Apply();
        return texture;
    }

    private void OnDestroy()
    {
        if (resistivityLegendTexture != null)
        {
            if (Application.isPlaying) Destroy(resistivityLegendTexture);
            else DestroyImmediate(resistivityLegendTexture);
        }
    }

    private void OnGUI()
    {
        float scale = Mathf.Clamp(Screen.height / 900f, 0.8f, 1.35f);
        GUIStyle label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(14 * scale), wordWrap = true };
        GUIStyle button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(14 * scale) };
        GUI.Box(new Rect(12 * scale, 12 * scale, Screen.width - 24 * scale, 48 * scale), GUIContent.none);
        GUI.Label(new Rect(22 * scale, 20 * scale, Screen.width - 44 * scale, 34 * scale), status, label);

        if (resistivityLegendTexture != null)
        {
            float legendWidth = Mathf.Min(Screen.width * 0.78f, 520f * scale);
            float legendX = (Screen.width - legendWidth) * 0.5f;
            float barY = 88f * scale;
            GUIStyle legendTitle = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(legendX, barY - 23f * scale, legendWidth, 18f * scale), "Resistivity (Ω·m)", legendTitle);
            Rect barRect = new Rect(legendX, barY, legendWidth, 12f * scale);
            GUI.DrawTexture(barRect, resistivityLegendTexture);
            GUIStyle tickLabel = new GUIStyle(label)
            {
                fontSize = Mathf.RoundToInt(10 * scale),
                alignment = TextAnchor.UpperCenter,
                wordWrap = false
            };
            DrawColorScaleTicks(barRect, tickLabel, scale);
        }

        if (placed && GUI.Button(new Rect(Screen.width - 164 * scale, Screen.height - 56 * scale, 148 * scale, 38 * scale), "Remove model", button))
        {
            placed = false;
            status = "Point at a surface and tap to place the model.";
            modelRoot.gameObject.SetActive(false);
        }
    }

    private void DrawColorScaleTicks(Rect barRect, GUIStyle labelStyle, float scale)
    {
        float min = Mathf.Max(minResistivity, 0.0001f);
        float max = Mathf.Max(maxResistivity, min + 0.0001f);
        float logMin = Mathf.Log(min);
        float logMax = Mathf.Log(max);
        int firstDecade = Mathf.FloorToInt(Mathf.Log10(min));
        int lastDecade = Mathf.CeilToInt(Mathf.Log10(max));

        for (int decade = firstDecade; decade <= lastDecade; decade++)
        {
            float decadeBase = Mathf.Pow(10f, decade);
            for (int factor = 1; factor <= 9; factor++)
            {
                float value = factor * decadeBase;
                if (value < min - 0.000001f || value > max + 0.000001f) continue;

                float position = Mathf.Clamp01((Mathf.Log(value) - logMin) / (logMax - logMin));
                float x = barRect.x + position * barRect.width;
                bool hasLabel = factor == 1 || factor == 3;
                float tickHeight = (hasLabel ? 8f : 4f) * scale;
                GUI.DrawTexture(new Rect(x, barRect.yMax, Mathf.Max(1f, scale), tickHeight), Texture2D.whiteTexture);

                if (hasLabel)
                {
                    string text = value >= 1f ? value.ToString("0", CultureInfo.InvariantCulture) : value.ToString("0.0", CultureInfo.InvariantCulture);
                    float labelWidth = 34f * scale;
                    // Center each label on its tick; only clamp to the screen, not to the bar,
                    // so the endpoint labels align with the endpoint ticks instead of shifting inward.
                    float labelX = Mathf.Clamp(x - labelWidth * 0.5f, 0f, Screen.width - labelWidth);
                    GUI.Label(new Rect(labelX, barRect.yMax + 9f * scale, labelWidth, 16f * scale), text, labelStyle);
                }
            }
        }
    }
}

