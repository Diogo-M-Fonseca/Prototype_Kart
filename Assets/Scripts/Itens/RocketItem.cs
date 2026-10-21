using UnityEngine;


[CreateAssetMenu(menuName = "Kart/Items/Rocket", fileName = "RocketItem")]
public class RocketItem : KartItem
{
    [Tooltip("Opcional. Se vazio, e criado um foguete simples (capsula) em runtime.")]
    [SerializeField] private RocketProjectile _rocketPrefab;
    [SerializeField, Min(0f)] private float _spawnDistance = 2.5f;
    [SerializeField, Min(0f)] private float _spawnHeight = 0.7f;

    public override void Use(KartItemHolder holder)
    {
        KartRacer owner = holder.Racer;
        Transform kart = holder.transform;

        Vector3 forward = Vector3.ProjectOnPlane(kart.forward, Vector3.up).normalized;
        Vector3 position = kart.position + forward * _spawnDistance + Vector3.up * _spawnHeight;
        Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);

        RocketProjectile rocket = _rocketPrefab != null
            ? Instantiate(_rocketPrefab, position, rotation)
            : RocketProjectile.CreateDefault(position, rotation);

        rocket.Launch(owner, FindTargetAhead(owner));
    }

    private static KartRacer FindTargetAhead(KartRacer owner)
    {
        if (owner == null || owner.Position <= 1)
        {
            return null;
        }

        foreach (KartRacer racer in FindObjectsByType<KartRacer>(FindObjectsSortMode.None))
        {
            if (racer != owner
                && racer.Registered
                && !racer.Finished
                && racer.gameObject.activeInHierarchy
                && racer.Position == owner.Position - 1)
            {
                return racer;
            }
        }

        return null;
    }
}
