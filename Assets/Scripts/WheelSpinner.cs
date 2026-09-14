using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class WheelSpinner : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = -360f;
    [SerializeField, Min(0f)] private float maximumTorque = 20f;
    [SerializeField, Min(0f)] private float acceleration = 0.05f;

    private Rigidbody2D wheelBody;

    private void Awake()
    {
        wheelBody = GetComponent<Rigidbody2D>();
        if (wheelBody == null)
            wheelBody = gameObject.AddComponent<Rigidbody2D>();

        wheelBody.interpolation = RigidbodyInterpolation2D.Interpolate;
        wheelBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    private void FixedUpdate()
    {
        // Only wheel torque moves the car; there is no direct chassis force or player input.
        var speedError = rotationSpeed - wheelBody.angularVelocity;
        var torque = Mathf.Clamp(speedError * acceleration, -maximumTorque, maximumTorque);
        wheelBody.AddTorque(torque);
    }
}
