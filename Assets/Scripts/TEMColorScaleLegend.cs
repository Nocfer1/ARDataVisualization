using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Transparent, logarithmic resistivity color scale for the AR view.</summary>
public class TEMColorScaleLegend : MonoBehaviour
{
    [SerializeField] private float minResistivity = 1f;
    [SerializeField] private float maxResistivity = 1000f;

    private RectTransform _panel;
    private RectTransform _bar;
    private RectTransform _ticksRoot;
    private bool _gradientLandscape;
    private List<Tick> _ticks;
    private int _lastWidth;
    private int _lastHeight;
    private float _lastScaleFactor;

    private struct Tick
    {
        public float position;
        public string label;

        public Tick(float position, string label)
        {
            this.position = position;
            this.label = label;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AddToSceneCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        Canvas fallback = null;
        foreach (Canvas canvas in canvases)
        {
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            if (canvas.name == "UI")
            {
                Attach(canvas);
                return;
            }
            if (fallback == null) fallback = canvas;
        }
        if (fallback != null) Attach(fallback);
    }

    private static void Attach(Canvas canvas)
    {
        if (canvas.GetComponent<TEMColorScaleLegend>() == null)
            canvas.gameObject.AddComponent<TEMColorScaleLegend>();
    }

    private void Awake()
    {
        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        EnsureUI();
        _panel = transform.Find("ColorScalePanel") as RectTransform;
        _bar = _panel != null ? _panel.Find("Gradient") as RectTransform : null;
        _ticksRoot = _panel != null ? _panel.Find("Ticks") as RectTransform : null;
        _ticks = BuildLogTicks();
        _gradientLandscape = Screen.width > Screen.height;
        BuildGradient(_gradientLandscape);
        BuildTicks();
        RefreshLayout();
    }

    private void EnsureUI()
    {
        // The layout container has no Image: only the gradient, tick marks and numbers render.
        RectTransform panel = transform.Find("ColorScalePanel") as RectTransform;
        if (panel == null)
            panel = CreateRect("ColorScalePanel", transform);

        Image oldBackground = panel.GetComponent<Image>();
        if (oldBackground != null) Destroy(oldBackground);
        if (panel.Find("Gradient") == null) CreateRect("Gradient", panel);
        if (panel.Find("Ticks") == null) CreateRect("Ticks", panel);
        for (int i = panel.childCount - 1; i >= 0; i--)
        {
            Transform child = panel.GetChild(i);
            if (child.name != "Gradient" && child.name != "Ticks")
                Destroy(child.gameObject);
        }

        Transform oldButton = transform.Find("OrientationSimulatorButton");
        if (oldButton != null) Destroy(oldButton.gameObject);
    }

    private static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child.GetComponent<RectTransform>();
    }

    private void Update()
    {
        CanvasScaler scaler = GetComponent<CanvasScaler>();
        float scaleFactor = scaler != null ? scaler.scaleFactor : 1f;
        if (Screen.width != _lastWidth || Screen.height != _lastHeight ||
            !Mathf.Approximately(scaleFactor, _lastScaleFactor))
            RefreshLayout();
    }

    private void RefreshLayout()
    {
        if (_panel == null) return;

        _lastWidth = Screen.width;
        _lastHeight = Screen.height;
        CanvasScaler scaler = GetComponent<CanvasScaler>();
        float uiScale = scaler != null ? Mathf.Max(0.01f, scaler.scaleFactor) : 1f;
        _lastScaleFactor = uiScale;
        float unit = 1f / uiScale;
        bool portrait = Screen.height >= Screen.width;
        float screenWidth = Screen.width / uiScale;
        float screenHeight = Screen.height / uiScale;
        bool landscape = !portrait;

        // The bar's local vertical axis is rotated in landscape. Reverse segment
        // placement there so the visible scale remains low-to-high from left to right.
        if (_gradientLandscape != landscape)
        {
            _gradientLandscape = landscape;
            BuildGradient(_gradientLandscape);
        }

        _panel.anchorMin = new Vector2(1f, 0.5f);
        _panel.anchorMax = new Vector2(1f, 0.5f);
        _panel.pivot = new Vector2(1f, 0.5f);
        _panel.anchoredPosition = new Vector2(-6f * unit, 0f);
        _panel.sizeDelta = portrait
            ? new Vector2(170f * unit, screenHeight * 0.5f)
            : new Vector2(screenWidth * 0.5f, 150f * unit);

        _bar.anchorMin = _bar.anchorMax = Vector2.zero;
        _bar.pivot = new Vector2(0.5f, 0.5f);
        _ticksRoot.anchorMin = _ticksRoot.anchorMax = Vector2.zero;
        _ticksRoot.pivot = Vector2.zero;
        _ticksRoot.anchoredPosition = Vector2.zero;
        // CreateRectTransform defaults to 100x100. Keep this origin container at
        // zero size so child tick coordinates share the panel's bottom-left origin.
        _ticksRoot.sizeDelta = Vector2.zero;

        float barThickness = 22f * unit;
        if (portrait)
        {
            float barHeight = _panel.sizeDelta.y - 24f * unit;
            float barX = _panel.sizeDelta.x - 12f * unit - barThickness * 0.5f;
            float barY = _panel.sizeDelta.y * 0.5f;
            _bar.anchoredPosition = new Vector2(barX, barY);
            _bar.sizeDelta = new Vector2(barThickness, barHeight);
            _bar.localRotation = Quaternion.identity;
            LayoutVerticalTicks(unit, barX - barThickness * 0.5f, 12f * unit, barHeight);
        }
        else
        {
            float barWidth = _panel.sizeDelta.x - 24f * unit;
            float barX = _panel.sizeDelta.x * 0.5f;
            float barY = 125f * unit;
            _bar.anchoredPosition = new Vector2(barX, barY);
            // The gradient is built bottom-to-top; rotate it so low-to-high reads left-to-right.
            _bar.sizeDelta = new Vector2(barThickness, barWidth);
            _bar.localRotation = Quaternion.Euler(0f, 0f, -90f);
            LayoutHorizontalTicks(unit, barX - barWidth * 0.5f, barY - barThickness * 0.5f, barWidth);
        }
    }

    private void LayoutVerticalTicks(float unit, float tickX, float barBottom, float barHeight)
    {
        for (int i = 0; i < _ticksRoot.childCount; i++)
        {
            RectTransform tickRoot = _ticksRoot.GetChild(i) as RectTransform;
            RectTransform mark = tickRoot.GetChild(0) as RectTransform;
            RectTransform labelRect = tickRoot.GetChild(1) as RectTransform;
            Text label = labelRect.GetComponent<Text>();
            Tick tick = _ticks[i];
            float markLength = (tick.label.Length > 0 ? 12f : 7f) * unit;

            tickRoot.anchoredPosition = new Vector2(tickX, barBottom + tick.position * barHeight);
            mark.anchoredPosition = Vector2.zero;
            mark.sizeDelta = new Vector2(markLength, 2f * unit);
            bool rotateLabel = Screen.width > Screen.height;
            if (rotateLabel)
            {
                labelRect.pivot = new Vector2(0.5f, 0.5f);
                labelRect.anchoredPosition = new Vector2(-(markLength + 2f * unit + 26f * unit), 0f);
                labelRect.sizeDelta = new Vector2(96f * unit, 32f * unit);
                labelRect.localRotation = Quaternion.Euler(0f, 0f, -90f);
                label.alignment = TextAnchor.MiddleCenter;
            }
            else
            {
                mark.pivot = new Vector2(1f, 0.5f);
                labelRect.pivot = new Vector2(1f, 0.5f);
                labelRect.anchoredPosition = new Vector2(-markLength, 0f);
                labelRect.sizeDelta = new Vector2(76f * unit, 32f * unit);
                labelRect.localRotation = Quaternion.identity;
                label.alignment = TextAnchor.MiddleRight;
            }
            label.fontSize = Mathf.RoundToInt(24f * unit);
        }
    }

    private void LayoutHorizontalTicks(float unit, float barLeft, float barY, float barWidth)
    {
        for (int i = 0; i < _ticksRoot.childCount; i++)
        {
            RectTransform tickRoot = _ticksRoot.GetChild(i) as RectTransform;
            RectTransform mark = tickRoot.GetChild(0) as RectTransform;
            RectTransform labelRect = tickRoot.GetChild(1) as RectTransform;
            Text label = labelRect.GetComponent<Text>();
            Tick tick = _ticks[i];
            float markLength = (tick.label.Length > 0 ? 18f : 10f) * unit;

            float horizontalPosition = 1f - tick.position;
            tickRoot.anchoredPosition = new Vector2(barLeft + horizontalPosition * barWidth, barY);
            mark.pivot = new Vector2(0.5f, 1f);
            mark.anchoredPosition = Vector2.zero;
            mark.sizeDelta = new Vector2(2f * unit, markLength);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = new Vector2(0f, -(markLength + 22f * unit + 1f * unit));
            labelRect.sizeDelta = new Vector2(84f * unit, 30f * unit);
            labelRect.localRotation = Quaternion.Euler(0f, 0f, -90f);
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = Mathf.RoundToInt(24f * unit);
        }
    }

    private List<Tick> BuildLogTicks()
    {
        var ticks = new List<Tick>();
        float min = Mathf.Max(minResistivity, 0.0001f);
        float max = Mathf.Max(maxResistivity, min * 10f);
        float logMin = Mathf.Log10(min);
        float logMax = Mathf.Log10(max);
        int startDecade = Mathf.FloorToInt(logMin);
        int endDecade = Mathf.CeilToInt(logMax);

        for (int decade = startDecade; decade <= endDecade; decade++)
        {
            float decadeBase = Mathf.Pow(10f, decade);
            for (int factor = 1; factor <= 9; factor++)
            {
                float value = factor * decadeBase;
                if (value < min || value > max) continue;

                float position = Mathf.InverseLerp(logMin, logMax, Mathf.Log10(value));
                string label = factor == 1 || factor == 3 ? FormatValue(value) : string.Empty;
                ticks.Add(new Tick(position, label));
            }
        }

        return ticks;
    }

    private void BuildGradient(bool landscape)
    {
        if (_bar == null) return;
        for (int i = _bar.childCount - 1; i >= 0; i--)
            Destroy(_bar.GetChild(i).gameObject);
        const int segments = 256;
        for (int i = 0; i < segments; i++)
        {
            GameObject segment = new GameObject("Color_" + i, typeof(RectTransform), typeof(Image));
            RectTransform rect = segment.GetComponent<RectTransform>();
            rect.SetParent(_bar, false);
            int segmentPosition = landscape ? segments - 1 - i : i;
            rect.anchorMin = new Vector2(0f, segmentPosition / (float)segments);
            rect.anchorMax = new Vector2(1f, (segmentPosition + 1f) / segments);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Image image = segment.GetComponent<Image>();
            image.color = JetPalette.Evaluate(i / (float)(segments - 1));
            image.raycastTarget = false;
        }
    }

    private void BuildTicks()
    {
        if (_ticksRoot == null) return;
        for (int i = _ticksRoot.childCount - 1; i >= 0; i--)
            Destroy(_ticksRoot.GetChild(i).gameObject);
        for (int i = 0; i < _ticks.Count; i++)
        {
            RectTransform tickRoot = CreateRect("Tick_" + i, _ticksRoot);
            RectTransform mark = CreateRect("Mark", tickRoot);
            mark.gameObject.AddComponent<Image>().raycastTarget = false;

            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(tickRoot, false);
            Text label = labelObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 16;
            label.color = Color.white;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.text = _ticks[i].label;
            label.enabled = _ticks[i].label.Length > 0;
        }
    }

    private static string FormatValue(float value)
    {
        return value >= 100f ? value.ToString("0") : value.ToString("0.#");
    }
}
