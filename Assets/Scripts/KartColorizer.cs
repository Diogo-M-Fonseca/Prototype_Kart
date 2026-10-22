using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[ExecuteAlways]
public class KartColorizer : MonoBehaviour
{
    public enum ColorSource
    {
        Player1,
        Player2,
        Player3,
        Player4,
        Custom
    }

    [Header("Carroceria")]
    [SerializeField] private Renderer[] _renderers;
    [SerializeField, Min(0)] private int _materialIndex = 0;

    [Header("Decoracao (fora do jogo)")]
    [SerializeField] private bool _useDecorationColor = false;
    [SerializeField] private ColorSource _decorationPlayer = ColorSource.Player1;
    [FormerlySerializedAs("_decorationColor")]
    [SerializeField] private Color _customColor = Color.white;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private readonly List<Material> _instances = new List<Material>();

    private MaterialPropertyBlock _block;
    private bool _colorSetByCode;
    private bool _started;

    public Color DecorationColor => _decorationPlayer == ColorSource.Custom
        ? _customColor
        : LocalMPSession.GetPlayerColor((int)_decorationPlayer);

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

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            RefreshPreview();
        }
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            ClearPreview();
        }
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            RefreshPreview();
        }
        else if (_started && _useDecorationColor)
        {
            SetColor(DecorationColor);
        }
    }

    private void Start()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        _started = true;

        if (_useDecorationColor && !_colorSetByCode)
        {
            SetColor(DecorationColor);
        }
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

    public void SetPlayer(int playerIndex)
    {
        SetColor(LocalMPSession.GetPlayerColor(playerIndex));
    }

    public void SetColor(Color color)
    {
        _colorSetByCode = true;

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

    private void RefreshPreview()
    {
        if (_renderers == null)
        {
            return;
        }

        if (!_useDecorationColor)
        {
            ClearPreview();
            return;
        }

        if (_block == null)
        {
            _block = new MaterialPropertyBlock();
        }

        Color color = DecorationColor;

        foreach (Renderer target in _renderers)
        {
            if (target == null)
            {
                continue;
            }

            Material[] shared = target.sharedMaterials;

            if (_materialIndex >= shared.Length || shared[_materialIndex] == null)
            {
                continue;
            }

            Material body = shared[_materialIndex];
            target.GetPropertyBlock(_block, _materialIndex);

            if (body.HasProperty(BaseColorId))
            {
                _block.SetColor(BaseColorId, color);
            }
            else if (body.HasProperty(ColorId))
            {
                _block.SetColor(ColorId, color);
            }
            else
            {
                continue;
            }

            target.SetPropertyBlock(_block, _materialIndex);
        }
    }

    private void ClearPreview()
    {
        if (_renderers == null)
        {
            return;
        }

        foreach (Renderer target in _renderers)
        {
            if (target == null || _materialIndex >= target.sharedMaterials.Length)
            {
                continue;
            }

            target.SetPropertyBlock(null, _materialIndex);
        }
    }
}