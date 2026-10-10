using UnityEngine;
using UnityEngine.UI;

/// <summary>Transparent, logarithmic resistivity color scale supporting portrait and landscape layouts.</summary>
public class TEMColorScaleLegend : MonoBehaviour
{
    [Header("Portrait References")]
    [SerializeField] private GameObject portraitRoot;
    [SerializeField] private Image portraitGradientImage;
    [SerializeField] private RectTransform portraitTicksContainer;

    [Header("Landscape References")]
    [SerializeField] private GameObject landscapeRoot;
    [SerializeField] private Image landscapeGradientImage;
    [SerializeField] private RectTransform landscapeTicksContainer;

    [Header("Shared Setup")]
    [SerializeField] private GameObject tickTemplatePrefab;
    [SerializeField] private float minResistivity = 1f;
    [SerializeField] private float maxResistivity = 1000f;

    private bool _isLandscape;
    private Texture2D _portraitGradientTexture;
    private Texture2D _landscapeGradientTexture;

    private void Start()
    {
        _isLandscape = Screen.width > Screen.height;

        ApplyOrientationState(_isLandscape);
        BuildGradients();
        BuildAllTicks();
    }

    private void Update()
    {
        bool currentLandscape = Screen.width > Screen.height;
        if (currentLandscape != _isLandscape)
        { 
            _isLandscape = currentLandscape;
            ApplyOrientationState(_isLandscape);
            BuildAllTicks();
        }
    }

    private void ApplyOrientationState(bool landscape)
    {
        if (portraitRoot != null) portraitRoot.SetActive(!landscape);
        if (landscapeRoot != null) landscapeRoot.SetActive(landscape);
    }

    private void BuildGradients()
    {
        if (portraitGradientImage != null)
            _portraitGradientTexture = CreateGradientTexture(portraitGradientImage.rectTransform, false);

        if (landscapeGradientImage != null)
            _landscapeGradientTexture = CreateGradientTexture(landscapeGradientImage.rectTransform, true);
    }

    private Texture2D CreateGradientTexture(RectTransform rectTransform, bool isLandscape)
    {
        int resolution = rectTransform != null 
            ? Mathf.RoundToInt(isLandscape ? rectTransform.rect.width : rectTransform.rect.height) 
            : 256;
        resolution = Mathf.Clamp(resolution, 64, 512);

        int texWidth = isLandscape ? resolution : 1;
        int texHeight = isLandscape ? 1 : resolution;

        Texture2D tex = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] colors = new Color[resolution];
        for (int i = 0; i < resolution; i++)
        {
            float t = i / (float)(resolution - 1);
            colors[i] = JetPalette.Evaluate(t);
        }

        tex.SetPixels(colors);
        tex.Apply();

        Image targetImage = isLandscape ? landscapeGradientImage : portraitGradientImage;
        if (targetImage != null)
        {
            targetImage.sprite = Sprite.Create(tex, new Rect(0, 0, texWidth, texHeight), new Vector2(0.5f, 0.5f));
        }

        return tex;
    }

    private void BuildAllTicks()
    {
        Canvas.ForceUpdateCanvases();

        BuildTicksForContainer(portraitTicksContainer, portraitGradientImage?.rectTransform, false);
        BuildTicksForContainer(landscapeTicksContainer, landscapeGradientImage?.rectTransform, true);
    }

    private void BuildTicksForContainer(RectTransform container, RectTransform gradientRect, bool isLandscape)
    {
        if (container == null || gradientRect == null || tickTemplatePrefab == null) return;

        container.anchorMin = gradientRect.anchorMin;
        container.anchorMax = gradientRect.anchorMax;
        container.anchoredPosition = gradientRect.anchoredPosition;
        container.sizeDelta = gradientRect.sizeDelta;
        container.pivot = gradientRect.pivot;

        foreach (Transform child in container)
        {
            Destroy(child.gameObject);
        }

        float logMin = Mathf.Log10(Mathf.Max(minResistivity, 0.0001f));
        float logMax = Mathf.Log10(Mathf.Max(maxResistivity, minResistivity * 10f));

        for (int decade = Mathf.FloorToInt(logMin); decade <= Mathf.CeilToInt(logMax); decade++)
        {
            float decadeBase = Mathf.Pow(10f, decade);
            for (int factor = 1; factor <= 9; factor++)
            {
                float value = factor * decadeBase;
                if (value < minResistivity || value > maxResistivity) continue;

                float normalizedPos = Mathf.InverseLerp(logMin, logMax, Mathf.Log10(value));

                GameObject tickObj = Instantiate(tickTemplatePrefab, container);
                RectTransform rt = tickObj.GetComponent<RectTransform>();

                if (isLandscape)
                {
                    rt.anchorMin = new Vector2(normalizedPos, 0f);
                    rt.anchorMax = new Vector2(normalizedPos, 1f);
                }
                else
                {
                    rt.anchorMin = new Vector2(0f, normalizedPos);
                    rt.anchorMax = new Vector2(1f, normalizedPos);
                }

                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = Vector2.zero;
                
                // Apply 90-degree rotation to the whole tick object in landscape mode
                rt.localRotation = isLandscape ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;

                bool hasLabel = (factor == 1 || factor == 3);
                string labelText = hasLabel ? (value >= 100f ? value.ToString("0") : value.ToString("0.#")) : string.Empty;

                TEMScaleTick tickScript = tickObj.GetComponent<TEMScaleTick>();
                if (tickScript != null)
                {
                    tickScript.Initialize(labelText, isLandscape);
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (_portraitGradientTexture != null) Destroy(_portraitGradientTexture);
        if (_landscapeGradientTexture != null) Destroy(_landscapeGradientTexture);
    }
}