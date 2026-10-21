using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class LocalMPManager : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private KartController _kartPrefab;
    [SerializeField] private KartCamera _cameraPrefab;

    [Header("Spawn")]
    [SerializeField] private Transform[] _spawnPoints;

    [Header("Jogadores")]
    [SerializeField, Range(1, 4)] private int _maxPlayers = 4;
    [SerializeField, Range(1, 4)] private int _minPlayersToStart = 2;
    [SerializeField] private bool _allowKeyboard = true;

    private readonly List<InputDevice> _joinedDevices = new List<InputDevice>();
    private readonly List<InputActionAsset> _playerActions = new List<InputActionAsset>();
    private bool _playing;

    private void Update()
    {
        if (_playing)
        {
            return;
        }

        foreach (Gamepad pad in Gamepad.all)
        {
            if (pad.startButton.wasPressedThisFrame)
            {
                HandleJoinOrStart(pad);
            }

            if (_playing)
            {
                return;
            }
        }

        Keyboard keyboard = Keyboard.current;
        if (_allowKeyboard && keyboard != null && keyboard.enterKey.wasPressedThisFrame)
        {
            HandleJoinOrStart(keyboard);
        }
    }

    private void OnDestroy()
    {
        foreach (InputActionAsset actions in _playerActions)
        {
            if (actions != null)
                Destroy(actions);
        }
    }

    private void HandleJoinOrStart(InputDevice device)
    {
        if (_joinedDevices.Contains(device))
        {
            if (_joinedDevices.Count >= _minPlayersToStart)
            {
                StartRace();
            }
            return;
        }

        if (_joinedDevices.Count < _maxPlayers)
        {
            _joinedDevices.Add(device);
        }
    }

    private void StartRace()
    {
        _playing = true;
        int count = _joinedDevices.Count;

        for (int i = 0; i < count; i++)
            SpawnPlayer(i, count, _joinedDevices[i]);
    }

    private void SpawnPlayer(int index, int playerCount, InputDevice device)
    {
        Transform spawn = _spawnPoints[index % _spawnPoints.Length];
        KartController kart = Instantiate(_kartPrefab, spawn.position, spawn.rotation);

        InputActionAsset actions = Instantiate(InputSystem.actions);
        actions.devices = new InputDevice[] { device };
        actions.Enable();
        _playerActions.Add(actions);

        kart.GetComponent<KartInputReader>().BindActions(actions);

        KartCamera kartCamera = Instantiate(_cameraPrefab);
        kartCamera.SetTarget(kart.GetComponent<Rigidbody>());

        Camera cam = kartCamera.GetComponent<Camera>();
        cam.rect = GetViewport(index, playerCount);

        AudioListener listener = kartCamera.GetComponent<AudioListener>();
        if (listener != null)
            listener.enabled = index == 0;
    }

    private static Rect GetViewport(int index, int playerCount)
    {
        if (playerCount == 1)
        {
            return new Rect(0f, 0f, 1f, 1f);
        }

        if (playerCount == 2)
        {
            return new Rect(0f, index == 0 ? 0.5f : 0f, 1f, 0.5f);
        }

        int column = index % 2;
        int row = index / 2;
        return new Rect(column * 0.5f, row == 0 ? 0.5f : 0f, 0.5f, 0.5f);
    }

    private void OnGUI()
    {
        if (_playing)
            return;

        GUILayout.BeginArea(new Rect(20f, 20f, 400f, 200f));
        GUILayout.Label($"Jogadores: {_joinedDevices.Count}/{_maxPlayers}");
        GUILayout.Label("Start (comando) ou Enter (teclado) para entrar.");

        if (_joinedDevices.Count >= _minPlayersToStart)
        {
            GUILayout.Label("Um jogador que já entrou carrega outra vez no seu botão para começar.");
        }
        else
            GUILayout.Label($"Mínimo de {_minPlayersToStart} jogadores para começar.");

        foreach (InputDevice device in _joinedDevices)
            GUILayout.Label("• " + device.displayName);

        GUILayout.EndArea();
    }
}
