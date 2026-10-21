using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class LocalMPLobby : MonoBehaviour
{
    [Header("Cenas")]
    [SerializeField] private string _gameSceneName = "Game";
    [SerializeField] private string _menuSceneName = "MainMenu";

    [Header("UI")]
    [Tooltip("Um texto por slot de jogador (até 4).")]
    [SerializeField] private TMP_Text[] _slotLabels;
    [SerializeField] private TMP_Text _hintLabel;

    [Header("Jogadores")]
    [SerializeField, Range(1, 4)] private int _minPlayersToStart = 2;
    [SerializeField] private bool _allowKeyboard = true;

    private bool _loading;

    private void OnEnable()
    {
        LocalMPSession.Clear();
        InputSystem.onDeviceChange += OnDeviceChange;
        Refresh();
    }

    private void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    private void Update()
    {
        if (_loading)
        {
            return;
        }

        foreach (Gamepad pad in Gamepad.all)
        {
            if (pad.startButton.wasPressedThisFrame)
            {
                JoinOrStart(pad);
            }
            else if (pad.buttonEast.wasPressedThisFrame)
            {
                Leave(pad);
            }

            if (_loading)
            {
                return;
            }
        }

        Keyboard keyboard = Keyboard.current;
        if (!_allowKeyboard || keyboard == null)
        {
            return;
        }

        if (keyboard.enterKey.wasPressedThisFrame)
        {
            JoinOrStart(keyboard);
        }
        else if (keyboard.escapeKey.wasPressedThisFrame)
        {
            Leave(keyboard);
        }
    }

    public void BackToMenu()
    {
        if (_loading)
        {
            return;
        }

        _loading = true;
        LocalMPSession.Clear();
        SceneManager.LoadScene(_menuSceneName);
    }

    private void JoinOrStart(InputDevice device)
    {
        if (LocalMPSession.Contains(device))
        {
            if (LocalMPSession.Count >= _minPlayersToStart)
            {
                StartGame();
            }

            return;
        }

        if (LocalMPSession.TryAdd(device))
        {
            Refresh();
        }
    }

    private void Leave(InputDevice device)
    {
        if (LocalMPSession.Remove(device))
        {
            Refresh();
            return;
        }

        if (LocalMPSession.Count == 0)
        {
            BackToMenu();
        }
    }

    private void StartGame()
    {
        _loading = true;
        SceneManager.LoadScene(_gameSceneName);
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (change != InputDeviceChange.Disconnected && change != InputDeviceChange.Removed)
        {
            return;
        }

        if (LocalMPSession.Remove(device))
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        int count = LocalMPSession.Count;

        if (_slotLabels != null)
        {
            for (int i = 0; i < _slotLabels.Length; i++)
            {
                if (_slotLabels[i] == null)
                {
                    continue;
                }

                _slotLabels[i].text = i < count ? $"Jogador {i + 1}: {GetDeviceName(LocalMPSession.Players[i])}" : $"Jogador {i + 1}: carrega em Start / Enter para entrar";

                _slotLabels[i].color = i < count ? LocalMPSession.GetPlayerColor(i) : Color.white;
            }
        }

        if (_hintLabel == null)
        {
            return;
        }

        _hintLabel.text = count >= _minPlayersToStart ? "Um jogador carrega outra vez em Start / Enter para começar.   B / Esc para sair." : $"São precisos pelo menos {_minPlayersToStart} jogadores.   B / Esc para sair.";
    }

    private static string GetDeviceName(InputDevice device)
    {
        return device is Keyboard ? "Teclado" : device.displayName;
    }
}