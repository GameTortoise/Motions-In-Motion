using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lays out four tiled copies of one sprite as an inward-facing picture frame.
/// The vertical copies rotate instead of stretching the artwork vertically.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class PictureFrameBorder : MonoBehaviour
{
    [SerializeField, Min(1f)] private float thickness = 16f;
    [SerializeField] private Image top;
    [SerializeField] private Image bottom;
    [SerializeField] private Image left;
    [SerializeField] private Image right;

    private bool isLayingOut;

    private void OnEnable() => Layout();
    private void OnValidate() => Layout();
    private void OnRectTransformDimensionsChange() => Layout();

    private void Layout()
    {
        if (isLayingOut)
            return;

        isLayingOut = true;
        RectTransform frame = (RectTransform)transform;
        float verticalLength = Mathf.Max(1f, frame.rect.height);

        LayoutHorizontal(top, true);
        LayoutHorizontal(bottom, false);
        LayoutVertical(left, false, verticalLength);
        LayoutVertical(right, true, verticalLength);
        isLayingOut = false;
    }

    private void LayoutHorizontal(Image image, bool atTop)
    {
        if (image == null)
            return;

        ConfigureTiling(image);
        RectTransform rect = image.rectTransform;
        float y = atTop ? 1f : 0f;
        rect.localRotation = Quaternion.identity;
        rect.anchorMin = new Vector2(0f, y);
        rect.anchorMax = new Vector2(1f, y);
        rect.pivot = new Vector2(0.5f, y);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, thickness);
    }

    private void LayoutVertical(Image image, bool atRight, float length)
    {
        if (image == null)
            return;

        ConfigureTiling(image);
        RectTransform rect = image.rectTransform;
        float x = atRight ? 1f : 0f;
        rect.anchorMin = new Vector2(x, 0.5f);
        rect.anchorMax = new Vector2(x, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(length, thickness);
        rect.anchoredPosition = new Vector2(atRight ? -thickness * 0.5f : thickness * 0.5f, 0f);
        rect.localRotation = Quaternion.Euler(0f, 0f, 90f);
    }

    private static void ConfigureTiling(Image image)
    {
        image.type = Image.Type.Tiled;
        image.preserveAspect = false;
        image.raycastTarget = false;
    }
}
