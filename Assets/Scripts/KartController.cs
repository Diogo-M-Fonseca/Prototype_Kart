using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class KartController : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private Rigidbody rb;


    [Header("Velocidade")]
    [SerializeField, Min(0f)] private float maxSpeed = 20f;
    [SerializeField, Min(0f)] private float reverseSpeed = 6f;
    [SerializeField, Min(0f)] private float acceleration = 15f;
    [SerializeField, Min(0f)] private float braking = 30f;
    [SerializeField, Min(0f)] private float coastDrag = 5f;

    [Header("Direção")]
    [SerializeField, Min(0f)] private float turnSpeed = 100f;
    [SerializeField, Min(0.1f)] private float fullSteerSpeed = 5f;

    [Header("Aderência")]
    [SerializeField, Min(0f)] private float grip = 10f;

    [Header("Chão")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField, Min(0f)] private float groundCheckDistance = 0.7f;

    [Header("Debug (só leitura)")]
    [SerializeField] private Vector2 moveInput;
    [SerializeField] private float forwardSpeed;
    [SerializeField] private float lateralSpeed;
    [SerializeField] private bool isGrounded;

    private InputAction moveAction;

    private void Reset()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void OnEnable()
    {
        if (InputSystem.actions == null)
        {
            Debug.LogError("InputSystem.actions é null. Ativa as Project-Wide Actions nas Project Settings.", this);
            enabled = false;
            return;
        }

        moveAction = InputSystem.actions.FindAction("Player/Move", false);
        if (moveAction == null)
        {
            Debug.LogError($"Action '{"Player/Move"}' não encontrada.", this);
            enabled = false;
            return;
        }

        moveAction.Enable();
    }

    private void Update()
    {
        moveInput = moveAction.ReadValue<Vector2>();
    }

    private void FixedUpdate()
    {
        isGrounded = Physics.Raycast(
        transform.position + transform.up * 0.1f,
        -transform.up,
        groundCheckDistance,
        groundMask,
        QueryTriggerInteraction.Ignore);

        if (!isGrounded) return;

        float dt = Time.fixedDeltaTime
       ;

        Vector3 velocity = rb.linearVelocity;
        Vector3 fwd = transform.forward;
        Vector3 right = transform.right;

        forwardSpeed = Vector3.Dot(velocity, fwd);
        lateralSpeed = Vector3.Dot(velocity, right);
        Vector3 other = velocity - fwd * forwardSpeed - right * lateralSpeed;

        float throttle = moveInput.y;
        if (throttle > 0.01f)
        {
            float rate = forwardSpeed < 0f ? braking : acceleration;
            forwardSpeed = Mathf.MoveTowards(forwardSpeed, maxSpeed * throttle, rate * dt);
        }
        else if (throttle < -0.01f)
        {
            float rate = forwardSpeed > 0f ? braking : acceleration;
            forwardSpeed = Mathf.MoveTowards(forwardSpeed, -reverseSpeed * -throttle, rate * dt);
        }
        else
        {
            forwardSpeed = Mathf.MoveTowards(forwardSpeed, 0f, coastDrag * dt);
        }

        lateralSpeed = Mathf.MoveTowards(lateralSpeed, 0f, grip * Mathf.Max(Mathf.Abs(lateralSpeed), 1f) * dt);

        rb.linearVelocity = fwd * forwardSpeed + right * lateralSpeed + other;

        float steerFactor = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / fullSteerSpeed) * Mathf.Sign(forwardSpeed);
        float yawDegrees = moveInput.x * turnSpeed * steerFactor;
        rb.angularVelocity = Vector3.up * (yawDegrees * Mathf.Deg2Rad);
    }
}