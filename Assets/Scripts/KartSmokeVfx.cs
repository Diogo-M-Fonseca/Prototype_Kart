using UnityEngine;

public class KartSmokeVfx : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private KartController kart;
    [SerializeField] private KartInputReader input;
    [SerializeField] private Rigidbody rb;

    [Header("Fumo andar")]
    [SerializeField] private ParticleSystem[] moveSmoke;
    [SerializeField, Min(0f)] private float moveMinSpeed = 3f;
    [Tooltip("Partículas por metro percorrido. Mais = rasto mais contínuo.")]
    [SerializeField, Min(0f)] private float moveParticlesPerMeter = 3f;

    [Header("Fumo travar")]
    [SerializeField] private ParticleSystem[] brakeSmoke;
    [SerializeField, Min(0f)] private float brakeMinSpeed = 6f;
    [SerializeField, Min(0f)] private float brakeMaxRate = 45f;

    [Header("Fumo drift")]
    [SerializeField] private ParticleSystem[] driftSmoke;
    [SerializeField, Min(0f)] private float driftMinSpeed = 6f;
    [SerializeField, Range(0f, 1f)] private float driftMinSteer = 0.1f;
    [SerializeField, Min(0.1f)] private float lateralSpeedForMax = 4f;
    [SerializeField, Min(0f)] private float driftMaxRate = 70f;

    [Header("Ajustes gerais")]
    [SerializeField, Min(0.1f)] private float referenceSpeed = 35f;
    [SerializeField, Min(0.1f)] private float intensitySharpness = 14f;

    private float moveIntensity;
    private float brakeIntensity;
    private float driftIntensity;

    private void Reset()
    {
        kart = GetComponentInParent<KartController>();
        input = GetComponentInParent<KartInputReader>();
        rb = GetComponentInParent<Rigidbody>();
    }

    private void Awake()
    {
        if (kart == null) kart = GetComponentInParent<KartController>();
        if (input == null) input = GetComponentInParent<KartInputReader>();
        if (rb == null) rb = GetComponentInParent<Rigidbody>();
    }

    private void Start()
    {
        PlayAll(moveSmoke);
        PlayAll(brakeSmoke);
        PlayAll(driftSmoke);

        SetRateOverDistance(moveSmoke, 0f);
        SetRateOverTime(brakeSmoke, 0f);
        SetRateOverTime(driftSmoke, 0f);
    }

    private void Update()
    {
        if (kart == null || input == null || rb == null)
            return;

        bool grounded = kart.IsGrounded;

        Vector3 velocity = rb.linearVelocity;
        Vector3 flat = Vector3.ProjectOnPlane(velocity, transform.up);
        float speed = flat.magnitude;
        float forwardSpeed = kart.ForwardSpeed;
        float lateralSpeed = Mathf.Abs(Vector3.Dot(flat, transform.right));

        float targetMove = 0f;
        if (grounded && speed > moveMinSpeed)
            targetMove = Mathf.Clamp01(speed / referenceSpeed);

        float throttle = input.Throttle;
        bool opposingThrottle = Mathf.Abs(throttle) > 0.1f && forwardSpeed * throttle < -0.5f;
        bool braking = input.Brake || opposingThrottle;

        float targetBrake = 0f;
        if (grounded && braking && Mathf.Abs(forwardSpeed) > brakeMinSpeed)
            targetBrake = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / referenceSpeed);

        float targetDrift = 0f;
        bool drifting = grounded
                        && input.Drift
                        && Mathf.Abs(input.Steer) > driftMinSteer
                        && speed > driftMinSpeed;

        if (drifting)
        {
            float lateralFactor = Mathf.Clamp01(lateralSpeed / lateralSpeedForMax);
            targetDrift = Mathf.Lerp(0.5f, 1f, lateralFactor);
        }

        float k = 1f - Mathf.Exp(-intensitySharpness * Time.deltaTime);
        moveIntensity = Mathf.Lerp(moveIntensity, targetMove, k);
        brakeIntensity = Mathf.Lerp(brakeIntensity, targetBrake, k);
        driftIntensity = Mathf.Lerp(driftIntensity, targetDrift, k);

        float moveRate = moveIntensity > 0.01f
            ? moveParticlesPerMeter * Mathf.Lerp(0.5f, 1f, moveIntensity)
            : 0f;

        SetRateOverDistance(moveSmoke, moveRate);
        SetRateOverTime(brakeSmoke, brakeIntensity * brakeMaxRate);
        SetRateOverTime(driftSmoke, driftIntensity * driftMaxRate);
    }

    private static void PlayAll(ParticleSystem[] systems)
    {
        if (systems == null) return;

        foreach (ParticleSystem ps in systems)
        {
            if (ps != null && !ps.isPlaying)
                ps.Play();
        }
    }

    private static void SetRateOverTime(ParticleSystem[] systems, float rate)
    {
        if (systems == null) return;

        foreach (ParticleSystem ps in systems)
        {
            if (ps == null) continue;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverDistance = 0f;
            emission.rateOverTime = rate < 0.05f ? 0f : rate;
        }
    }

    private static void SetRateOverDistance(ParticleSystem[] systems, float rate)
    {
        if (systems == null) return;

        foreach (ParticleSystem ps in systems)
        {
            if (ps == null) continue;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = rate < 0.01f ? 0f : rate;
        }
    }
}