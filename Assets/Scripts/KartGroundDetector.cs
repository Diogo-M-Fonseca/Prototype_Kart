using UnityEngine;

public class KartGroundDetector : MonoBehaviour
{
    [Header("Sondas")]
    [SerializeField] private Transform[] probes;
    [SerializeField] private LayerMask trackMask = ~0;

    [Header("Alcance")]
    [SerializeField, Min(0.01f)] private float rayLength = 0.6f;
    [SerializeField, Min(0.01f)] private float extendedRayLength = 3f;
    [SerializeField, Min(0f)] private float coyoteTime = 0.12f;

    private float lastContactTime = float.NegativeInfinity;

    public bool HasContact { get; private set; }

    public bool IsGrounded { get; private set; }

    public Vector3 Normal { get; private set; } = Vector3.up;

    public void Tick(bool useExtendedRange)
    {
        float length = useExtendedRange ? extendedRayLength : rayLength;
        Vector3 down = -transform.up;

        Vector3 normalSum = Vector3.zero;
        int hits = 0;
        int count = (probes != null && probes.Length > 0) ? probes.Length : 1;

        for (int i = 0; i < count; i++)
        {
            Vector3 origin = (probes != null && probes.Length > 0 && probes[i] != null)
                ? probes[i].position
                : transform.position;

            if (Physics.Raycast(origin, down, out RaycastHit hit, length, trackMask, QueryTriggerInteraction.Ignore))
            {
                normalSum += hit.normal;
                hits++;
            }
        }

        HasContact = hits > 0;

        if (HasContact)
        {
            Normal = (normalSum / hits).normalized;
            lastContactTime = Time.time;
        }

        IsGrounded = HasContact || (Time.time - lastContactTime) <= coyoteTime;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        int count = (probes != null && probes.Length > 0) ? probes.Length : 1;

        for (int i = 0; i < count; i++)
        {
            Vector3 origin = (probes != null && probes.Length > 0 && probes[i] != null)
                ? probes[i].position
                : transform.position;

            Gizmos.DrawLine(origin, origin - transform.up * rayLength);
        }
    }
}