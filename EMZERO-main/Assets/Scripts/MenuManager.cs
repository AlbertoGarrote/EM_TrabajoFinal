using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    public static MenuManager Instance;
    [SerializeField] int minPlayerNumber = 0;
    //public Action startGame;
    public void Awake()
    {
        Time.timeScale = 1f; // Asegúrate de que el tiempo está restaurado al cargar la escena
    }
   
    public void StartGame()
    {
        if (Instance == null)
        {
            Instance = new MenuManager();
        }
        else
        {
            Destroy(this);
        }

        if (NetworkManager.Singleton.IsServer && GameManager.Instance.clientIds.Count >= minPlayerNumber)
        {
            NetworkManager.Singleton.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
            //startGame.Invoke();
            // Cambia "MainScene" por el nombre de tu escena principal
            //Inicia la partida para todos los clientes
            //Compart el mapa a todos los clientes
        }
        
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // Salir en el editor
#else
            Application.Quit(); // Salir en una build
#endif
    }
}
