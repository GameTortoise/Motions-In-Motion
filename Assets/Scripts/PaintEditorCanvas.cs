using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))]
public sealed class PaintEditorCanvas : MonoBehaviour
{
    private enum PaintTool
    {
        Pen,
        Marker,
        Eraser
    }

    [Header("Canvas")]
    [SerializeField, Min(128)] private int textureWidth = 1280;
    [SerializeField, Min(128)] private int textureHeight = 720;
    [SerializeField] private bool startOpen;
    [SerializeField] private bool blackPenOnly;

    [Header("Default brush sizes")]
    [SerializeField, Range(2, 64)] private int penSize = 8;
    [SerializeField, Range(2, 64)] private int markerSize = 32;
    [SerializeField, Range(2, 64)] private int eraserSize = 24;

    [Header("World drawing")]
    [SerializeField] private Vector3 worldSpawnPosition = Vector3.zero;
    [SerializeField, Min(1f)] private float worldPixelsPerUnit = 100f;
    [SerializeField] private int worldSortingOrder = 10;

    private Canvas rootCanvas;
    private GraphicRaycaster raycaster;
    private RectTransform drawingRect;
    private RawImage markerLayer;
    private RawImage penLayer;
    private Texture2D markerTexture;
    private Texture2D penTexture;
    private Color32[] markerPixels;
    private Color32[] penPixels;
    private Slider sizeSlider;
    private Text sizeLabel;
    private Text toolLabel;

    private PaintTool activeTool = PaintTool.Pen;
    private Color32 activeColor = new(0, 0, 0, 255);
    private Vector2Int previousPixel;
    private bool drawing;
    private readonly List<List<PixelChange>> undoHistory = new();
    private List<PixelChange> currentStroke;
    private HashSet<int> currentStrokeIndices;

    private static readonly Color32 Transparent = new(0, 0, 0, 0);
    private static readonly Color32 MarkerAlpha = new(255, 255, 255, 90);

    private readonly struct PixelChange
    {
        public readonly int Index;
        public readonly Color32 Marker;
        public readonly Color32 Pen;

        public PixelChange(int index, Color32 marker, Color32 pen)
        {
            Index = index;
            Marker = marker;
            Pen = pen;
        }
    }

    private void Awake()
    {
        rootCanvas = GetComponent<Canvas>();
        raycaster = GetComponent<GraphicRaycaster>();
        BuildInterface();
        CreateDrawingTextures();
        SetOpen(startOpen);
        SelectTool(PaintTool.Pen);
    }

    private void OnDestroy()
    {
        if (markerTexture != null)
            Destroy(markerTexture);
        if (penTexture != null)
            Destroy(penTexture);
    }

    private void OnGUI()
    {
        var label = rootCanvas != null && rootCanvas.enabled ? "Close Paint" : "Open Paint";
        if (GUI.Button(new Rect(10f, 10f, 96f, 30f), label))
            SetOpen(rootCanvas == null || !rootCanvas.enabled);
    }

    private void Update()
    {
        if (rootCanvas == null || !rootCanvas.enabled || drawingRect == null || Mouse.current == null)
            return;

        var mouse = Mouse.current;
        var screenPoint = mouse.position.ReadValue();
        var inside = TryGetPixel(screenPoint, out var pixel);

        if (mouse.leftButton.wasPressedThisFrame && inside)
        {
            BeginStroke();
            drawing = true;
            previousPixel = pixel;
            DrawLine(pixel, pixel);
        }
        else if (drawing && mouse.leftButton.isPressed && inside)
        {
            DrawLine(previousPixel, pixel);
            previousPixel = pixel;
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            drawing = false;
            FinishStroke();
        }
    }

    public void SetOpen(bool open)
    {
        if (rootCanvas == null)
            rootCanvas = GetComponent<Canvas>();
        if (raycaster == null)
            raycaster = GetComponent<GraphicRaycaster>();

        rootCanvas.enabled = open;
        raycaster.enabled = open;
        if (drawing)
            FinishStroke();
        drawing = false;
    }

    private void BuildInterface()
    {
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = 500;

        var scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var backdrop = CreateImage("Paint Editor", transform, new Color32(24, 27, 34, 255));
        Stretch(backdrop.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var toolbar = CreateImage("Toolbar", backdrop.transform, new Color32(42, 47, 58, 255));
        Stretch(toolbar.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -76f), Vector2.zero);
        var layout = toolbar.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 10, 10);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        CreateButton("Back", toolbar.transform, new Color32(75, 82, 99, 255), UndoLastStroke, 70f);
        if (!blackPenOnly)
        {
            CreateButton("Pen", toolbar.transform, new Color32(75, 82, 99, 255), () => SelectTool(PaintTool.Pen), 62f);
            CreateButton("Marker", toolbar.transform, new Color32(75, 82, 99, 255), () => SelectTool(PaintTool.Marker), 78f);
            CreateButton("Eraser", toolbar.transform, new Color32(75, 82, 99, 255), () => SelectTool(PaintTool.Eraser), 76f);

            AddSpacer(toolbar.transform, 8f);
            AddColorButton(toolbar.transform, "Black", Color.black);
            AddColorButton(toolbar.transform, "Red", Color.red);
            AddColorButton(toolbar.transform, "Blue", new Color32(30, 110, 255, 255));
            AddColorButton(toolbar.transform, "Green", new Color32(20, 185, 80, 255));
            AddColorButton(toolbar.transform, "Yellow", Color.yellow);
            AddColorButton(toolbar.transform, "Purple", new Color32(155, 80, 220, 255));
            AddColorButton(toolbar.transform, "Orange", new Color32(255, 140, 25, 255));
            AddSpacer(toolbar.transform, 8f);
        }
        else
        {
            CreateButton("Make Object", toolbar.transform, new Color32(46, 125, 88, 255),
                () => CreateWorldDrawingGameObject(), 112f);
            AddSpacer(toolbar.transform, 8f);
        }

        sizeLabel = CreateLabel("Size: 8", toolbar.transform, 64f);
        sizeSlider = CreateSlider(toolbar.transform);
        sizeSlider.onValueChanged.AddListener(OnSizeChanged);
        toolLabel = CreateLabel(blackPenOnly ? "Black Pen" : "Pen", toolbar.transform, blackPenOnly ? 100f : 70f);

        var surfaceFrame = CreateImage("Drawing Surface", backdrop.transform, new Color32(213, 216, 222, 255));
        Stretch(surfaceFrame.rectTransform, Vector2.zero, Vector2.one, new Vector2(14f, 14f), new Vector2(-14f, -90f));

        var paper = CreateImage("Paper", surfaceFrame.transform, Color.white);
        Stretch(paper.rectTransform, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
        drawingRect = paper.rectTransform;

        markerLayer = CreateRawImage("Marker Layer", paper.transform);
        Stretch(markerLayer.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        penLayer = CreateRawImage("Pen Layer", paper.transform);
        Stretch(penLayer.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    private void CreateDrawingTextures()
    {
        markerPixels = CreateTransparentPixels();
        penPixels = CreateTransparentPixels();
        markerTexture = CreateTransparentTexture("Paint Marker Layer", markerPixels);
        penTexture = CreateTransparentTexture("Paint Pen Layer", penPixels);
        markerLayer.texture = markerTexture;
        penLayer.texture = penTexture;
    }

    private Color32[] CreateTransparentPixels()
    {
        var pixels = new Color32[textureWidth * textureHeight];
        Array.Fill(pixels, Transparent);
        return pixels;
    }

    private Texture2D CreateTransparentTexture(string textureName, Color32[] pixels)
    {
        var texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false)
        {
            name = textureName,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(pixels);
        texture.Apply(false);
        return texture;
    }

    private bool TryGetPixel(Vector2 screenPoint, out Vector2Int pixel)
    {
        pixel = default;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(drawingRect, screenPoint, null, out var local))
            return false;

        var rect = drawingRect.rect;
        if (!rect.Contains(local))
            return false;

        var u = Mathf.InverseLerp(rect.xMin, rect.xMax, local.x);
        var v = Mathf.InverseLerp(rect.yMin, rect.yMax, local.y);
        pixel = new Vector2Int(
            Mathf.Clamp(Mathf.RoundToInt(u * (textureWidth - 1)), 0, textureWidth - 1),
            Mathf.Clamp(Mathf.RoundToInt(v * (textureHeight - 1)), 0, textureHeight - 1));
        return true;
    }

    private void DrawLine(Vector2Int from, Vector2Int to)
    {
        var distance = Mathf.Max(Mathf.Abs(to.x - from.x), Mathf.Abs(to.y - from.y));
        var steps = Mathf.Max(1, distance);
        for (var i = 0; i <= steps; i++)
        {
            var t = i / (float)steps;
            Stamp(Vector2Int.RoundToInt(Vector2.Lerp(from, to, t)));
        }

        ApplyPixels();
    }

    private void Stamp(Vector2Int center)
    {
        var size = GetCurrentSize();
        var radius = Mathf.Max(1, size / 2);
        var radiusSquared = radius * radius;
        for (var y = -radius; y <= radius; y++)
        for (var x = -radius; x <= radius; x++)
        {
            if (x * x + y * y > radiusSquared)
                continue;

            var px = center.x + x;
            var py = center.y + y;
            if (px < 0 || py < 0 || px >= textureWidth || py >= textureHeight)
                continue;

            var index = py * textureWidth + px;
            RecordPixelBeforeChange(index);

            if (activeTool == PaintTool.Eraser)
            {
                markerPixels[index] = Transparent;
                penPixels[index] = Transparent;
            }
            else if (activeTool == PaintTool.Marker)
            {
                var markerColor = activeColor;
                markerColor.a = MarkerAlpha.a;
                markerPixels[index] = markerColor;
            }
            else
            {
                penPixels[index] = activeColor;
            }
        }
    }

    private void BeginStroke()
    {
        currentStroke = new List<PixelChange>();
        currentStrokeIndices = new HashSet<int>();
    }

    private void RecordPixelBeforeChange(int index)
    {
        if (currentStrokeIndices == null || !currentStrokeIndices.Add(index))
            return;

        currentStroke.Add(new PixelChange(index, markerPixels[index], penPixels[index]));
    }

    private void FinishStroke()
    {
        if (currentStroke is { Count: > 0 })
        {
            undoHistory.Add(currentStroke);
            if (undoHistory.Count > 30)
                undoHistory.RemoveAt(0);
        }

        currentStroke = null;
        currentStrokeIndices = null;
    }

    private void UndoLastStroke()
    {
        if (drawing)
        {
            drawing = false;
            FinishStroke();
        }

        if (undoHistory.Count == 0)
            return;

        var lastIndex = undoHistory.Count - 1;
        var stroke = undoHistory[lastIndex];
        undoHistory.RemoveAt(lastIndex);
        foreach (var change in stroke)
        {
            markerPixels[change.Index] = change.Marker;
            penPixels[change.Index] = change.Pen;
        }
        ApplyPixels();
    }

    private void ApplyPixels()
    {
        markerTexture.SetPixels32(markerPixels);
        penTexture.SetPixels32(penPixels);
        markerTexture.Apply(false);
        penTexture.Apply(false);
    }

    /// <summary>
    /// Creates the drawn black-pen layer as a new world-space GameObject.
    /// Other scripts can call this method directly and use the returned object.
    /// </summary>
    public GameObject CreateWorldDrawingGameObject()
    {
        if (!TryGetVisiblePenBounds(out var penBounds))
        {
            Debug.LogWarning("Draw something before creating a world object.", this);
            return null;
        }

        // This is the point where the world GameObject is created.
        var worldDrawing = new GameObject("Black Pen Drawing");
        worldDrawing.transform.position = worldSpawnPosition;

        const int transparentPadding = 2;
        var croppedWidth = penBounds.width + transparentPadding * 2;
        var croppedHeight = penBounds.height + transparentPadding * 2;
        var croppedPixels = new Color32[croppedWidth * croppedHeight];
        Array.Fill(croppedPixels, Transparent);
        for (var y = 0; y < penBounds.height; y++)
        for (var x = 0; x < penBounds.width; x++)
        {
            var sourceIndex = (penBounds.yMin + y) * textureWidth + penBounds.xMin + x;
            var destinationIndex = (y + transparentPadding) * croppedWidth + x + transparentPadding;
            croppedPixels[destinationIndex] = penPixels[sourceIndex];
        }

        var worldTexture = new Texture2D(croppedWidth, croppedHeight, TextureFormat.RGBA32, false)
        {
            name = "Black Pen Drawing Texture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        worldTexture.SetPixels32(croppedPixels);
        worldTexture.Apply(false);

        var sprite = Sprite.Create(
            worldTexture,
            new Rect(0f, 0f, croppedWidth, croppedHeight),
            new Vector2(0.5f, 0.5f),
            worldPixelsPerUnit,
            0,
            SpriteMeshType.Tight,
            Vector4.zero,
            true);
        sprite.name = "Black Pen Drawing Sprite";

        var spriteRenderer = worldDrawing.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        spriteRenderer.sortingOrder = worldSortingOrder;

        var polygonCollider = worldDrawing.AddComponent<PolygonCollider2D>();
        CopySpritePhysicsShape(sprite, polygonCollider);
        worldDrawing.AddComponent<Rigidbody2D>();

        return worldDrawing;
    }

    private bool TryGetVisiblePenBounds(out RectInt bounds)
    {
        bounds = default;
        if (penPixels == null || penPixels.Length == 0)
            return false;

        var minX = textureWidth;
        var minY = textureHeight;
        var maxX = -1;
        var maxY = -1;
        for (var y = 0; y < textureHeight; y++)
        for (var x = 0; x < textureWidth; x++)
        {
            if (penPixels[y * textureWidth + x].a == 0)
                continue;

            minX = Mathf.Min(minX, x);
            minY = Mathf.Min(minY, y);
            maxX = Mathf.Max(maxX, x);
            maxY = Mathf.Max(maxY, y);
        }

        if (maxX < minX || maxY < minY)
            return false;

        bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        return true;
    }

    private static void CopySpritePhysicsShape(Sprite sprite, PolygonCollider2D polygonCollider)
    {
        var shapeCount = sprite.GetPhysicsShapeCount();
        if (shapeCount == 0)
        {
            polygonCollider.enabled = false;
            Debug.LogWarning("Unity could not generate a polygon outline for this drawing.", polygonCollider);
            return;
        }

        polygonCollider.pathCount = shapeCount;
        var points = new List<Vector2>();
        for (var pathIndex = 0; pathIndex < shapeCount; pathIndex++)
        {
            points.Clear();
            sprite.GetPhysicsShape(pathIndex, points);
            polygonCollider.SetPath(pathIndex, points);
        }
    }

    private void SelectTool(PaintTool tool)
    {
        activeTool = tool;
        if (toolLabel != null)
            toolLabel.text = blackPenOnly ? "Black Pen" : tool.ToString();
        if (sizeSlider != null)
            sizeSlider.SetValueWithoutNotify(GetCurrentSize());
        UpdateSizeLabel();
    }

    private void SelectColor(Color32 color)
    {
        activeColor = color;
        if (activeTool == PaintTool.Eraser)
            SelectTool(PaintTool.Pen);
    }

    private void OnSizeChanged(float value)
    {
        var size = Mathf.RoundToInt(value);
        switch (activeTool)
        {
            case PaintTool.Marker: markerSize = size; break;
            case PaintTool.Eraser: eraserSize = size; break;
            default: penSize = size; break;
        }
        UpdateSizeLabel();
    }

    private int GetCurrentSize() => activeTool switch
    {
        PaintTool.Marker => markerSize,
        PaintTool.Eraser => eraserSize,
        _ => penSize
    };

    private void UpdateSizeLabel()
    {
        if (sizeLabel != null)
            sizeLabel.text = $"Size: {GetCurrentSize()}";
    }

    private void AddColorButton(Transform parent, string label, Color32 color)
    {
        var textColor = color.r + color.g + color.b > 500 ? Color.black : Color.white;
        CreateButton(label, parent, color, () => SelectColor(color), 64f, textColor);
    }

    private static Button CreateButton(string label, Transform parent, Color color, UnityEngine.Events.UnityAction action,
        float width, Color? textColor = null)
    {
        var image = CreateImage(label, parent, color);
        image.gameObject.AddComponent<LayoutElement>().preferredWidth = width;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        var text = CreateText(label, image.transform, textColor ?? Color.white, 15);
        Stretch(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return button;
    }

    private static Text CreateLabel(string label, Transform parent, float width)
    {
        var text = CreateText(label, parent, Color.white, 15);
        text.alignment = TextAnchor.MiddleCenter;
        text.gameObject.AddComponent<LayoutElement>().preferredWidth = width;
        return text;
    }

    private static Slider CreateSlider(Transform parent)
    {
        var root = new GameObject("Brush Size", typeof(RectTransform), typeof(LayoutElement), typeof(Slider));
        root.transform.SetParent(parent, false);
        root.GetComponent<LayoutElement>().preferredWidth = 170f;

        var background = CreateImage("Background", root.transform, new Color32(20, 23, 29, 255));
        Stretch(background.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -4f), new Vector2(0f, 4f));
        var fill = CreateImage("Fill", background.transform, new Color32(83, 155, 255, 255));
        Stretch(fill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var handle = CreateImage("Handle", root.transform, Color.white);
        handle.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        handle.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        handle.rectTransform.sizeDelta = new Vector2(18f, 28f);

        var slider = root.GetComponent<Slider>();
        slider.minValue = 2f;
        slider.maxValue = 64f;
        slider.wholeNumbers = true;
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    private static void AddSpacer(Transform parent, float width)
    {
        var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(parent, false);
        spacer.GetComponent<LayoutElement>().preferredWidth = width;
    }

    private static Image CreateImage(string objectName, Transform parent, Color color)
    {
        var gameObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        gameObject.transform.SetParent(parent, false);
        var image = gameObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static RawImage CreateRawImage(string objectName, Transform parent)
    {
        var gameObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        gameObject.transform.SetParent(parent, false);
        var image = gameObject.GetComponent<RawImage>();
        image.color = Color.white;
        image.raycastTarget = false;
        return image;
    }

    private static Text CreateText(string value, Transform parent, Color color, int fontSize)
    {
        var gameObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        gameObject.transform.SetParent(parent, false);
        var text = gameObject.GetComponent<Text>();
        text.text = value;
        text.color = color;
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return text;
    }

    private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
