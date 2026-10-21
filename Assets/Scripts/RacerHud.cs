using TMPro;
using UnityEngine;

public class RacerHud : MonoBehaviour
{
    [Header("Durante a corrida")]
    [SerializeField] private TMP_Text _lapLabel;
    [SerializeField] private TMP_Text _positionLabel;

    [Header("Painel de resultados (aparece quando o jogador acaba)")]
    [SerializeField] private GameObject _resultsPanel;
    [SerializeField] private TMP_Text _titleLabel;
    [SerializeField] private TMP_Text _rowsLabel;
    [SerializeField] private GameObject _exitHint;

    [Header("Cor do jogador")]
    [SerializeField] private bool _tintPosition = true;
    [SerializeField] private bool _tintTitle = true;

    private string _lapText;
    private string _positionText;
    private string _titleText;
    private string _rowsText;

    public void Bind(Rect viewport, Color playerColor)
    {
        RectTransform rect = (RectTransform)transform;
        rect.anchorMin = viewport.min;
        rect.anchorMax = viewport.max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        if (_tintPosition && _positionLabel != null)
        {
            _positionLabel.color = playerColor;
        }

        if (_tintTitle && _titleLabel != null)
        {
            _titleLabel.color = playerColor;
        }

        _lapText = null;
        _positionText = null;
        _titleText = null;
        _rowsText = null;

        gameObject.SetActive(true);
        SetActive(_resultsPanel, false);
    }

    public void ShowRacing(string lap, string position)
    {
        SetActive(_resultsPanel, false);
        SetActive(_lapLabel, true);
        SetActive(_positionLabel, true);

        SetText(_lapLabel, ref _lapText, lap);
        SetText(_positionLabel, ref _positionText, position);
    }

    public void ShowResults(string title, string rows, bool showExitHint)
    {
        SetActive(_lapLabel, false);
        SetActive(_positionLabel, false);
        SetActive(_resultsPanel, true);
        SetActive(_exitHint, showExitHint);

        SetText(_titleLabel, ref _titleText, title);
        SetText(_rowsLabel, ref _rowsText, rows);
    }

    private static void SetText(TMP_Text label, ref string cache, string value)
    {
        if (label == null || cache == value)
        {
            return;
        }

        cache = value;
        label.text = value;
    }

    private static void SetActive(Component component, bool active)
    {
        if (component != null)
        {
            SetActive(component.gameObject, active);
        }
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }
}
