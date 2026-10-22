using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class KartItemHud : MonoBehaviour
{
    [Header("Item atual (o proximo a ser usado)")]
    [SerializeField] private Image _currentIcon;
    [SerializeField] private TMP_Text _currentLabel;

    [Header("Segundo item")]
    [SerializeField] private GameObject _nextSlot;
    [SerializeField] private Image _nextIcon;

    private KartItemHolder _holder;

    public void Bind(KartItemHolder holder)
    {
        Unsubscribe();
        _holder = holder;

        if (_holder == null)
        {
            Refresh(null);
            return;
        }

        _holder.ItemsChanged += Refresh;
        Refresh(_holder.Items);
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (_holder != null)
        {
            _holder.ItemsChanged -= Refresh;
        }

        _holder = null;
    }

    private void Refresh(IReadOnlyList<KartItem> items)
    {
        KartItem current = items != null && items.Count > 0 ? items[0] : null;
        KartItem next = items != null && items.Count > 1 ? items[1] : null;

        Sprite currentSprite = ItemIconRenderer.GetIcon(current);

        if (_currentIcon != null)
        {
            _currentIcon.sprite = currentSprite;
            _currentIcon.enabled = currentSprite != null;
        }

        if (_currentLabel != null)
        {
            _currentLabel.text = current != null && currentSprite == null ? current.DisplayName : string.Empty;
        }

        Sprite nextSprite = ItemIconRenderer.GetIcon(next);

        if (_nextSlot != null)
        {
            _nextSlot.SetActive(next != null);
        }

        if (_nextIcon != null)
        {
            _nextIcon.sprite = nextSprite;
            _nextIcon.enabled = nextSprite != null;
        }
    }
}