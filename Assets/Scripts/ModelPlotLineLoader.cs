using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public class ModelPlotLineLoader : MonoBehaviour
{
    [Serializable]
    public class StationModel
    {
        public int stationNumber;
        public List<float> resistivities;
        public List<float> thicknesses;
        public float doi;
    }

    [Header("Input")]
    public string csvFileName = "test3.csv";
    public GameObject blockPrefab;
    public bool hasHeader = true;

    [Header("Layout")]
    public float stationSpacing = 3.0f;
    public float stationWidth = 0.4f;
    public float sectionDepth = 1.5f;
    public float verticalScale = 0.08f;

    [Header("Interpolation")]
    public int interpolationSteps = 8;

    [Header("Color Range (log scale)")]
    public float minValue = 1f;
    public float maxValue = 1000f;

    private void Start()
    {
        ClearChildren();
        LoadModelPlot();
    }

    void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
    }

    void LoadModelPlot()
    {
        string path = Path.Combine(Application.streamingAssetsPath, csvFileName);

        if (!File.Exists(path))
        {
            Debug.LogError("CSV not found: " + path);
            return;
        }

        string[] lines = File.ReadAllLines(path);
        if (lines.Length < 2)
        {
            Debug.LogError("CSV is empty or invalid.");
            return;
        }

        string[] headers = SplitCsvLine(lines[0]);

        int stationIdx = Array.IndexOf(headers, "StationNumber");
        int resistivitiesIdx = Array.IndexOf(headers, "Resistivities");
        int thicknessesIdx = Array.IndexOf(headers, "Thicknesses");
        int doiIdx = Array.IndexOf(headers, "DOI");

        if (stationIdx < 0 || resistivitiesIdx < 0 || thicknessesIdx < 0 || doiIdx < 0)
        {
            Debug.LogError("Required columns not found in CSV.");
            return;
        }

        List<StationModel> stations = new List<StationModel>();

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            string[] cols = SplitCsvLine(lines[i]);

            StationModel station = new StationModel
            {
                stationNumber = int.Parse(cols[stationIdx], CultureInfo.InvariantCulture),
                resistivities = ParseFloatList(cols[resistivitiesIdx]),
                thicknesses = ParseFloatList(cols[thicknessesIdx]),
                doi = float.Parse(cols[doiIdx], CultureInfo.InvariantCulture)
            };

            stations.Add(station);
        }

        if (stations.Count == 0)
        {
            Debug.LogError("No stations loaded.");
            return;
        }

        int visualIndex = 0;

        for (int i = 0; i < stations.Count - 1; i++)
        {
            StationModel a = stations[i];
            StationModel b = stations[i + 1];

            // dibujar estación real A
            DrawStation(a, visualIndex * stationSpacing);

            // interpolar entre A y B
            for (int step = 1; step <= interpolationSteps; step++)
            {
                float t = step / (float)(interpolationSteps + 1);
                StationModel interp = InterpolateStations(a, b, t);
                DrawStation(interp, (visualIndex + t) * stationSpacing);
            }

            visualIndex++;
        }

        // dibujar última estación real
        DrawStation(stations[stations.Count - 1], visualIndex * stationSpacing);

        Debug.Log("Interpolated ModelPlot line loaded.");
    }

    void DrawStation(StationModel station, float x)
    {
        float currentDepth = 0f;

        int layerCount = Mathf.Min(station.thicknesses.Count, station.resistivities.Count);

        for (int layer = 0; layer < layerCount; layer++)
        {
            float thickness = station.thicknesses[layer];
            float resistivity = station.resistivities[layer];

            float centerDepth = currentDepth + thickness * 0.5f;

            CreateLayerBlock(
                x,
                centerDepth,
                thickness,
                resistivity,
                $"Station_{station.stationNumber}_Layer_{layer + 1}"
            );

            currentDepth += thickness;
        }

        if (station.resistivities.Count > station.thicknesses.Count)
        {
            float remainingThickness = Mathf.Max(station.doi - currentDepth, 1f);
            float lastResistivity = station.resistivities[station.resistivities.Count - 1];
            float centerDepth = currentDepth + remainingThickness * 0.5f;

            CreateLayerBlock(
                x,
                centerDepth,
                remainingThickness,
                lastResistivity,
                $"Station_{station.stationNumber}_HalfSpace"
            );
        }
    }

    StationModel InterpolateStations(StationModel a, StationModel b, float t)
    {
        StationModel result = new StationModel
        {
            stationNumber = -1,
            resistivities = new List<float>(),
            thicknesses = new List<float>(),
            doi = Mathf.Lerp(a.doi, b.doi, t)
        };

        int resistivityCount = Mathf.Min(a.resistivities.Count, b.resistivities.Count);
        int thicknessCount = Mathf.Min(a.thicknesses.Count, b.thicknesses.Count);

        for (int i = 0; i < resistivityCount; i++)
        {
            float r = Mathf.Lerp(a.resistivities[i], b.resistivities[i], t);
            result.resistivities.Add(r);
        }

        for (int i = 0; i < thicknessCount; i++)
        {
            float th = Mathf.Lerp(a.thicknesses[i], b.thicknesses[i], t);
            result.thicknesses.Add(th);
        }

        return result;
    }

    void CreateLayerBlock(float x, float centerDepth, float thickness, float resistivity, string objectName)
    {
        float y = -(centerDepth * verticalScale);

        GameObject block = Instantiate(
            blockPrefab,
            new Vector3(x, y, 0f),
            Quaternion.identity,
            transform
        );

        block.name = objectName;
        block.transform.localScale = new Vector3(
            stationWidth,
            thickness * verticalScale,
            sectionDepth
        );

        float safeValue = Mathf.Max(resistivity, 0.0001f);
        float safeMin = Mathf.Max(minValue, 0.0001f);
        float safeMax = Mathf.Max(maxValue, safeMin + 0.0001f);

        float logValue = Mathf.Log10(safeValue);
        float logMin = Mathf.Log10(safeMin);
        float logMax = Mathf.Log10(safeMax);

        float t = Mathf.InverseLerp(logMin, logMax, logValue);
        Color color = GetModelPlotColor(t);

        Renderer r = block.GetComponent<Renderer>();
        if (r != null)
        {
            Material mat = new Material(r.sharedMaterial);
            mat.color = color;
            r.material = mat;
        }
    }

    Color GetModelPlotColor(float t)
    {
        t = Mathf.Clamp01(t);

        if (t < 0.15f) return Color.Lerp(new Color(0.03f, 0.18f, 0.52f), new Color(0.08f, 0.35f, 0.72f), t / 0.15f);
        if (t < 0.30f) return Color.Lerp(new Color(0.08f, 0.35f, 0.72f), new Color(0.35f, 0.78f, 0.90f), (t - 0.15f) / 0.15f);
        if (t < 0.50f) return Color.Lerp(new Color(0.35f, 0.78f, 0.90f), new Color(0.32f, 0.82f, 0.38f), (t - 0.30f) / 0.20f);
        if (t < 0.68f) return Color.Lerp(new Color(0.32f, 0.82f, 0.38f), new Color(0.92f, 0.88f, 0.25f), (t - 0.50f) / 0.18f);
        if (t < 0.84f) return Color.Lerp(new Color(0.92f, 0.88f, 0.25f), new Color(0.96f, 0.67f, 0.20f), (t - 0.68f) / 0.16f);
        return Color.Lerp(new Color(0.96f, 0.67f, 0.20f), new Color(0.75f, 0.50f, 0.90f), (t - 0.84f) / 0.16f);
    }

    List<float> ParseFloatList(string text)
    {
        List<float> values = new List<float>();

        text = text.Trim();
        if (text.StartsWith("\"") && text.EndsWith("\""))
            text = text.Substring(1, text.Length - 2);

        text = text.Trim('[', ']');

        if (string.IsNullOrWhiteSpace(text))
            return values;

        string[] parts = text.Split(',');

        foreach (string part in parts)
        {
            if (float.TryParse(part.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    string[] SplitCsvLine(string line)
    {
        List<string> fields = new List<string>();
        bool insideQuotes = false;
        string current = "";

        foreach (char c in line)
        {
            if (c == '"')
            {
                insideQuotes = !insideQuotes;
                current += c;
            }
            else if (c == ',' && !insideQuotes)
            {
                fields.Add(current);
                current = "";
            }
            else
            {
                current += c;
            }
        }

        fields.Add(current);
        return fields.ToArray();
    }
}