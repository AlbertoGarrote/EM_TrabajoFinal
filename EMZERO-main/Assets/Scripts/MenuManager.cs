using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
    public static MenuManager Instance;
    [SerializeField] Button startButton;
    public delegate bool UpdateCondition();
    //public Action startGame;
    public void Awake()
    {
        Time.timeScale = 1f; // Asegúrate de que el tiempo está restaurado al cargar la escena


    }

    public void StartGame()
    {
        NetworkManager.Singleton.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
        //startGame.Invoke();
        // Cambia "MainScene" por el nombre de tu escena principal
        //Inicia la partida para todos los clientes
        //Compart el mapa a todos los clientes
        
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // Salir en el editor
#else
            Application.Quit(); // Salir en una build
#endif
    }

    public void UpdateButton(Button b,UpdateCondition updateCondition)
    {
        if (updateCondition())
        {
            b.interactable = true;
        }
        else
        {
            b.interactable = false;
        }
    }

    public void Update()
    {
        UpdateButton(startButton, ()=> GameManager.Instance.clientIds.Count >= GameManager.Instance.minPlayerNumber);
    }
}
