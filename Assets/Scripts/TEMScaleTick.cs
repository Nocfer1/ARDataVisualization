using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Controls individual tick marks and labels on the color scale legend using TextMeshPro.</summary>
public class TEMScaleTick : MonoBehaviour
{
    [SerializeField] private RectTransform lineRect;
    [SerializeField] private TMP_Text labelText; // TextMeshPro reference

    [Header("Mark Length Configuration")]
    [SerializeField] private float labeledMarkLength = 40f;
    [SerializeField] private float unlabeledMarkLength = 25f;

    public void Initialize(string text, bool isLandscape)
    {
        bool hasLabel = !string.IsNullOrEmpty(text);

        if (labelText != null)
        {
            labelText.text = text;
            labelText.gameObject.SetActive(hasLabel);
            
            // Rotate label -90 degrees in landscape mode, and reset to 0 in portrait
            labelText.rectTransform.localRotation = isLandscape ? Quaternion.Euler(0f, 0f, -90f) : Quaternion.identity;
        }

        if (lineRect != null)
        {
            float length = hasLabel ? labeledMarkLength : unlabeledMarkLength;

            // Landscape marks are vertical lines, portrait marks are horizontal lines
            lineRect.sizeDelta = new Vector2(length, isLandscape ? 4f : 3f);
        }
    }
}