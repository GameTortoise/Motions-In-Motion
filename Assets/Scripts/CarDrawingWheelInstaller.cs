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

    public void InstallDrawingAsWheels(GameObject drawing)
    {
        if (drawing == null || wheelMounts.Count == 0)
            return;

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
