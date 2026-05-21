using UnityEngine;

public class MazeSceneManager : MonoBehaviour
{

    [SerializeField] private GameObject gameOverUI;
    [SerializeField] private GameObject gameClearUI;

    public void showGameOver()
    {

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        gameOverUI.SetActive(true);
    }

    public void showGameClear()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        gameClearUI.SetActive(true);
    }

    public void ReturnToMainMenu()
    {
        // Load the main menu scene
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    public void RestartMaze()
    {
        // Reload the current maze scene
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

}
