using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Turns world drawings made by PaintEditorCanvas into the two physical wheels of this car.
/// Each drawing is duplicated so both wheels always use the same shape.
/// </summary>
[DisallowMultipleComponent]
public sealed class CarDrawingWheelInstaller : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float chassisMass = 4f;
    [SerializeField, Min(0.05f)] private float wheelRadius = 0.5f;
    [SerializeField, Min(0.01f)] private float wheelMass = 1f;
    [SerializeField, Range(0f, 1f)] private float wheelFriction = 0.9f;
    [SerializeField, Min(0f)] private float suspensionFrequency = 5f;
    [SerializeField, Range(0f, 1f)] private float suspensionDamping = 0.8f;

    private static CarDrawingWheelInstaller activeInstaller;

    private readonly List<Vector3> wheelMounts = new();
    private readonly List<GameObject> installedWheels = new();
    private Rigidbody2D chassisBody;
    private PhysicsMaterial2D wheelMaterial;
    private float preservedRotationSpeed = -360f;
    private Texture2D installedDrawingTexture;
    private Sprite installedDrawingSprite;

    private void Awake()
    {
        activeInstaller = this;
        chassisBody = GetComponent<Rigidbody2D>();
        if (chassisBody == null)
            chassisBody = gameObject.AddComponent<Rigidbody2D>();

        chassisBody.interpolation = RigidbodyInterpolation2D.Interpolate;
        chassisBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        chassisBody.mass = chassisMass;

        wheelMaterial = new PhysicsMaterial2D("Drawn Wheel Grip")
        {
            friction = wheelFriction,
            bounciness = 0f
        };

        var originalWheels = GetComponentsInChildren<WheelSpinner>(true);
        System.Array.Sort(originalWheels,
            (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

        if (originalWheels.Length > 0)
            preservedRotationSpeed = originalWheels[0].RotationSpeed;

        foreach (var wheel in originalWheels)
        {
            wheelMounts.Add(transform.InverseTransformPoint(wheel.transform.position));
            PrepareWheel(wheel.gameObject, false);
            installedWheels.Add(wheel.gameObject);
        }

        if (wheelMounts.Count == 0)
        {
            wheelMounts.Add(new Vector3(-0.25f, -0.9f, 0f));
            wheelMounts.Add(new Vector3(0.25f, -0.9f, 0f));
        }
    }

    private void OnDestroy()
    {
        if (activeInstaller == this)
            activeInstaller = null;

        if (wheelMaterial != null)
            Destroy(wheelMaterial);
        if (installedDrawingSprite != null)
            Destroy(installedDrawingSprite);
        if (installedDrawingTexture != null)
            Destroy(installedDrawingTexture);
    }

    /// <summary>
    /// Entry point used by PaintEditorCanvas immediately after it creates a drawing GameObject.
    /// </summary>
    public static bool TryInstallDrawing(GameObject drawing)
    {
        if (activeInstaller == null || drawing == null)
            return false;

        activeInstaller.InstallDrawingAsWheels(drawing);
        return true;
    }

    /// <summary>Selects this car for the next call to TryInstallDrawing.</summary>
    public void SelectForNextDrawing()
    {
        activeInstaller = this;
    }

    public void InstallDrawingAsWheels(GameObject drawing)
    {
        if (drawing == null || wheelMounts.Count == 0)
            return;

        var drawingRenderer = drawing.GetComponent<SpriteRenderer>();
        var nextSprite = drawingRenderer != null ? drawingRenderer.sprite : null;
        var nextTexture = nextSprite != null ? nextSprite.texture : null;
        var previousSprite = installedDrawingSprite;
        var previousTexture = installedDrawingTexture;

        drawing.transform.SetParent(null, true);
        drawing.transform.rotation = transform.rotation;
        FitDrawingToWheelRadius(drawing);

        var replacementWheels = new List<GameObject> { drawing };
        for (var slot = 1; slot < wheelMounts.Count; slot++)
            replacementWheels.Add(Instantiate(drawing));

        foreach (var oldWheel in installedWheels)
        {
            if (oldWheel == null)
                continue;

            oldWheel.SetActive(false);
            Destroy(oldWheel);
        }
        installedWheels.Clear();

        for (var slot = 0; slot < replacementWheels.Count; slot++)
        {
            var wheel = replacementWheels[slot];
            wheel.name = slot == 0 ? "Drawn Wheel Left" : "Drawn Wheel Right";
            wheel.transform.position = transform.TransformPoint(wheelMounts[slot]);
            wheel.transform.rotation = transform.rotation;
            PrepareWheel(wheel, true);
            installedWheels.Add(wheel);
        }

        installedDrawingSprite = nextSprite;
        installedDrawingTexture = nextTexture;
        if (previousSprite != null && previousSprite != installedDrawingSprite)
            Destroy(previousSprite);
        if (previousTexture != null && previousTexture != installedDrawingTexture)
            Destroy(previousTexture);
    }

    public bool InstallPngAsWheels(byte[] pngData, int maximumDimension = 2048)
    {
        if (!TryReadPngSize(pngData, out var width, out var height) ||
            width > maximumDimension || height > maximumDimension)
            return false;

        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            name = "Network Wheel Drawing Texture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        if (!texture.LoadImage(pngData, false) || texture.width != width || texture.height != height)
        {
            Destroy(texture);
            return false;
        }

        var sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.Tight,
            Vector4.zero,
            true);
        sprite.name = "Network Wheel Drawing Sprite";

        var drawing = new GameObject("Network Wheel Drawing");
        var spriteRenderer = drawing.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        spriteRenderer.sortingOrder = 10;

        var polygonCollider = drawing.AddComponent<PolygonCollider2D>();
        CopySpritePhysicsShape(sprite, polygonCollider);
        drawing.AddComponent<Rigidbody2D>();
        InstallDrawingAsWheels(drawing);
        return true;
    }

    public static bool TryReadPngSize(byte[] pngData, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (pngData == null || pngData.Length < 24)
            return false;

        if (pngData[0] != 137 || pngData[1] != 80 || pngData[2] != 78 || pngData[3] != 71 ||
            pngData[4] != 13 || pngData[5] != 10 || pngData[6] != 26 || pngData[7] != 10 ||
            pngData[12] != 73 || pngData[13] != 72 || pngData[14] != 68 || pngData[15] != 82)
            return false;

        width = ReadBigEndianInt(pngData, 16);
        height = ReadBigEndianInt(pngData, 20);
        return width > 0 && height > 0;
    }

    private static int ReadBigEndianInt(byte[] data, int offset)
    {
        return (data[offset] << 24) |
               (data[offset + 1] << 16) |
               (data[offset + 2] << 8) |
               data[offset + 3];
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

    private void FitDrawingToWheelRadius(GameObject drawing)
    {
        var renderer = drawing.GetComponent<SpriteRenderer>();
        if (renderer == null)
            return;

        var largestDimension = Mathf.Max(renderer.bounds.size.x, renderer.bounds.size.y);
        if (largestDimension <= Mathf.Epsilon)
            return;

        var scale = wheelRadius * 2f / largestDimension;
        drawing.transform.localScale *= scale;
    }

    private void PrepareWheel(GameObject wheel, bool addSpinner)
    {
        // Wheels must be independent bodies. Parenting them to the scaled chassis would
        // distort both their drawing and their collider.
        wheel.transform.SetParent(null, true);

        var body = wheel.GetComponent<Rigidbody2D>();
        if (body == null)
            body = wheel.AddComponent<Rigidbody2D>();

        body.bodyType = RigidbodyType2D.Dynamic;
        body.mass = wheelMass;
        body.gravityScale = 1f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        foreach (var wheelCollider in wheel.GetComponents<Collider2D>())
            wheelCollider.sharedMaterial = wheelMaterial;

        var joint = wheel.GetComponent<WheelJoint2D>();
        if (joint == null)
            joint = wheel.AddComponent<WheelJoint2D>();

        joint.connectedBody = chassisBody;
        joint.autoConfigureConnectedAnchor = false;
        joint.anchor = Vector2.zero;
        joint.connectedAnchor = transform.InverseTransformPoint(wheel.transform.position);
        joint.enableCollision = false;

        var suspension = joint.suspension;
        suspension.frequency = suspensionFrequency;
        suspension.dampingRatio = suspensionDamping;
        suspension.angle = 90f;
        joint.suspension = suspension;

        if (addSpinner)
        {
            var spinner = wheel.GetComponent<WheelSpinner>();
            if (spinner == null)
                spinner = wheel.AddComponent<WheelSpinner>();
            spinner.SetRotationSpeed(preservedRotationSpeed);
        }
    }
}
