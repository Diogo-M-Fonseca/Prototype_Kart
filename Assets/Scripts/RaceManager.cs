using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RaceManager : MonoBehaviour
{
    public static RaceManager Instance { get; private set; }

    [Header("Pista")]
    [SerializeField] private RaceCheckpoint[] _checkpoints;
    [SerializeField, Min(1)] private int _totalLaps = 3;

    [Header("Interface (um HUD por jogador, já criado na cena)")]
    [SerializeField] private RacerHud[] _huds;

    [Header("Fim da corrida")]
    [SerializeField] private string _menuSceneName = "MainMenu";
    [SerializeField, Min(0f)] private float _exitDelay = 1.5f;

    [Header("Queda da pista")]
    [SerializeField, Min(0.5f)] private float _fallRespawnTime = 2f;
    [SerializeField, Min(1f)] private float _fallDepth = 40f;
    [SerializeField, Min(0f)] private float _respawnHeight = 1f;

    private sealed class RacerView
    {
        public KartRacer Racer;
        public KartCamera Camera;
        public RacerHud Hud;
    }

    private readonly List<RacerView> _views = new List<RacerView>();
    private float _startTime;
    private int _finishedCount;
    private float _allFinishedTime = -1f;
    private bool _warnedInvalidTrack;
    private bool _warnedMissingHud;
    private float _lowestCheckpointY = float.NegativeInfinity;

    public int TotalLaps => _totalLaps;

    private void Awake()
    {
        Instance = this;
        _startTime = Time.time;

        if (_checkpoints != null)
        {
            _lowestCheckpointY = float.PositiveInfinity;

            for (int i = 0; i < _checkpoints.Length; i++)
            {
                if (_checkpoints[i] == null)
                    continue;

                _checkpoints[i].Index = i;
                _lowestCheckpointY = Mathf.Min(_checkpoints[i].transform.position.y, _lowestCheckpointY);
            }

            if (float.IsPositiveInfinity(_lowestCheckpointY))
                _lowestCheckpointY = float.NegativeInfinity;
        }

        if (_huds != null)
        {
            foreach (RacerHud hud in _huds)
            {
                if (hud != null)
                    hud.gameObject.SetActive(false);
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Register(KartRacer racer, int playerIndex, Color color, KartCamera kartCamera, Rect viewport)
    {
        if (!HasValidTrack())
        {
            if (!_warnedInvalidTrack)
            {
                Debug.LogWarning("RaceManager: são precisos pelo menos 2 checkpoints (o primeiro é a linha de meta). A corrida está desativada.", this);
                _warnedInvalidTrack = true;
            }

            return;
        }

        racer.Init(playerIndex, color, 1);

        if (!racer.TryGetComponent(out KartItemHolder itemHolder))
        {
            itemHolder = racer.gameObject.AddComponent<KartItemHolder>();
        }

        RacerHud hud = _huds != null && playerIndex < _huds.Length ? _huds[playerIndex] : null;

        if (hud != null)
        {
            hud.Bind(viewport, color);
            hud.BindItems(itemHolder);
        }
        else if (!_warnedMissingHud)
        {
            Debug.LogWarning("RaceManager: falta o HUD de um jogador. Usa \"Criar / atualizar RaceManager\" no tool de setup para criar o canvas na cena.", this);
            _warnedMissingHud = true;
        }

        _views.Add(new RacerView { Racer = racer, Camera = kartCamera, Hud = hud });
    }

    private bool HasValidTrack()
    {
        if (_checkpoints == null || _checkpoints.Length < 2)
        {
            return false;
        }

        return _checkpoints.All(c => c != null);
    }

    public void OnCheckpointHit(KartRacer racer, int index)
    {
        if (!racer.Registered || racer.Finished || index != racer.NextCheckpoint)
        {
            return;
        }

        racer.CheckpointsPassed++;
        racer.NextCheckpoint = (index + 1) % _checkpoints.Length;

        Transform checkpoint = _checkpoints[index].transform;
        racer.SetRespawnPoint(
            checkpoint.position + Vector3.up * _respawnHeight,
            Quaternion.Euler(0f, checkpoint.eulerAngles.y, 0f));

        if (index != 0)
        {
            return;
        }

        racer.LapsCompleted++;

        if (racer.LapsCompleted >= _totalLaps)
        {
            Finish(racer);
        }
    }

    private void Finish(KartRacer racer)
    {
        racer.Finished = true;
        racer.FinishPlace = ++_finishedCount;
        racer.FinishTime = Time.time - _startTime;
        racer.Position = racer.FinishPlace;

        RacerView view = _views.Find(v => v.Racer == racer);

        if (view != null && view.Camera != null)
        {
            view.Camera.enabled = false;
        }

        racer.gameObject.SetActive(false);

        if (_finishedCount >= _views.Count)
        {
            _allFinishedTime = Time.time;
        }
    }

    private void Update()
    {
        if (_views.Count == 0)
        {
            return;
        }

        CheckFalls();
        UpdateRanking();
        RefreshUi();

        bool allFinished = _allFinishedTime >= 0f;

        if (allFinished && Time.time - _allFinishedTime >= _exitDelay && ExitPressed())
        {
            SceneManager.LoadScene(_menuSceneName);
        }
    }

    private void CheckFalls()
    {
        foreach (RacerView view in _views)
        {
            KartRacer racer = view.Racer;

            if (racer.Finished || !racer.gameObject.activeInHierarchy)
            {
                continue;
            }

            bool airborneTooLong = racer.AirborneTime >= _fallRespawnTime;
            bool belowTrack = racer.transform.position.y < _lowestCheckpointY - _fallDepth;

            if (airborneTooLong || belowTrack)
            {
                Respawn(view);
            }
        }
    }

    private static void Respawn(RacerView view)
    {
        KartRacer racer = view.Racer;
        Rigidbody body = racer.Body;

        racer.transform.SetPositionAndRotation(racer.RespawnPosition, racer.RespawnRotation);

        if (body != null)
        {
            body.position = racer.RespawnPosition;
            body.rotation = racer.RespawnRotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        racer.ResetAirborne();

        if (view.Camera != null)
        {
            view.Camera.SnapToTarget();
        }
    }

    private void UpdateRanking()
    {
        List<KartRacer> ordered = _views.Select(v => v.Racer).OrderBy(r => r.Finished ? 0 : 1).ThenBy(r => r.Finished ? r.FinishPlace : 0).ThenByDescending(r => r.Finished ? 0f : r.GetProgress(_checkpoints)).ToList();

        for (int i = 0; i < ordered.Count; i++)
        {
            ordered[i].Position = i + 1;
        }
    }

    private void OnDrawGizmos()
    {
        if (_checkpoints == null || _checkpoints.Length < 2)
        {
            return;
        }

        int count = _checkpoints.Length;
        Vector3 lift = Vector3.up * 0.5f;

        for (int i = 0; i < count; i++)
        {
            RaceCheckpoint a = _checkpoints[i];
            RaceCheckpoint b = _checkpoints[(i + 1) % count];

            if (a == null || b == null)
            {
                continue;
            }

            Vector3 from = a.transform.position + lift;
            Vector3 to = b.transform.position + lift;
            Vector3 delta = to - from;

            if (delta.sqrMagnitude < 1e-4f)
            {
                continue;
            }

            Gizmos.color = i == count - 1 ? new Color(1f, 0.9f, 0.1f) : new Color(0.2f, 1f, 0.4f);
            Gizmos.DrawLine(from, to);

            Vector3 direction = delta.normalized;
            Vector3 middle = (from + to) * 0.5f;
            Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;

            Gizmos.DrawLine(middle, middle - direction * 3f + side * 1.5f);
            Gizmos.DrawLine(middle, middle - direction * 3f - side * 1.5f);
        }
    }

    private static bool ExitPressed()
    {
        foreach (Gamepad pad in Gamepad.all)
        {
            if (pad.startButton.wasPressedThisFrame)
                return true;
        }

        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.enterKey.wasPressedThisFrame;
    }

    private void RefreshUi()
    {
        bool allFinished = _allFinishedTime >= 0f;
        bool showExitHint = allFinished && Time.time - _allFinishedTime >= _exitDelay;
        string rows = null;

        foreach (RacerView view in _views)
        {
            if (view.Hud == null)
            {
                continue;
            }

            KartRacer racer = view.Racer;

            if (!racer.Finished)
            {
                view.Hud.ShowRacing($"Volta {racer.CurrentLap(_totalLaps)}/{_totalLaps}", Ordinal(racer.Position));
                continue;
            }

            if (rows == null)
                rows = BuildRows();

            view.Hud.ShowResults($"Jogador {racer.PlayerIndex + 1}: {Ordinal(racer.FinishPlace)} lugar", rows, showExitHint);
        }
    }

    private string BuildRows()
    {
        StringBuilder builder = new StringBuilder();

        for (int place = 1; place <= _views.Count; place++)
        {
            KartRacer racer = _views.Select(v => v.Racer).FirstOrDefault(r => r.Finished && r.FinishPlace == place);

            if (racer != null)
            {
                string color = ColorUtility.ToHtmlStringRGB(racer.PlayerColor);
                builder.AppendLine($"{Ordinal(place)}   <color=#{color}>Jogador {racer.PlayerIndex + 1}</color>   {FormatTime(racer.FinishTime)}");
            }
            else
            {
                builder.AppendLine($"{Ordinal(place)}   <color=#777777>...</color>");
            }
        }

        return builder.ToString();
    }

    private static readonly string OrdinalSuffix = ((char)0x00BA).ToString();

    private static string Ordinal(int place) => place + OrdinalSuffix;

    private static string FormatTime(float seconds)
    {
        int minutes = (int)(seconds / 60f);
        float rest = seconds - minutes * 60f;
        return $"{minutes:00}:{rest:00.00}";
    }
}