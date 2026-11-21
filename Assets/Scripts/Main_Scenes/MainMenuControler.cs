using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    // Replace "HospitalLevel" with the exact name of your gameplay scene!
    [SerializeField] private string firstLevelSceneName = "Lobby";

    public void StartGame()
    {
        SceneManager.LoadScene(firstLevelSceneName);
    }

    public void QuitGame()
    {
        Debug.Log("Game Quit Triggered!"); // Shows in Editor since Application.Quit only closes built .exe files
        Application.Quit();
    }
}