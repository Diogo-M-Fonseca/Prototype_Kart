using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class KartController : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference steerAction;

    [Header("Movement")]
    [SerializeField] private float acceleration = 12f;
    [SerializeField] private float brakeAcceleration = 18f;
    [SerializeField] private float maxSpeed = 20f;
    [SerializeField] private float maxReverseSpeed = 5f;

    [Header("Steering")]
    [SerializeField] private float turnSpeed = 100f;
    [SerializeField] private float minSteeringSpeed = 0.1f;

    private Rigidbody rb;

    private float moveInput;
    private float steerInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.constraints |= RigidbodyConstraints.FreezeRotationX
                        | RigidbodyConstraints.FreezeRotationZ;
    }

    private void OnEnable()
    {
        moveAction.action.Enable();
        steerAction.action.Enable();
    }

    private void OnDisable()
    {
        moveAction.action.Disable();
        steerAction.action.Disable();
    }

    private void Update()
    {
        moveInput = moveAction.action.ReadValue<float>();
        steerInput = steerAction.action.ReadValue<float>();
    }

    private void FixedUpdate()
    {
        MoveKart();
        SteerKart();
        LimitSpeed();
    }

    private void MoveKart()
    {
        float forwardSpeed =
            Vector3.Dot(rb.linearVelocity, transform.forward);

        if (moveInput > 0f)
        {
            if (forwardSpeed < maxSpeed)
            {
                rb.AddForce(
                    transform.forward * acceleration,
                    ForceMode.Acceleration
                );
            }
        }
        else if (moveInput < 0f)
        {
            if (forwardSpeed > 0.5f)
            {
                rb.AddForce(
                    -transform.forward * brakeAcceleration,
                    ForceMode.Acceleration
                );
            }
            else if (forwardSpeed > -maxReverseSpeed)
            {
                rb.AddForce(
                    -transform.forward * acceleration,
                    ForceMode.Acceleration
                );
            }
        }
    }

    private void SteerKart()
    {
        float forwardSpeed =
            Vector3.Dot(rb.linearVelocity, transform.forward);

        if (Mathf.Abs(forwardSpeed) < minSteeringSpeed)
            return;

        float speedFactor =
            Mathf.Clamp01(Mathf.Abs(forwardSpeed) / maxSpeed);

        float reverseFactor = Mathf.Sign(forwardSpeed);

        float rotation =
            steerInput * turnSpeed * speedFactor
            * reverseFactor * Time.fixedDeltaTime;

        Quaternion turnRotation =
            Quaternion.Euler(0f, rotation, 0f);

        rb.MoveRotation(rb.rotation * turnRotation);
    }

    private void LimitSpeed()
    {
        Vector3 velocity = rb.linearVelocity;

        Vector3 horizontalVelocity =
            new Vector3(velocity.x, 0f, velocity.z);

        float forwardSpeed =
            Vector3.Dot(horizontalVelocity, transform.forward);

        if (forwardSpeed > maxSpeed)
        {
            horizontalVelocity -=
                transform.forward * (forwardSpeed - maxSpeed);
        }
        else if (forwardSpeed < -maxReverseSpeed)
        {
            horizontalVelocity -=
                transform.forward * (forwardSpeed + maxReverseSpeed);
        }

        rb.linearVelocity = new Vector3(
            horizontalVelocity.x,
            velocity.y,
            horizontalVelocity.z
        );
    }
}
