using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
public static class LocalMPSession
{
    public const int MaxPlayers = 4;

    private static readonly List<InputDevice> _players = new List<InputDevice>();

    private static readonly Color[] _playerColors =
    {
        new Color(0.90f, 0.15f, 0.15f),
        new Color(0.15f, 0.40f, 0.95f),
        new Color(0.20f, 0.75f, 0.25f),
        new Color(0.60f, 0.25f, 0.85f),
    };

    public static IReadOnlyList<InputDevice> Players => _players;
    public static int Count => _players.Count;

    public static Color GetPlayerColor(int index) => _playerColors[index % _playerColors.Length];

    public static bool Contains(InputDevice device) => _players.Contains(device);

    public static bool TryAdd(InputDevice device)
    {
        if (device == null || _players.Count >= MaxPlayers || _players.Contains(device))
            return false;

        _players.Add(device);
        return true;
    }

    public static bool Remove(InputDevice device) => _players.Remove(device);

    public static void Clear() => _players.Clear();
}
