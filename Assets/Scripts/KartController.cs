using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(KartInputReader))]
[RequireComponent(typeof(KartGroundDetector))]
[RequireComponent(typeof(KartMotor))]
[RequireComponent(typeof(KartTrackAdhesion))]
public class KartController : MonoBehaviour
{
    [Header("Fisica")]
    [SerializeField] private Vector3 centerOfMassLocal = new Vector3(0f, -0.3f, 0f);

    [Header("Atordoamento")]
    [SerializeField, Min(0f)] private float stunInvulnerability = 1f;

    private Rigidbody rb;
    private KartInputReader input;
    private KartGroundDetector ground;
    private KartMotor motor;
    private KartTrackAdhesion adhesion;

    private float stunTimer;
    private float stunSpinPerSecond;
    private float invulnerableUntil;

    public bool IsGrounded => ground.IsGrounded;
    public float ForwardSpeed => motor.ForwardSpeed;

    public bool IsStunned => stunTimer > 0f;

    public bool CanBeStunned => !IsStunned && Time.time >= invulnerableUntil;

    public bool Stun(float duration, float spinTurns = 2f)
    {
        if (duration <= 0f || !CanBeStunned)
        {
            return false;
        }

        stunTimer = duration;
        stunSpinPerSecond = 360f * spinTurns / duration * (Random.value < 0.5f ? -1f : 1f);
        invulnerableUntil = Time.time + duration + stunInvulnerability;
        return true;
    }

    public bool FullAdhesion
    {
        get => adhesion.FullAdhesion;
        set => adhesion.FullAdhesion = value;
    }

    private void Reset()
    {
        Rigidbody body = GetComponent<Rigidbody>();
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.mass = 150f;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        input = GetComponent<KartInputReader>();
        ground = GetComponent<KartGroundDetector>();
        motor = GetComponent<KartMotor>();
        adhesion = GetComponent<KartTrackAdhesion>();

        rb.centerOfMass = centerOfMassLocal;
    }

    private void FixedUpdate()
    {
        ground.Tick(adhesion.FullAdhesion);

        if (stunTimer > 0f)
        {
            float dt = Time.fixedDeltaTime;
            float step = Mathf.Min(dt, stunTimer);
            stunTimer -= dt;

            motor.Tick(0f, 0f, true, false, ground.IsGrounded, ground.Normal);

            if (ground.IsGrounded)
            {
                rb.rotation = Quaternion.AngleAxis(stunSpinPerSecond * step, ground.Normal) * rb.rotation;
            }
        }
        else
        {
            motor.Tick(input.Throttle, input.Steer, input.Brake, input.Drift, ground.IsGrounded, ground.Normal);
        }

        adhesion.Tick(ground);
    }
}