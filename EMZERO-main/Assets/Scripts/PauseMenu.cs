using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public GameObject pausePanel; // Asigna el panel desde el inspector

    private bool isPaused = false;

    void Update()
    {
        // Detecta si el jugador presiona la tecla Escape o Pausa
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    public void PauseGame()
    {
        isPaused = true;
        GameManager.Instance.PauseGameClientRpc();
    }

    public void ResumeGame()
    {
        isPaused = false;
        GameManager.Instance.ResumeGameClientRpc();
    }

    public void QuitGame()
    {
        // Opcional: Asegúrate de que el tiempo está restaurado antes de salir
        Time.timeScale = 1f;
        GameManager.Instance.disconectSelf();
        MenuManager.Instance.ResetHostButton();
        SceneManager.LoadScene("MenuScene"); // Cambia "MainMenu" por el nombre de tu escena principal
    }
}
