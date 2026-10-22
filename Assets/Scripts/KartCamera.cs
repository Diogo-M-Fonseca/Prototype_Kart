using UnityEngine;

public class KartCamera : MonoBehaviour
{
    [Header("Alvo")]
    [SerializeField] private Rigidbody target;

    [Header("Enquadramento")]
    [SerializeField, Min(0f)] private float distance = 6f;
    [SerializeField] private float height = 2f;
    [SerializeField] private float pivotHeight = 0.8f;
    [SerializeField, Range(-10f, 45f)] private float pitch = 10f;

    [Header("Direção")]
    [SerializeField, Min(0f)] private float minSpeedForHeading = 2f;
    [SerializeField] private bool ignoreReverse = true;
    [SerializeField, Min(0.01f)] private float yawSmoothTime = 0.25f;

    [Header("Seguimento")]
    [SerializeField, Min(0f)] private float horizontalFollowSharpness = 12f;
    [SerializeField, Min(0f)] private float verticalFollowSharpness = 10f;

    private Vector3 heading = Vector3.forward;
    private Vector3 pivot;
    private float yaw;
    private float yawVelocity;
    private KartController targetKart;

    private void Start()
    {
        CacheTargetKart();
        SnapToTarget();
    }

    public void SetTarget(Rigidbody newTarget)
    {
        target = newTarget;
        CacheTargetKart();
        SnapToTarget();
    }

    private void CacheTargetKart()
    {
        targetKart = null;

        if (target != null)
        {
            target.TryGetComponent(out targetKart);
        }
    }

    public void SnapToTarget()
    {
        if (target == null)
            return;

        heading = ReadHeading();
        yaw = Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg;
        yawVelocity = 0f;
        pivot = target.position + Vector3.up * pivotHeight;
        Apply();
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        heading = ReadHeading();
        float targetYaw = Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg;
        yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVelocity, yawSmoothTime);

        float dt = Time.deltaTime;
        float kh = 1f - Mathf.Exp(-horizontalFollowSharpness * dt);
        float kv = 1f - Mathf.Exp(-verticalFollowSharpness * dt);
        Vector3 targetPivot = target.position + Vector3.up * pivotHeight;

        pivot = new Vector3(
            Mathf.Lerp(pivot.x, targetPivot.x, kh),
            Mathf.Lerp(pivot.y, targetPivot.y, kv),
            Mathf.Lerp(pivot.z, targetPivot.z, kh));

        Apply();
    }

    private void Apply()
    {
        Vector3 back = Quaternion.Euler(0f, yaw, 0f) * Vector3.back;
        Vector3 position = pivot + back * distance + Vector3.up * height;

        transform.SetPositionAndRotation(position, Quaternion.Euler(pitch, yaw, 0f));
    }

    private Vector3 ReadHeading()
    {
        if (targetKart != null && targetKart.IsStunned)
        {
            return heading;
        }

        Vector3 velocity = target.linearVelocity;
        Vector3 flatVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
        Vector3 kartForward = target.transform.forward;
        Vector3 flatForward = Vector3.ProjectOnPlane(kartForward, Vector3.up);

        bool movingBackwards = Vector3.Dot(velocity, kartForward) < 0f;
        bool useVelocity = flatVelocity.sqrMagnitude >= minSpeedForHeading * minSpeedForHeading
                           && !(ignoreReverse && movingBackwards);

        if (useVelocity)
        {
            return flatVelocity.normalized;
        }

        if (flatForward.sqrMagnitude > 1e-3f)
        {
            return flatForward.normalized;
        }

        return heading;
    }
}