using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class ItemBox : MonoBehaviour
{
    [Header("Itens")]
    [Tooltip("Lista de itens possiveis. Cada vez que um jogador apanha a caixa recebe um destes ao acaso.")]
    [SerializeField] private KartItem[] _items;

    [Header("Animacao")]
    [Tooltip("Parte visual que encolhe ate desaparecer. Se vazio, encolhe o proprio objeto.")]
    [SerializeField] private Transform _visual;
    [SerializeField, Min(0.01f)] private float _shrinkDuration = 0.35f;
    [SerializeField, Min(0.01f)] private float _growDuration = 0.35f;

    [Header("Reaparecer")]
    [SerializeField] private bool _respawn = true;
    [SerializeField, Min(0f)] private float _respawnTime = 3f;

    private Collider _collider;
    private Renderer[] _renderers;
    private Vector3 _baseScale;
    private bool _available = true;
    private Coroutine _routine;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _collider.isTrigger = true;

        if (_visual == null)
        {
            _visual = transform;
        }

        _baseScale = _visual.localScale;
        _renderers = GetComponentsInChildren<Renderer>();
    }

    private void OnEnable()
    {
        Restore();
    }

    private void Start()
    {
        if (_items == null)
        {
            return;
        }

        foreach (KartItem item in _items)
        {
            ItemIconRenderer.GetIcon(item);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_available || _items == null || _items.Length == 0)
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

        KartItem item = PickRandom();

        if (item == null || !holder.TryAddItem(item))
        {
            return;
        }

        _available = false;
        _collider.enabled = false;

        if (_routine != null)
        {
            StopCoroutine(_routine);
        }

        _routine = StartCoroutine(PickupRoutine());
    }

    private KartItem PickRandom()
    {
        int count = 0;

        foreach (KartItem item in _items)
        {
            if (item != null)
            {
                count++;
            }
        }

        if (count == 0)
        {
            return null;
        }

        int pick = Random.Range(0, count);

        foreach (KartItem item in _items)
        {
            if (item == null)
            {
                continue;
            }

            if (pick-- == 0)
            {
                return item;
            }
        }

        return null;
    }

    private IEnumerator PickupRoutine()
    {
        yield return ScaleTo(1f, 0f, _shrinkDuration);
        SetRenderers(false);

        if (!_respawn)
        {
            yield break;
        }

        yield return new WaitForSeconds(_respawnTime);

        SetRenderers(true);
        yield return ScaleTo(0f, 1f, _growDuration);

        _collider.enabled = true;
        _available = true;
        _routine = null;
    }

    private IEnumerator ScaleTo(float from, float to, float duration)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(from, to, t / duration);
            _visual.localScale = _baseScale * k;
            yield return null;
        }

        _visual.localScale = _baseScale * to;
    }

    private void Restore()
    {
        if (_collider == null)
        {
            return;
        }

        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        _available = true;
        _collider.enabled = true;
        _visual.localScale = _baseScale;
        SetRenderers(true);
    }

    private void SetRenderers(bool visible)
    {
        foreach (Renderer r in _renderers)
        {
            if (r != null)
            {
                r.enabled = visible;
            }
        }
    }
}