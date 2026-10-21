using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ItemBox : MonoBehaviour
{
    [SerializeField] private KartItem[] _items;
    [SerializeField, Min(0f)] private float _respawnTime = 3f;

    private Renderer[] _renderers;
    private Collider _collider;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _collider.isTrigger = true;
        _renderers = GetComponentsInChildren<Renderer>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_items == null || _items.Length == 0)
        {
            return;
        }

        KartRacer racer = other.GetComponentInParent<KartRacer>();

        if (racer == null)
        {
            return;
        }

        if (!racer.TryGetComponent(out KartItemHolder holder))
        {
            holder = racer.gameObject.AddComponent<KartItemHolder>();
        }

        if (holder.IsFull)
        {
            return;
        }

        if (holder.TryAddItem(_items[Random.Range(0, _items.Length)]))
        {
            SetAvailable(false);
            Invoke(nameof(Respawn), _respawnTime);
        }
    }

    private void Respawn() => SetAvailable(true);

    private void SetAvailable(bool available)
    {
        _collider.enabled = available;

        foreach (Renderer r in _renderers)
        {
            r.enabled = available;
        }
    }
}
