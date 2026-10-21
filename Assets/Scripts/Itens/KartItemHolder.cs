using System;
using System.Collections.Generic;
using UnityEngine;


public class KartItemHolder : MonoBehaviour
{
    public const int MaxItems = 2;

    [Header("Testes")]
    [Tooltip("Itens com que o kart arranca. Util para testar sem apanhar caixas.")]
    [SerializeField] private KartItem[] _startingItems;

    private readonly List<KartItem> _items = new List<KartItem>(MaxItems);
    private KartInputReader _input;
    private KartRacer _racer;

    public event Action<IReadOnlyList<KartItem>> ItemsChanged;

    public IReadOnlyList<KartItem> Items => _items;
    public bool IsFull => _items.Count >= MaxItems;

    public KartRacer Racer
    {
        get
        {
            if (_racer == null)
            {
                TryGetComponent(out _racer);
            }

            return _racer;
        }
    }

    private void Awake()
    {
        TryGetComponent(out _input);

        if (_startingItems == null)
        {
            return;
        }

        foreach (KartItem item in _startingItems)
        {
            TryAddItem(item);
        }
    }

    private void Update()
    {
        if (_input != null && _input.UseItemPressed)
        {
            UseNext();
        }
    }

    public bool TryAddItem(KartItem item)
    {
        if (item == null || IsFull)
        {
            return false;
        }

        _items.Add(item);
        ItemsChanged?.Invoke(_items);
        return true;
    }

    public bool UseNext()
    {
        if (_items.Count == 0)
        {
            return false;
        }

        KartItem item = _items[0];
        _items.RemoveAt(0);
        ItemsChanged?.Invoke(_items);

        item.Use(this);
        return true;
    }
}
