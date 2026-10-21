using System.Collections.Generic;
using UnityEngine;

public class KartRacer : MonoBehaviour
{
    public bool Registered { get; private set; }
    public int PlayerIndex { get; private set; }
    public Color PlayerColor { get; private set; } = Color.white;

    public int NextCheckpoint { get; set; }
    public int CheckpointsPassed { get; set; }
    public int LapsCompleted { get; set; }

    public bool Finished { get; set; }
    public int FinishPlace { get; set; }
    public float FinishTime { get; set; }

    public int Position { get; set; }

    public Vector3 RespawnPosition { get; private set; }
    public Quaternion RespawnRotation { get; private set; } = Quaternion.identity;
    public float AirborneTime { get; private set; }

    public Rigidbody Body { get; private set; }

    private KartController _kart;

    private void Update()
    {
        if (!Registered || Finished || _kart == null)
        {
            return;
        }

        AirborneTime = _kart.IsGrounded ? 0f : AirborneTime + Time.deltaTime;
    }

    public void SetRespawnPoint(Vector3 position, Quaternion rotation)
    {
        RespawnPosition = position;
        RespawnRotation = rotation;
    }

    public void ResetAirborne()
    {
        AirborneTime = 0f;
    }

    public void Init(int playerIndex, Color color, int firstCheckpoint)
    {
        _kart = GetComponent<KartController>();
        Body = GetComponent<Rigidbody>();
        RespawnPosition = transform.position;
        RespawnRotation = transform.rotation;
        AirborneTime = 0f;

        PlayerIndex = playerIndex;
        PlayerColor = color;
        NextCheckpoint = firstCheckpoint;
        CheckpointsPassed = 0;
        LapsCompleted = 0;
        Finished = false;
        FinishPlace = 0;
        FinishTime = 0f;
        Position = playerIndex + 1;
        Registered = true;
    }

    public int CurrentLap(int totalLaps)
    {
        return Mathf.Min(LapsCompleted + 1, totalLaps);
    }

    public float GetProgress(IReadOnlyList<RaceCheckpoint> checkpoints)
    {
        int count = checkpoints.Count;
        if (count < 2)
        {
            return CheckpointsPassed;
        }

        Vector3 from = checkpoints[(NextCheckpoint - 1 + count) % count].transform.position;
        Vector3 to = checkpoints[NextCheckpoint].transform.position;

        Vector3 segment = to - from;
        segment.y = 0f;

        float lengthSqr = segment.sqrMagnitude;
        if (lengthSqr < 1e-4f)
        {
            return CheckpointsPassed;
        }

        Vector3 offset = transform.position - from;
        offset.y = 0f;

        float fraction = Mathf.Clamp01(Vector3.Dot(offset, segment) / lengthSqr);

        return CheckpointsPassed + Mathf.Min(fraction, 0.99f);
    }
}