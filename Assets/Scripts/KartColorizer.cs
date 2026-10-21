using System.Collections.Generic;
using UnityEngine;

public class KartColorizer : MonoBehaviour
{
    [Header("Carroçaria")]
    [SerializeField] private Renderer[] _renderers;
    [SerializeField, Min(0)] private int _materialIndex = 0;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private readonly List<Material> _instances = new List<Material>();

    private void Reset()
    {
        List<Renderer> found = new List<Renderer>();

        foreach (Renderer candidate in GetComponentsInChildren<Renderer>())
        {
            if (candidate is MeshRenderer || candidate is SkinnedMeshRenderer)
            {
                found.Add(candidate);
            }
        }

        _renderers = found.ToArray();
    }

    private void OnDestroy()
    {
        foreach (Material material in _instances)
        {
            if (material != null)
            {
                Destroy(material);
            }
        }
    }

    public void SetColor(Color color)
    {
        if (_renderers == null)
        {
            return;
        }

        foreach (Renderer target in _renderers)
        {
            if (target == null)
            {
                continue;
            }

            Material[] materials = target.materials;
            _instances.AddRange(materials);

            if (_materialIndex >= materials.Length)
            {
                continue;
            }

            Material body = materials[_materialIndex];

            if (body.HasProperty(BaseColorId))
            {
                body.SetColor(BaseColorId, color);
            }
            else if (body.HasProperty(ColorId))
            {
                body.SetColor(ColorId, color);
            }
        }
    }
}