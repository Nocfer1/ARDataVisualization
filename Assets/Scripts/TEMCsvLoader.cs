using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Handles fetching and parsing TEM CSV files from StreamingAssets.
/// </summary>
public class TEMCsvLoader
{
    /// <summary>
    /// Asynchronously loads and parses a CSV file from StreamingAssets.
    /// Does not require MonoBehaviour or StartCoroutine.
    /// </summary>
    public async Task<List<TEMStationData>> LoadCsvAsync(string fileName)
    {
        string path = Path.Combine(Application.streamingAssetsPath, fileName);

        using var request = UnityWebRequest.Get(path);
        
        var operation = request.SendWebRequest();
        var tcs = new TaskCompletionSource<bool>();
        operation.completed += _ => tcs.SetResult(true);
        await tcs.Task;

        if (request.result != UnityWebRequest.Result.Success)
        {
            throw new InvalidOperationException($"Could not load '{fileName}': {request.error}. Ensure it exists in Assets/StreamingAssets.");
        }

        string csv = request.downloadHandler.text.TrimStart('\uFEFF');

        if (!TryParseStations(csv, out List<TEMStationData> stations, out string error))
        {
            throw new FormatException($"Failed to parse '{fileName}': {error}");
        }
        
        return stations;
    }

    private bool TryParseStations(string csv, out List<TEMStationData> stations, out string error)
    {
        stations = new List<TEMStationData>();
        error = "";
        string[] lines = csv.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
        {
            error = "The model CSV is empty or has no data rows.";
            return false;
        }

        string[] headers = SplitCsvLine(lines[0]);
        int stationIndex = Array.IndexOf(headers, "StationNumber");
        int resistivityIndex = Array.IndexOf(headers, "Resistivities");
        int thicknessIndex = Array.IndexOf(headers, "Thicknesses");
        int doiIndex = Array.IndexOf(headers, "DOI");
        int xIndex = Array.IndexOf(headers, "UtmX");
        int yIndex = Array.IndexOf(headers, "UtmY");
        int zIndex = Array.IndexOf(headers, "UtmZ");

        if (stationIndex < 0 || resistivityIndex < 0 || thicknessIndex < 0 || doiIndex < 0 ||
            xIndex < 0 || yIndex < 0 || zIndex < 0)
        {
            error = "CSV requires StationNumber, Resistivities, Thicknesses, DOI, X, Y, and Z columns.";
            return false;
        }

        // Determine the highest column index required to avoid out-of-bounds errors on partial rows
        int maxRequiredIndex = stationIndex;
        maxRequiredIndex = Mathf.Max(maxRequiredIndex, resistivityIndex);
        maxRequiredIndex = Mathf.Max(maxRequiredIndex, thicknessIndex);
        maxRequiredIndex = Mathf.Max(maxRequiredIndex, doiIndex);
        maxRequiredIndex = Mathf.Max(maxRequiredIndex, xIndex);
        maxRequiredIndex = Mathf.Max(maxRequiredIndex, yIndex);
        maxRequiredIndex = Mathf.Max(maxRequiredIndex, zIndex);

        for (int row = 1; row < lines.Length; row++)
        {
            string[] columns = SplitCsvLine(lines[row]);
            if (columns.Length <= maxRequiredIndex) continue;

            if (!int.TryParse(columns[stationIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) ||
                !float.TryParse(columns[doiIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out float doi) ||
                !float.TryParse(columns[xIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(columns[yIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ||
                !float.TryParse(columns[zIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            {
                continue;
            }

            List<float> rho = ParseFloatList(columns[resistivityIndex]);
            List<float> thickness = ParseFloatList(columns[thicknessIndex]);
            if (rho.Count == 0 || thickness.Count == 0) continue;

            stations.Add(new TEMStationData
            {
                stationNumber = number,
                resistivities = rho,
                thicknesses = thickness,
                doi = doi,
                worldPosition = new Vector3(x, y, z)
            });
        }

        if (stations.Count == 0)
        {
            error = "No valid model stations were found in the CSV.";
            return false;
        }

        return true;
    }

    private List<float> ParseFloatList(string text)
    {
        text = text.Trim().Trim('"').Trim('[', ']');
        List<float> values = new List<float>();
        foreach (string part in text.Split(','))
        {
            if (float.TryParse(part.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                values.Add(value);
        }
        return values;
    }

    private string[] SplitCsvLine(string line)
    {
        List<string> fields = new List<string>();
        bool quoted = false;
        StringBuilder field = new StringBuilder();
        foreach (char c in line)
        {
            if (c == '"') quoted = !quoted;
            else if (c == ',' && !quoted)
            {
                fields.Add(field.ToString().Trim());
                field.Clear();
            }
            else field.Append(c);
        }
        fields.Add(field.ToString().Trim());
        return fields.ToArray();
    }
}