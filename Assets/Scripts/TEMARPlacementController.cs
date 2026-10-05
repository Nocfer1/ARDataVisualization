using UnityEngine;

public class TEMARPlacementController : MonoBehaviour
{
    [SerializeField] private bool placeAboveGround = true;
    private Transform _modelRoot;

    // --- CONVENIENCE HELPER FOR NOW ---
    public void CreateDemoModel()
    {
        // 1. Build synthetic demo dataset
        int stations = 8;
        float[] thicknesses = new float[] { 0.02f, 0.035f, 0.05f, 0.08f };
        float[] baseResistivities = new float[] { 15f, 120f, 8f, 450f };

        float[,] resMatrix = new float[stations, thicknesses.Length];
        for (int s = 0; s < stations; s++)
        {
            float variation = 0.78f + 0.22f * Mathf.Sin(s * 0.9f);
            for (int l = 0; l < thicknesses.Length; l++)
            {
                resMatrix[s, l] = baseResistivities[l] * variation;
            }
        }

        TEMData demoData = new TEMData
        {
            stationCount = stations,
            stationSpacing = 0.045f,
            sectionWidth = 0.12f,
            layerThicknesses = thicknesses,
            resistivities = resMatrix
        };

        // 2. Pass data to core renderer
        BuildModel(demoData);
    }

    // --- CORE DUMB RENDERER (Future CSV Compatible) ---
    public void BuildModel(TEMData data)
    {
        if (_modelRoot != null) Destroy(_modelRoot.gameObject);

        GameObject rootObject = new GameObject("TEM_Model_Root");
        _modelRoot = rootObject.transform;
        _modelRoot.SetParent(transform, false);

        int layerCount = data.layerThicknesses.Length;
        float totalDepth = 0f;
        foreach (float t in data.layerThicknesses) totalDepth += t;

        float startX = -((data.stationCount - 1) * data.stationSpacing) * 0.5f;

        for (int station = 0; station < data.stationCount; station++)
        {
            float x = startX + station * data.stationSpacing;
            float yOffset = 0f;

            for (int layer = 0; layer < layerCount; layer++)
            {
                float thickness = data.layerThicknesses[layer];
                float rho = data.resistivities[station, layer];

                GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.transform.SetParent(_modelRoot, false);

                float blockCenterY = placeAboveGround 
                    ? (totalDepth - yOffset) - (thickness * 0.5f) 
                    : -(yOffset + thickness * 0.5f);

                block.transform.localPosition = new Vector3(x, blockCenterY, 0f);
                block.transform.localScale = new Vector3(data.stationSpacing * 0.96f, thickness * 0.96f, data.sectionWidth);

                if (block.TryGetComponent<Collider>(out var col)) Destroy(col);

                Renderer blockRenderer = block.GetComponent<Renderer>();
                if (blockRenderer != null)
                {
                    blockRenderer.material.color = ResistivityColor(rho);
                }

                yOffset += thickness;
            }
        }

        // Generate unified BoxCollider for XRI Selection / Manipulation
        float totalWidth = data.stationCount * data.stationSpacing;
        BoxCollider boxCol = gameObject.GetComponent<BoxCollider>();
        if (boxCol == null) boxCol = gameObject.AddComponent<BoxCollider>();

        float colliderCenterY = placeAboveGround ? (totalDepth * 0.5f) : (-totalDepth * 0.5f);
        boxCol.center = new Vector3(0f, colliderCenterY, 0f);
        boxCol.size = new Vector3(totalWidth, totalDepth, data.sectionWidth);
    }

    private Color ResistivityColor(float rho)
    {
        float logRho = Mathf.Log10(Mathf.Clamp(rho, 1f, 1000f));
        float t = Mathf.InverseLerp(0f, 3f, logRho);
        return Color.HSVToRGB(Mathf.Lerp(0.66f, 0f, t), 0.85f, 0.9f);
    }
}