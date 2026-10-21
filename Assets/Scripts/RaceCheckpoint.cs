using UnityEngine;

public class RaceCheckpoint : MonoBehaviour
{
    public int Index { get; set; } = -1;

    private void Reset()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(20f, 6f, 3f);
        box.center = new Vector3(0f, 3f, 0f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Index < 0 || RaceManager.Instance == null)
            return;

        Rigidbody body = other.attachedRigidbody;

        if (body != null && body.TryGetComponent(out KartRacer racer))
        {
            RaceManager.Instance.OnCheckpointHit(racer, Index);
        }
    }

    private void OnDrawGizmos()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null)
            return;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Index == 0 ? new Color(1f, 0.9f, 0.1f, 0.9f) : new Color(0.2f, 0.9f, 1f, 0.8f);
        Gizmos.DrawWireCube(box.center, box.size);

        Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.12f);
        Gizmos.DrawCube(box.center, box.size);

        Gizmos.color = Color.white;
        Gizmos.DrawLine(box.center, box.center + Vector3.forward * 4f);
    }
}
