
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static UnityEngine.EventSystems.StandaloneInputModule;

public class MenuManager : MonoBehaviour
{
    public static MenuManager Instance;
    [SerializeField] Button startButton;
    [SerializeField] GameObject lobbyParent, layerGroup, playerInfoPrefab, hostButton, relay;
    [SerializeField] TMP_Text lobbyName, playerName;
    public List<GameObject> players;
    public UnityAction startHost;
    GameObject canvas;

    public delegate bool UpdateCondition();

    public void Start()
    {
        lobbyParent.SetActive(false);
        lobbyName.gameObject.SetActive(false);
        playerName.gameObject.SetActive(false);
        hostButton.GetComponentInChildren<TMP_Text>().text = "HOST";
        ResetHostButton();


    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {

        if (scene.name == "MenuScene")
        {

            canvas.SetActive(true);
            Reset();
        }
        else
        {
            canvas.SetActive(false);
        }

    }

    //public Action startGame;
    public void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        canvas = GetComponent<Canvas>().gameObject;
        Time.timeScale = 1f; // Asegúrate de que el tiempo está restaurado al cargar la escena
        players = new List<GameObject>();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public void StartGame()
    {
        NetworkManager.Singleton.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);

        //ResetHostButton(startHost);
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

    public void UpdateButton(Button b, UpdateCondition updateCondition)
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
        /*UpdateButton(startButton, 
            () => GameManager.Instance.clientIds.Count >= GameManager.Instance.minPlayerNumber && NetworkManager.Singleton.IsServer
            );
        */

    }

    public void showLobby()
    {
        startButton.enabled = false;
        lobbyParent.SetActive(true);

    }

    public void exitLobby()
    {
        startButton.enabled = true;
        lobbyParent.SetActive(false);
    }

    public void addPlayerToLobby(string name)
    {
        GameObject player = Instantiate(playerInfoPrefab, layerGroup.transform);
        player.GetComponentInChildren<TMP_Text>().text = name;
        players.Add(player);
    }

    public void RemovePlayerFromLobby(string name)
    {
        GameObject player = players.Find(p => p.GetComponent<TMP_Text>().text == name);
        players.Remove(player);
        Destroy(player);
    }

    public void ChangeLobbyName(string name)
    {
        lobbyName.text = "Lobby de " + name;
    }

    public void ChangeLobbyName(string name, string code)
    {
        lobbyName.text = $"Lobby de {name} [{code}]";
    }

    public void StartHostButton()
    {
        lobbyName.gameObject.SetActive(true);
        hostButton.GetComponentInChildren<TMP_Text>().text = "JUGAR";
        hostButton.GetComponent<Button>().onClick.RemoveAllListeners();
        hostButton.GetComponent<Button>().onClick.AddListener(StartGame);
        relay.SetActive(false);
    }

    public void StartClientButton()
    {
        lobbyName.gameObject.SetActive(true);
        hostButton.GetComponentInChildren<TMP_Text>().text = "Esperando al host";
        hostButton.GetComponent<Button>().interactable = false;
        relay.SetActive(false);
    }


    public void ResetHostButton()
    {
        hostButton.GetComponent<Button>().interactable = true;
        hostButton.GetComponentInChildren<TMP_Text>().text = "HOST";
        hostButton.SetActive(true);
        relay.SetActive(true);
        hostButton.GetComponent<Button>().onClick.RemoveAllListeners();
        hostButton.GetComponent<Button>().onClick.AddListener(startHost);
    }

    public void Reset()
    {
        lobbyParent.gameObject.SetActive(false);
        startButton.interactable = true;
        startButton.enabled = true;
    }

    public void ChangePlayerName(string name, bool host)
    {
        playerName.gameObject.SetActive(true);
        playerName.text = $"Tu nombre: {name}";
        if(host)
        {
            playerName.text += " [host]";
        }
    }
}
