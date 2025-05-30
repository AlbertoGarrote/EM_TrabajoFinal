using System.Runtime.CompilerServices;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    public GameObject pausePanel; // Asigna el panel desde el inspector

    private bool isPaused = false;
    
    private void OnEnable()
    {
        GameManager.OnHostResume += HandleHostResume;
    }
    void Update()
    {
        // Detecta si el jugador presiona la tecla Escape o Pausa
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (GameManager.Instance.hostPaused) { return; }
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
     
        if (NetworkManager.Singleton.IsHost)
        {
            GameManager.Instance.PauseGameClientRpc();
            isPaused = true;
        }
        else if(!GameManager.Instance.hostPaused)
        {
            isPaused = true;
            pausePanel.GetComponentsInChildren<TMP_Text>(true)[0].text = "PAUSA";

            pausePanel.GetComponentsInChildren<Button>(true)[0].interactable = true;

            pausePanel.SetActive(true); // Muestra el panel de pausa
            Time.timeScale = 0f; // Detiene el tiempo en el juego

            // Gestión del cursor
            Cursor.lockState = CursorLockMode.None; // Desbloquea el cursor
            Cursor.visible = true; // Hace visible el cursor
        }


    }

    public void ResumeGame()
    {
     
        if (NetworkManager.Singleton.IsHost)
        {
            GameManager.Instance.ResumeGameClientRpc();
            isPaused = false;

        }
        else if(!GameManager.Instance.hostPaused)
        {
            pausePanel.SetActive(false); // Oculta el panel de pausa
            Time.timeScale = 1f; // Reactiva el tiempo en el juego
            isPaused = false;

            // Gestión del cursor
            Cursor.lockState = CursorLockMode.Locked; // Bloquea el cursor
            Cursor.visible = false; // Oculta el cursor
        }
    }

    public void QuitGame()
    {
        // Opcional: Asegúrate de que el tiempo está restaurado antes de salir
        Time.timeScale = 1f;
        
        GameManager.Instance.disconectSelf();
        MenuManager.Instance.ResetHostButton();
        SceneManager.LoadScene("MenuScene"); // Cambia "MainMenu" por el nombre de tu escena principal
    }
    private void HandleHostResume()
    {
        isPaused = false;
    }
}
