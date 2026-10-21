using UnityEngine;

public class KartTrackAdhesion : MonoBehaviour
{
    [Header("Aderência total")]
    [SerializeField] private bool fullAdhesion = true;
    [SerializeField, Min(0f)] private float stickAcceleration = 40f;

    [Header("Downforce")]
    [SerializeField, Min(0f)] private float speedDownforce = 0.6f;

    [Header("Anti-voo")]
    [SerializeField] private bool limitLaunch = true;
    [SerializeField, Min(0f)] private float maxSeparationSpeed = 1.5f;
    [SerializeField, Min(1f)] private float airGravityMultiplier = 2f;

    [Header("Alinhamento")]
    [SerializeField, Min(0f)] private float alignSpeed = 12f;

    private Rigidbody rb;

    public bool FullAdhesion
    {
        get => fullAdhesion;
        set => fullAdhesion = value;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
    }

    public void Tick(KartGroundDetector ground)
    {
        Vector3 velocity = rb.linearVelocity;
        float speed = velocity.magnitude;

        if (ground.IsGrounded)
        {
            Vector3 n = ground.Normal;

            if (fullAdhesion)
                rb.AddForce(-n * (stickAcceleration + speed * speedDownforce), ForceMode.Acceleration);
            else
                rb.AddForce(Physics.gravity - n * (speed * speedDownforce), ForceMode.Acceleration);

            if (limitLaunch && ground.HasContact)
            {
                float separation = Vector3.Dot(velocity, n);
                if (separation > maxSeparationSpeed)
                    rb.linearVelocity = velocity - n * (separation - maxSeparationSpeed);
            }
        }
        else
        {
            rb.AddForce(Physics.gravity * airGravityMultiplier, ForceMode.Acceleration);
        }

        Align(ground);
    }

    private void Align(KartGroundDetector ground)
    {
        Vector3 targetUp;

        if (ground.IsGrounded)
        {
            targetUp = ground.Normal;
        }
        else if (fullAdhesion)
        {
            return;
        }
        else
        {
            targetUp = Vector3.up;
        }

        Quaternion current = rb.rotation;
        Quaternion target = Quaternion.FromToRotation(current * Vector3.up, targetUp) * current;
        float t = 1f - Mathf.Exp(-alignSpeed * Time.fixedDeltaTime);

        rb.rotation = Quaternion.Slerp(current, target, t);
    }
}
