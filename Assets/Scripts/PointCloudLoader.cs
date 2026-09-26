using System.Globalization;
using System.IO;
using UnityEngine;

public class PointCloudLoader : MonoBehaviour
{
    public string csvFileName = "unity_points.csv";
    public bool hasHeader = true;
    public GameObject pointPrefab;
    public float pointScale = 0.2f;

    public float minValue = 1f;
    public float maxValue = 100f;

    private void Start()
    {
        ClearChildren();
        LoadCSV();
    }

    void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
    }
    void LoadCSV()
    {
        string path = Path.Combine(Application.streamingAssetsPath, csvFileName);

        if (!File.Exists(path))
        {
            Debug.LogError("CSV not found: " + path);
            return;
        }

        string[] lines = File.ReadAllLines(path);
        int startIndex = hasHeader ? 1 : 0;

        for (int i = startIndex; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            string[] cols = lines[i].Split(',');

            if (cols.Length < 4)
                continue;

            float x = float.Parse(cols[0], CultureInfo.InvariantCulture);
            float y = float.Parse(cols[1], CultureInfo.InvariantCulture);
            float z = float.Parse(cols[2], CultureInfo.InvariantCulture);
            float value = float.Parse(cols[3], CultureInfo.InvariantCulture);

            GameObject point = Instantiate(pointPrefab, new Vector3(x * 0.05f, y * 0.05f, z * 0.05f), Quaternion.identity, transform);
            point.transform.localScale = new Vector3(pointScale, pointScale * 0.5f, pointScale);

            float safeValue = Mathf.Max(value, 0.0001f);
            float safeMin = Mathf.Max(minValue, 0.0001f);
            float safeMax = Mathf.Max(maxValue, safeMin + 0.0001f);

            float logValue = Mathf.Log10(safeValue);
            float logMin = Mathf.Log10(safeMin);
            float logMax = Mathf.Log10(safeMax);

            float t = Mathf.InverseLerp(logMin, logMax, logValue);
            Color color = Color.Lerp(Color.blue, Color.red, t);
            
            Renderer r = point.GetComponent<Renderer>();
            if (r != null)
            {
                Material mat = new Material(r.sharedMaterial);
                mat.color = color;
                r.material = mat;
            }
        }
    }
}