
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
    [SerializeField] GameObject lobbyParent, layerGroup, playerInfoPrefab, hostButton, relay, optionsParent;
    [SerializeField] TMP_Text lobbyName, playerName;
    public List<GameObject> players;
    public UnityAction startHost;
    GameObject canvas;

    public delegate bool UpdateCondition();

    bool isMenuScene = true;
    bool isHosted = false;
    bool isWaiting = false;
    bool isReady = false;
    int playersReady = 0;
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {

        if (scene.name == "MenuScene")
        {
            isMenuScene = true;
            canvas.SetActive(true);
            if (NetworkManager.Singleton.IsClient)
            {
                lobbyParent.SetActive(true);

            }
            else
            {
                lobbyParent.SetActive(false);
                optionsParent.SetActive(false);
                lobbyName.gameObject.SetActive(false);
                hostButton.GetComponentInChildren<TMP_Text>().text = "HOST";
                ResetHostButton();
                Reset();
            }


        


      
            foreach (var p in players)
            {
                Destroy(p);
            }
            players.Clear();
            foreach (ulong id in GameManager.Instance.clientIds)
            {
                GameObject player = Instantiate(playerInfoPrefab, layerGroup.transform);
                player.GetComponentInChildren<TMP_Text>().text = GameManager.Instance.clientNames[id];
                players.Add(player);
            }
            if (NetworkManager.Singleton.IsHost)
                hostButton.GetComponentInChildren<TMP_Text>().text = $"JUGAR ({GameManager.Instance.clientIds.Count}/{GameManager.Instance.minPlayerNumber})";
        }
        else
        {
            isMenuScene = false;
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
        UpdateButton(hostButton.GetComponent<Button>(), UpdateHostButton);
    }

    public bool UpdateHostButton()
    {
        bool button = false;
        if (NetworkManager.Singleton.IsHost)
        {
            button = GameManager.Instance.clientIds.Count >= GameManager.Instance.minPlayerNumber
                && playersReady == GameManager.Instance.clientIds.Count-1;
        }
        else
        {
            button = !isWaiting;
        }
        return button;
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
        if (NetworkManager.Singleton.IsServer)
            hostButton.GetComponentInChildren<TMP_Text>().text = $"JUGAR ({GameManager.Instance.clientIds.Count}/{GameManager.Instance.minPlayerNumber})";

        GameObject player = Instantiate(playerInfoPrefab, layerGroup.transform);
        player.GetComponentInChildren<TMP_Text>().text = name;

        players.Add(player);
    }

    public void RemovePlayerFromLobby(string name)
    {
        if (isMenuScene)
        {
            GameObject player = players.Find(p => p.GetComponentInChildren<TMP_Text>().text == name);
            if (player != null)
            {
                players.Remove(player);
                Destroy(player);
            }
            if (NetworkManager.Singleton.IsServer)
                hostButton.GetComponentInChildren<TMP_Text>().text = $"JUGAR ({GameManager.Instance.clientIds.Count}/{GameManager.Instance.minPlayerNumber})";
            Debug.Log($"no se encontró al jugador: {name}");
        }
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
        optionsParent.SetActive(true);
        lobbyName.gameObject.SetActive(true);
        hostButton.GetComponentInChildren<TMP_Text>().text = $"JUGAR ({GameManager.Instance.clientIds.Count}/{GameManager.Instance.minPlayerNumber})";
        hostButton.GetComponent<Button>().onClick.RemoveAllListeners();
        hostButton.GetComponent<Button>().onClick.AddListener(StartGame);
        relay.SetActive(false);
        isHosted = true;
    }

    public void StartClientButton()
    {
        isWaiting = true;
        lobbyName.gameObject.SetActive(true);
        hostButton.GetComponentInChildren<TMP_Text>().text = "Esperando al host";
        //hostButton.GetComponent<Button>().interactable = false;
        relay.GetComponentInChildren<TMP_InputField>().gameObject.SetActive(false);
        relay.GetComponentInChildren<Button>().GetComponentInChildren<TMP_Text>().text = "NO LISTO";
        relay.GetComponentInChildren<Button>().onClick.RemoveAllListeners();
        relay.GetComponentInChildren<Button>().onClick.AddListener(PlayerReadyToggle);
    }

    public void PlayerReadyToggle()
    {
        if(!isReady)
        {
            //Mandar "Listo" a servidor
            PlayerReadyServerRpc(true);
            isReady = true;
            relay.GetComponentInChildren<Button>().GetComponentInChildren<TMP_Text>().text = "LISTO";
        }
        else
        {
            //Mandar "No Listo" al servidor
            PlayerReadyServerRpc(false);
            isReady = false;
            relay.GetComponentInChildren<Button>().GetComponentInChildren<TMP_Text>().text = "NO LISTO";
        }
    }

    [ServerRpc]
    public void PlayerReadyServerRpc(bool isReady)
    {
      
        if (isReady)
        {
            playersReady++;
        }
        else
        {
            playersReady--;
        }
        Debug.Log($"Jugadores listos {playersReady}");

    }

    public void ResetHostButton()
    {

        //hostButton.GetComponent<Button>().interactable = true;
        isWaiting = false;
        hostButton.GetComponentInChildren<TMP_Text>().text = "HOST";
        //hostButton.GetComponentInChildren<TMP_Text>().text = $"JUGAR ({GameManager.Instance.clientIds.Count}/{GameManager.Instance.minPlayerNumber})";
        hostButton.SetActive(true);
        relay.SetActive(true);
        hostButton.GetComponent<Button>().onClick.RemoveAllListeners();
        hostButton.GetComponent<Button>().onClick.AddListener(startHost);
    }

    public void Reset()
    {
        //lobbyParent.gameObject.SetActive(false);
        startButton.interactable = true;
        startButton.enabled = true;
    }

    public void Disconnect()
    {
        foreach (var p in players)
        {
            Destroy(p);
        }
        players.Clear();
        ResetHostButton();
        lobbyName.gameObject.SetActive(false);
        optionsParent.SetActive(false);
        playerName.GetComponentInChildren<TMP_InputField>().interactable = true;
    }
}
