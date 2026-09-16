using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

public static class MainMenuInfoPanelBuilder
{
    private const string PrefabPath = "Assets/Externals/GameJam/Views/MainMenuJam.prefab";
    private const string HowToPlayPath = "Assets/Externals/GameJam/Docs/How To Play.txt";
    private const string GddPath = "Assets/Externals/GameJam/Docs/GDD.txt";

    [MenuItem("Tools/Game Jam/Rebuild Main Menu Info Panels")]
    public static void Rebuild()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var existing = root.transform.Find("Info Panels");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            var controller = root.GetComponent<MainMenuInfoPanels>();
            if (controller == null)
                controller = root.AddComponent<MainMenuInfoPanels>();

            var infoRoot = CreateRect("Info Panels", root.transform);
            Stretch(infoRoot);

            var howToPlayButton = CreateButton(
                "How to Play Button", infoRoot, "How to Play",
                new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(24f, 24f), new Vector2(220f, 58f), new Vector2(0f, 0f));

            var gddButton = CreateButton(
                "GDD Button", infoRoot, "GDD",
                new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-24f, 24f), new Vector2(160f, 58f), new Vector2(1f, 0f));

            var overlay = CreateRect("Document Overlay", infoRoot);
            Stretch(overlay);
            var overlayImage = overlay.gameObject.AddComponent<Image>();
            overlayImage.color = new Color(0.025f, 0.035f, 0.05f, 0.96f);

            var panel = CreateRect("Panel", overlay);
            panel.anchorMin = new Vector2(0.04f, 0.04f);
            panel.anchorMax = new Vector2(0.96f, 0.96f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.94f, 0.92f, 0.82f, 1f);

            var title = CreateText("Title", panel, "Document", 38f, FontStyles.Bold);
            title.alignment = TextAlignmentOptions.Center;
            title.color = new Color(0.08f, 0.09f, 0.12f, 1f);
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(80f, -74f), new Vector2(-80f, -16f));

            var closeButton = CreateButton(
                "Close Button", panel, "X",
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-18f, -18f), new Vector2(48f, 48f), new Vector2(1f, 1f));
            closeButton.targetGraphic.GetComponent<Image>().color = new Color(0.65f, 0.16f, 0.14f, 1f);

            var scrollView = CreateRect("Scroll View", panel);
            scrollView.anchorMin = new Vector2(0.035f, 0.04f);
            scrollView.anchorMax = new Vector2(0.965f, 0.86f);
            scrollView.offsetMin = Vector2.zero;
            scrollView.offsetMax = Vector2.zero;
            var scrollBackground = scrollView.gameObject.AddComponent<Image>();
            scrollBackground.color = new Color(1f, 1f, 1f, 0.42f);

            var viewport = CreateRect("Viewport", scrollView);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = new Vector2(0.965f, 1f);
            viewport.offsetMin = new Vector2(12f, 12f);
            viewport.offsetMax = new Vector2(-4f, -12f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var body = CreateText("Read Only Text", viewport, string.Empty, 24f, FontStyles.Normal);
            body.alignment = TextAlignmentOptions.TopLeft;
            body.color = new Color(0.07f, 0.075f, 0.09f, 1f);
            body.raycastTarget = false;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.overflowMode = TextOverflowModes.Overflow;
            body.rectTransform.anchorMin = new Vector2(0f, 1f);
            body.rectTransform.anchorMax = new Vector2(1f, 1f);
            body.rectTransform.pivot = new Vector2(0.5f, 1f);
            body.rectTransform.anchoredPosition = Vector2.zero;
            body.rectTransform.sizeDelta = new Vector2(-24f, 1000f);
            var fitter = body.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollbar = CreateScrollbar(scrollView);

            var scrollRect = scrollView.gameObject.AddComponent<ScrollRect>();
            scrollRect.content = body.rectTransform;
            scrollRect.viewport = viewport;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 48f;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scrollRect.verticalScrollbarSpacing = -2f;

            UnityEventTools.AddPersistentListener(howToPlayButton.onClick, controller.OpenHowToPlay);
            UnityEventTools.AddPersistentListener(gddButton.onClick, controller.OpenGdd);
            UnityEventTools.AddPersistentListener(closeButton.onClick, controller.Close);

            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("overlay").objectReferenceValue = overlay.gameObject;
            serializedController.FindProperty("title").objectReferenceValue = title;
            serializedController.FindProperty("body").objectReferenceValue = body;
            serializedController.FindProperty("scrollRect").objectReferenceValue = scrollRect;
            serializedController.FindProperty("howToPlayText").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<TextAsset>(HowToPlayPath);
            serializedController.FindProperty("gddText").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<TextAsset>(GddPath);
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            overlay.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Rebuilt MainMenuJam information buttons and scroll panel.");
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.layer = 5;
        var rect = (RectTransform)gameObject.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string value,
        float fontSize, FontStyles style)
    {
        var rect = CreateRect(name, parent);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, string label,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Vector2 pivot)
    {
        var rect = CreateRect(name, parent);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.12f, 0.24f, 0.34f, 0.98f);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        var text = CreateText("Label", rect, label, 25f, FontStyles.Bold);
        Stretch(text.rectTransform);
        text.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private static Scrollbar CreateScrollbar(Transform parent)
    {
        var barRect = CreateRect("Scrollbar Vertical", parent);
        barRect.anchorMin = new Vector2(1f, 0f);
        barRect.anchorMax = new Vector2(1f, 1f);
        barRect.pivot = new Vector2(1f, 1f);
        barRect.offsetMin = new Vector2(-22f, 12f);
        barRect.offsetMax = new Vector2(-8f, -12f);
        var background = barRect.gameObject.AddComponent<Image>();
        background.color = new Color(0.08f, 0.09f, 0.12f, 0.2f);

        var slidingArea = CreateRect("Sliding Area", barRect);
        Stretch(slidingArea);
        slidingArea.offsetMin = new Vector2(2f, 2f);
        slidingArea.offsetMax = new Vector2(-2f, -2f);

        var handle = CreateRect("Handle", slidingArea);
        Stretch(handle);
        var handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = new Color(0.12f, 0.24f, 0.34f, 0.9f);

        var scrollbar = barRect.gameObject.AddComponent<Scrollbar>();
        scrollbar.targetGraphic = handleImage;
        scrollbar.handleRect = handle;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        return scrollbar;
    }
}
