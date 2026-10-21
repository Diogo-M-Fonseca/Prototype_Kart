using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class LocalMPManager : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private KartController _kartPrefab;
    [Tooltip("Prefab com Camera + KartCamera (+ AudioListener).")]
    [SerializeField] private KartCamera _cameraPrefab;

    [Header("Spawn")]
    [SerializeField] private Transform[] _spawnPoints;

    [Header("Testes")]
    [Tooltip("Permite carregar a cena diretamente (sem lobby) com um jogador.")]
    [SerializeField] private bool _fallbackWithoutLobby = true;

    private readonly List<InputActionAsset> _playerActions = new List<InputActionAsset>();

    private void Start()
    {
        IReadOnlyList<InputDevice> devices = LocalMPSession.Players;

        if (devices.Count == 0)
        {
            if (_fallbackWithoutLobby)
            {
                SpawnPlayer(0, 1, null);
            }
            else
            {
                Debug.LogError("Nenhum jogador na sessão. Passa pela scene de lobby primeiro.", this);
            }

            return;
        }

        int count = devices.Count;

        for (int i = 0; i < count; i++)
        {
            SpawnPlayer(i, count, devices[i]);
        }
    }

    private void OnDestroy()
    {
        foreach (InputActionAsset actions in _playerActions)
        {
            if (actions != null)
            {
                Destroy(actions);
            }
        }
    }

    private void SpawnPlayer(int index, int playerCount, InputDevice device)
    {
        if (_kartPrefab == null || _cameraPrefab == null || _spawnPoints == null || _spawnPoints.Length == 0)
        {
            Debug.LogError("LocalMPManager não está configurado (prefabs ou pontos de spawn em falta).", this);
            return;
        }

        Transform spawn = _spawnPoints[index % _spawnPoints.Length];
        KartController kart = Instantiate(_kartPrefab, spawn.position, spawn.rotation);

        InputActionAsset actions = Instantiate(InputSystem.actions);
        actions.devices = device != null ? new InputDevice[] { device } : null;
        actions.Enable();
        _playerActions.Add(actions);

        kart.GetComponent<KartInputReader>().BindActions(actions);

        if (kart.TryGetComponent(out KartColorizer colorizer))
        {
            colorizer.SetColor(LocalMPSession.GetPlayerColor(index));
        }
        else
        {
            Debug.LogWarning("O prefab do kart não tem KartColorizer; a cor do jogador não foi aplicada.", kart);
        }

        KartCamera kartCamera = Instantiate(_cameraPrefab);
        kartCamera.SetTarget(kart.GetComponent<Rigidbody>());

        Camera cam = kartCamera.GetComponent<Camera>();
        cam.rect = GetViewport(index, playerCount);

        AudioListener listener = kartCamera.GetComponent<AudioListener>();
        if (listener != null)
        {
            listener.enabled = index == 0;
        }
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
}
