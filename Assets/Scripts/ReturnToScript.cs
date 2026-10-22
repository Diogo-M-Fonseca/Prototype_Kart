using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class ReturnToScript : MonoBehaviour
{
    [Header("Cena")]
    [SerializeField] private string _menuSceneName = "MainMenu";

    private bool _loading;

    private void Update()
    {
        if (_loading)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;

        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            BackToMenu();
        }
    }

    public void BackToMenu()
    {
        if (_loading)
        {
            return;
        }

        _loading = true;
        Time.timeScale = 1f;
        LocalMPSession.Clear();
        SceneManager.LoadScene(_menuSceneName);
    }
}
