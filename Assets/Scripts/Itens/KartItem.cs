using UnityEngine;


public abstract class KartItem : ScriptableObject
{
    [SerializeField] private string _displayName = "Item";
    [SerializeField] private Sprite _icon;

    public string DisplayName => _displayName;
    public Sprite Icon => _icon;

    public abstract void Use(KartItemHolder holder);
}
