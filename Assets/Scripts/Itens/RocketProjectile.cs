using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class RocketProjectile : MonoBehaviour
{
    private const int MaxCorners = 64;

    [Header("Movimento")]
    [SerializeField, Min(1f)] private float _speed = 40f;
    [SerializeField, Min(1f)] private float _turnSpeed = 300f;
    [SerializeField, Min(0f)] private float _hoverHeight = 0.7f;
    [SerializeField, Min(0.1f)] private float _waypointReachDistance = 4f;

    [Header("Navegacao (NavMesh sem agents)")]
    [SerializeField, Min(0.05f)] private float _repathInterval = 0.25f;
    [SerializeField, Min(0.5f)] private float _navSampleDistance = 8f;
    [SerializeField] private int _areaMask = NavMesh.AllAreas;

    [Header("Desvio de outros jogadores")]
    [SerializeField, Min(0.5f)] private float _avoidRadius = 3.5f;
    [SerializeField, Min(0f)] private float _avoidMargin = 1f;
    [SerializeField, Min(1f)] private float _lookAheadDistance = 35f;
    [SerializeField, Min(0f)] private float _maxPredictTime = 1.5f;
    [SerializeField, Min(1)] private int _maxDetoursPerRepath = 3;

    [Header("Vida e impacto")]
    [SerializeField, Min(0.1f)] private float _lifetime = 12f;
    [SerializeField, Min(0.1f)] private float _noTargetLifetime = 3f;
    [SerializeField, Min(0f)] private float _ownerImmunityTime = 0.4f;
    [SerializeField, Min(0f)] private float _stunDuration = 1.5f;
    [SerializeField, Min(0f)] private float _stunSpinTurns = 2f;

    private readonly List<Vector3> _route = new List<Vector3>(32);
    private readonly Vector3[] _corners = new Vector3[MaxCorners];

    private NavMeshPath _path;
    private Rigidbody _body;
    private KartRacer _owner;
    private KartRacer _target;
    private int _routeIndex;
    private float _repathTimer;
    private float _age;
    private float _groundY;
    private bool _launched;
    private bool _consumed;

    public static RocketProjectile CreateDefault(Vector3 position, Quaternion rotation)
    {
        GameObject root = new GameObject("Rocket");
        root.transform.SetPositionAndRotation(position, rotation);

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Destroy(visual.GetComponent<Collider>());
        visual.transform.SetParent(root.transform, false);
        visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        visual.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);

        return root.AddComponent<RocketProjectile>();
    }

    private void Awake()
    {
        _path = new NavMeshPath();

        if (!TryGetComponent(out _body))
        {
            _body = gameObject.AddComponent<Rigidbody>();
        }

        _body.isKinematic = true;
        _body.useGravity = false;
        _body.interpolation = RigidbodyInterpolation.Interpolate;

        Collider col = GetComponentInChildren<Collider>();

        if (col == null)
        {
            SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
            sphere.radius = 0.8f;
            col = sphere;
        }

        col.isTrigger = true;
    }
    public void Launch(KartRacer owner, KartRacer target)
    {
        _owner = owner;
        _target = target;
        _groundY = transform.position.y - _hoverHeight;
        _repathTimer = 0f;
        _age = 0f;
        _launched = true;
    }

    private void FixedUpdate()
    {
        if (!_launched || _consumed)
        {
            return;
        }

        float dt = Time.fixedDeltaTime;
        _age += dt;

        bool hasTarget = HasValidTarget();

        if (_age >= _lifetime || (!hasTarget && _age >= _noTargetLifetime))
        {
            Destroy(gameObject);
            return;
        }

        if (hasTarget)
        {
            _repathTimer -= dt;

            if (_repathTimer <= 0f)
            {
                _repathTimer = _repathInterval;
                Repath();
            }
        }

        Move(dt, hasTarget);
    }

    private bool HasValidTarget()
    {
        return _target != null && _target.gameObject.activeInHierarchy && !_target.Finished;
    }


    private void Move(float dt, bool hasTarget)
    {
        Vector3 position = _body.position;
        Vector3 aim = transform.forward;

        if (hasTarget)
        {
            Vector3 goal = CurrentGoal(position);
            Vector3 toGoal = goal - position;
            toGoal.y = 0f;

            if (toGoal.sqrMagnitude > 0.01f)
            {
                aim = toGoal.normalized;
            }

            _groundY = Mathf.MoveTowards(_groundY, goal.y, 20f * dt);
        }

        aim.y = 0f;

        Quaternion desired = Quaternion.LookRotation(aim, Vector3.up);
        Quaternion rotation = Quaternion.RotateTowards(_body.rotation, desired, _turnSpeed * dt);

        Vector3 next = position + rotation * Vector3.forward * (_speed * dt);
        next.y = _groundY + _hoverHeight;

        _body.MoveRotation(rotation);
        _body.MovePosition(next);
    }

    private Vector3 CurrentGoal(Vector3 position)
    {
        float reachSqr = _waypointReachDistance * _waypointReachDistance;

        while (_routeIndex < _route.Count)
        {
            Vector3 toPoint = _route[_routeIndex] - position;
            toPoint.y = 0f;

            float sqr = toPoint.sqrMagnitude;
            bool reached = sqr <= reachSqr;
            bool passed = sqr <= reachSqr * 4f && Vector3.Dot(toPoint, transform.forward) < 0f;

            if (!reached && !passed)
            {
                return _route[_routeIndex];
            }

            _routeIndex++;
        }

        return _target.transform.position;
    }

    private void Repath()
    {
        _route.Clear();
        _routeIndex = 0;

        if (!NavMesh.SamplePosition(_body.position, out NavMeshHit from, _navSampleDistance, _areaMask)
            || !NavMesh.SamplePosition(_target.transform.position, out NavMeshHit to, _navSampleDistance, _areaMask))
        {
            return;
        }

        if (!NavMesh.CalculatePath(from.position, to.position, _areaMask, _path)
            || _path.status == NavMeshPathStatus.PathInvalid)
        {
            return;
        }

        int count = _path.GetCornersNonAlloc(_corners);

        for (int i = 1; i < count; i++)
        {
            _route.Add(_corners[i]);
        }

        AvoidOthers(from.position);
    }

    private void AvoidOthers(Vector3 start)
    {
        KartRacer[] racers = FindObjectsByType<KartRacer>(FindObjectsSortMode.None);

        Vector3 from = start;
        float travelled = 0f;
        int detours = 0;

        for (int i = 0; i < _route.Count && travelled < _lookAheadDistance && detours < _maxDetoursPerRepath; i++)
        {
            Vector3 to = _route[i];
            Vector3 segment = to - from;
            segment.y = 0f;

            float length = segment.magnitude;

            if (length < 0.01f)
            {
                from = to;
                continue;
            }

            Vector3 direction = segment / length;

            if (TryFindBlocker(racers, from, direction, length, travelled, out Vector3 blocker)
                && TryBuildDetour(from, direction, blocker, out Vector3 detour))
            {

                _route.Insert(i, detour);
                detours++;
                to = detour;
            }

            travelled += Vector3.Distance(from, to);
            from = to;
        }
    }

    private bool TryFindBlocker(KartRacer[] racers, Vector3 from, Vector3 direction, float length, float travelled, out Vector3 blocker)
    {
        blocker = default;
        float bestAlong = float.MaxValue;
        bool found = false;

        foreach (KartRacer racer in racers)
        {
            if (racer == null || racer == _target || racer.Finished || !racer.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 position = racer.transform.position;
            Rigidbody body = racer.Body != null ? racer.Body : racer.GetComponent<Rigidbody>();

            if (body != null)
            {
                float eta = Mathf.Min((travelled + Vector3.Distance(from, position)) / _speed, _maxPredictTime);
                Vector3 velocity = body.linearVelocity;
                velocity.y = 0f;
                position += velocity * eta;
            }

            Vector3 offset = position - from;
            offset.y = 0f;

            float along = Vector3.Dot(offset, direction);

            if (along <= 0f || along > length + _avoidRadius)
            {
                continue;
            }

            Vector3 closest = from + direction * Mathf.Min(along, length);
            Vector3 gap = position - closest;
            gap.y = 0f;

            if (gap.magnitude < _avoidRadius && along < bestAlong)
            {
                bestAlong = along;
                blocker = position;
                found = true;
            }
        }

        return found;
    }

    private bool TryBuildDetour(Vector3 from, Vector3 direction, Vector3 blocker, out Vector3 detour)
    {
        Vector3 right = Vector3.Cross(Vector3.up, direction);
        float side = Vector3.Dot(blocker - from, right) >= 0f ? -1f : 1f;

        for (int attempt = 0; attempt < 2; attempt++, side = -side)
        {
            Vector3 candidate = blocker + right * (side * (_avoidRadius + _avoidMargin));
            candidate.y = from.y;

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, _navSampleDistance, _areaMask))
            {
                continue;
            }

            Vector3 gap = hit.position - blocker;
            gap.y = 0f;

            if (gap.magnitude < _avoidRadius * 0.8f)
            {
                continue;
            }

            if (NavMesh.Raycast(from, hit.position, out _, _areaMask))
            {
                continue;
            }

            detour = hit.position;
            return true;
        }

        detour = default;
        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_launched || _consumed)
        {
            return;
        }

        KartRacer racer = other.GetComponentInParent<KartRacer>();

        if (racer == null || (racer == _owner && _age < _ownerImmunityTime))
        {
            return;
        }

        Hit(racer);
    }

    private void Hit(KartRacer racer)
    {
        _consumed = true;

        if (racer.TryGetComponent(out KartController kart))
        {
            kart.Stun(_stunDuration, _stunSpinTurns);
        }

        Destroy(gameObject);
    }
}