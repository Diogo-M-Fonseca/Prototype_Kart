using UnityEngine;


public abstract class KartItem : ScriptableObject
{
    [SerializeField] private string _displayName = "Item";
    [SerializeField] private GameObject _iconPrefab;
    [SerializeField] private Sprite _icon;

    public string DisplayName => _displayName;
    public Sprite Icon => _icon;
    public virtual GameObject IconPrefab => _iconPrefab;

    public virtual GameObject CreateFallbackModel() => null;

    public abstract void Use(KartItemHolder holder);
}
