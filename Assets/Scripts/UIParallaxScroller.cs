using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class UIParallaxScroller : MonoBehaviour
{
    [SerializeField] private RawImage[] layers;
    [SerializeField] private float[] scrollRates = { 0.008f, 0.014f, 0.022f, 0.034f, 0.05f, 0.07f };
    [SerializeField] private float direction = 1f;

    private void OnEnable()
    {
        FitLayersWithoutStretching();
    }

    private void OnRectTransformDimensionsChange()
    {
        FitLayersWithoutStretching();
    }

    private void Update()
    {
        if (layers == null || scrollRates == null)
        {
            return;
        }

        int layerCount = Mathf.Min(layers.Length, scrollRates.Length);
        for (int i = 0; i < layerCount; i++)
        {
            RawImage layer = layers[i];
            if (layer == null || layer.texture == null)
            {
                continue;
            }

            Rect uv = layer.uvRect;
            uv.x = Mathf.Repeat(uv.x + scrollRates[i] * direction * Time.deltaTime, 1f);
            layer.uvRect = uv;
        }
    }

    private void FitLayersWithoutStretching()
    {
        if (layers == null)
        {
            return;
        }

        foreach (RawImage layer in layers)
        {
            if (layer == null || layer.texture == null)
            {
                continue;
            }

            Rect panel = layer.rectTransform.rect;
            if (panel.width <= 0f || panel.height <= 0f)
            {
                continue;
            }

            float panelAspect = panel.width / panel.height;
            float textureAspect = (float)layer.texture.width / layer.texture.height;
            Rect uv = layer.uvRect;
            uv.width = panelAspect / textureAspect;
            uv.height = 1f;
            layer.uvRect = uv;
        }
    }
}
