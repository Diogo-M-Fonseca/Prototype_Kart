using UnityEngine;

public class KartMotor : MonoBehaviour
{
    [Header("Velocidade")]
    [SerializeField, Min(0f)] private float maxForwardSpeed = 35f;
    [SerializeField, Min(0f)] private float maxReverseSpeed = 12f;
    [SerializeField, Min(0f)] private float acceleration = 22f;
    [SerializeField, Min(0f)] private float brakeDeceleration = 45f;
    [SerializeField, Min(0f)] private float coastDeceleration = 6f;

    [Header("Direção")]
    [SerializeField, Min(0f)] private float steerSpeed = 100f;
    [SerializeField]
    private AnimationCurve steerBySpeed = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0.55f));
    [SerializeField, Min(1f)] private float driftSteerMultiplier = 1.4f;

    [Header("Aderência lateral")]
    [SerializeField, Min(0f)] private float grip = 10f;
    [SerializeField, Min(0f)] private float driftGrip = 2.5f;

    private Rigidbody rb;

    public float ForwardSpeed { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void Tick(float throttle, float steer, bool brake, bool drift, bool grounded, Vector3 groundNormal)
    {
        if (!grounded)
        {
            return;
        }

        float dt = Time.fixedDeltaTime;
        Quaternion rotation = rb.rotation;
        Vector3 n = groundNormal;

        Vector3 forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, n);
        if (forward.sqrMagnitude < 1e-4f)
        {
            return;
        }

        forward.Normalize();
        Vector3 right = Vector3.Cross(n, forward);

        Vector3 velocity = rb.linearVelocity;
        float forwardSpeed = Vector3.Dot(velocity, forward);
        float lateralSpeed = Vector3.Dot(velocity, right);
        float normalSpeed = Vector3.Dot(velocity, n);

        if (Mathf.Abs(throttle) > 0.01f)
        {
            float target = throttle > 0f ? throttle * maxForwardSpeed : throttle * maxReverseSpeed;
            bool opposing = forwardSpeed * throttle < -0.5f;
            float rate = opposing ? brakeDeceleration : acceleration;
            forwardSpeed = Mathf.MoveTowards(forwardSpeed, target, rate * dt);
        }
        else
        {
            forwardSpeed = Mathf.MoveTowards(forwardSpeed, 0f, coastDeceleration * dt);
        }

        if (brake)
        {
            forwardSpeed = Mathf.MoveTowards(forwardSpeed, 0f, brakeDeceleration * dt);
        }

        lateralSpeed *= Mathf.Exp(-(drift ? driftGrip : grip) * dt);

        rb.linearVelocity = forward * forwardSpeed + right * lateralSpeed + n * normalSpeed;

        float speedRatio = maxForwardSpeed > 0f ? Mathf.Clamp01(Mathf.Abs(forwardSpeed) / maxForwardSpeed) : 0f;
        float direction = Mathf.Abs(forwardSpeed) < 0.5f ? 0f : Mathf.Sign(forwardSpeed);
        float yaw = steer * steerSpeed * steerBySpeed.Evaluate(speedRatio) * direction
                    * (drift ? driftSteerMultiplier : 1f) * dt;

        rb.rotation = Quaternion.AngleAxis(yaw, n) * rotation;
        rb.angularVelocity = Vector3.zero;

        ForwardSpeed = forwardSpeed;
    }
}
