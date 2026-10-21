using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(KartInputReader))]
[RequireComponent(typeof(KartGroundDetector))]
[RequireComponent(typeof(KartMotor))]
[RequireComponent(typeof(KartTrackAdhesion))]
public class KartController : MonoBehaviour
{
    [Header("Física")]
    [SerializeField] private Vector3 centerOfMassLocal = new Vector3(0f, -0.3f, 0f);

    private Rigidbody rb;
    private KartInputReader input;
    private KartGroundDetector ground;
    private KartMotor motor;
    private KartTrackAdhesion adhesion;

    public bool IsGrounded => ground.IsGrounded;
    public float ForwardSpeed => motor.ForwardSpeed;

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

        motor.Tick(input.Throttle, input.Steer, input.Brake, input.Drift, ground.IsGrounded, ground.Normal);

        adhesion.Tick(ground);
    }
}