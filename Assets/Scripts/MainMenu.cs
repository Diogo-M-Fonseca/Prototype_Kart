using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header("Cenas")]
    [SerializeField] private string _singlePlayerSceneName = "SinglePlayer";
    [SerializeField] private string _localMultiplayerLobbySceneName = "LocalMultiplayerLobby";
 
    public void PlaySinglePlayer()
    {
        LocalMPSession.Clear();
        SceneManager.LoadScene(_singlePlayerSceneName);
    }
 
    public void OpenLocalMultiplayer()
    {
        SceneManager.LoadScene(_localMultiplayerLobbySceneName);
    }
 
    public void Quit()
    {
        Application.Quit();
    }
}
