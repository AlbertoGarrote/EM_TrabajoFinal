
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
    //GESTIONA EL MENU PRINCIPAL Y EL LOBBY
    //NO ES NETWORK OBJECT: EL GAMEMANAGER SE ENCARGA DE QUE TODOS LOS MENUMANAGER DE LOS CLIENTES RECIBAN LA MISMA INFORMACIÓN
    //Y SE ACTUALIZEN DE LA MISMA MANERA PARA MANTENERSE SINCRONIZADOS

    //Singleton
    public static MenuManager Instance;

    //Referencias a la escena
    [SerializeField] Button startButton;
    [SerializeField] GameObject lobbyParent, layerGroup, playerInfoPrefab, hostButton, relay, optionsParent;
    [SerializeField] TMP_Text lobbyName, playerName;

    //Lista de jugadores visibles en el lobby
    public List<GameObject> players;


    public UnityAction startHost;

    GameObject canvas;

    public delegate bool UpdateCondition();

    //Estados del manager
    bool isMenuScene = true;
    bool isHosted = false;
    bool isWaiting = false;
    public bool isReady = false;


    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        //Aunque el menuManager solo hace cosas en la escena del menu, se conserva (dontDestroyOnLoad) para mantener
        //la información entre partidas

        //Cuando entra a la escena del menu
        if (scene.name == "MenuScene")
        {
            isMenuScene = true;
            canvas.SetActive(true);

            //Si se vuelve al menú con una red aun activa (rejugar)
            if (NetworkManager.Singleton.IsClient)
            {
                lobbyParent.SetActive(true);
                ShowReadyPlayers();
                GameManager.Instance.gameStarted.Value = false;
            }
            else //Si se accede al menú sin una red activa, es decir, la primera vez o tras una desconexion
            {
                lobbyParent.SetActive(false);
                optionsParent.SetActive(false);
                lobbyName.gameObject.SetActive(false);
                hostButton.GetComponentInChildren<TMP_Text>().text = "HOST";
                ResetHostButton();
                ResetJoinButton();
                Reset();
            }

            //Cada vez que se vuelve a la escena del menú, se borran todos los jugadores del lobby y se vuelven a añadir teniendo
            //en cuenta los jugadores conectados actualmente en GameManager. De este modo también se contempla si un jugador
            //se desconectó durante la partida
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

            //Si se vuelve a la escena principal despues de una partida manteniendo la conexion, se muestra el boton de jugar
            if (NetworkManager.Singleton.IsHost)
                hostButton.GetComponentInChildren<TMP_Text>().text = $"JUGAR ({GameManager.Instance.clientIds.Count}/{GameManager.Instance.minPlayerNumber})";
        }
        else
        {
            isMenuScene = false;
            canvas.SetActive(false);
        }

    }

    public void Awake()
    {
        //Singleton
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

    //Lo llama el boton de empezar partida solo si se dan las condiciones adecuadas. Cambia la escena a todos los clientes
    public void StartGame()
    {
        GameManager.Instance.gameStarted.Value = true;
        NetworkManager.Singleton.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // Salir en el editor
#else
            Application.Quit(); // Salir en una build
#endif
    }

    //Mantiene un boton actualizado dada una condicion
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

    //Mantiene el boton de jugar pulsable solo si se es host, hay suficientes jugadores conectados, y todos están listos
    public bool UpdateHostButton()
    {
        bool button = false;
        if (NetworkManager.Singleton.IsHost)
        {
            button = GameManager.Instance.clientIds.Count >= GameManager.Instance.minPlayerNumber
                && GameManager.Instance.playersReady == GameManager.Instance.clientIds.Count - 1;
        }
        else
        {
            button = !isWaiting;
        }
        return button;
    }

    //Abrir el menu del lobby
    public void showLobby()
    {
        startButton.enabled = false;
        lobbyParent.SetActive(true);
        ResetJoinButton();
    }

    //Salir del menu del lobby
    public void exitLobby()
    {
        startButton.enabled = true;
        lobbyParent.SetActive(false);
    }

    //Añadir un jugador al lobby
    public void addPlayerToLobby(string name)
    {
        if (NetworkManager.Singleton.IsServer)
            hostButton.GetComponentInChildren<TMP_Text>().text = $"JUGAR ({GameManager.Instance.clientIds.Count}/{GameManager.Instance.minPlayerNumber})";

        GameObject player = Instantiate(playerInfoPrefab, layerGroup.transform);
        player.GetComponentInChildren<TMP_Text>().text = name;

        players.Add(player);
    }

    //Sacar un jugador del lobby
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

        }
    }

    //Cambiar el nombre del lobby
    public void ChangeLobbyName(string name)
    {
        lobbyName.text = "Lobby de " + name;
    }

    //Cambiar el nombre del lobby, indicando además que este es el host
    public void ChangeLobbyName(string name, string code)
    {
        lobbyName.text = $"Lobby de {name} [{code}]";
    }

    //Se llama al pulsar el boton de "HOST". Su funcionalidad cambia a la de empezar la partida
    public void StartHostButton()
    {
        optionsParent.SetActive(true);
        lobbyName.gameObject.SetActive(true);
        hostButton.GetComponentInChildren<TMP_Text>().text = $"JUGAR ({GameManager.Instance.clientIds.Count}/{GameManager.Instance.minPlayerNumber})";
        hostButton.GetComponent<Button>().onClick.RemoveAllListeners();
        hostButton.GetComponent<Button>().onClick.AddListener(StartGame);
        ShowReadyPlayers();
        relay.SetActive(false);
        isHosted = true;
    }

    //Se llama al pulsar el boton de "UNIRSE". Su funcionalidad cambia a la de "listo"
    public void StartClientButton()
    {
        isWaiting = true;
        lobbyName.gameObject.SetActive(true);
        hostButton.GetComponentInChildren<TMP_Text>().text = "Esperando al host";
        relay.GetComponentInChildren<TMP_InputField>().gameObject.SetActive(false);
        relay.GetComponentInChildren<Button>().GetComponentInChildren<TMP_Text>().text = "NO LISTO";
        relay.GetComponentInChildren<Button>().onClick.RemoveAllListeners();
        relay.GetComponentInChildren<Button>().onClick.AddListener(PlayerReadyToggle);
    }

    //Sirve para alternar entre "listo" y "no listo", notificando al host a través de gameManager 
    public void PlayerReadyToggle()
    {
        if (!isReady)
        {
            //Mandar "Listo" a servidor
            GameManager.Instance.PlayerReadyServerRpc(true, NetworkManager.Singleton.LocalClientId);
            isReady = true;
            relay.GetComponentInChildren<Button>().GetComponentInChildren<TMP_Text>().text = "LISTO";
        }
        else
        {
            //Mandar "No Listo" al servidor
            GameManager.Instance.PlayerReadyServerRpc(false, NetworkManager.Singleton.LocalClientId);
            isReady = false;
            relay.GetComponentInChildren<Button>().GetComponentInChildren<TMP_Text>().text = "NO LISTO";
        }
    }

    //Actualizar los jugadores listos de la interfaz del host
    public void ShowReadyPlayers()
    {
        optionsParent.GetComponentsInChildren<TMP_Text>()[0].text = $"JUGADORES LISTOS: {GameManager.Instance.playersReady}";
    }

    //Devuelve el boton de host a su funcionalidad original (iniciar el host) y vuelve a mostrar las opciones de unirse a partida

    public void ResetHostButton()
    {

        isWaiting = false;
        hostButton.GetComponentInChildren<TMP_Text>().text = "HOST";
        hostButton.SetActive(true);
        relay.SetActive(true);
        hostButton.GetComponent<Button>().onClick.RemoveAllListeners();
        hostButton.GetComponent<Button>().onClick.AddListener(startHost);

    }

    //Devuelve el boton de Join a su funcionalidad original de unirse a partidas
    public void ResetJoinButton()
    {
        GameManager.Instance.playersReady = 0;

        playerName.GetComponentInChildren<TMP_InputField>().interactable = true;
        playerName.GetComponentInChildren<TMP_InputField>().gameObject.SetActive(true);

        relay.GetComponentInChildren<Button>().onClick.RemoveAllListeners();
        relay.GetComponentInChildren<Button>().onClick.AddListener(GameManager.Instance.startClient);
        relay.GetComponentInChildren<Button>().GetComponentInChildren<TMP_Text>().text = "UNIRSE";
        relay.GetComponentInChildren<TMP_InputField>(true).gameObject.SetActive(true);
    }


    public void Reset()
    {
        startButton.interactable = true;
        startButton.enabled = true;
    }

    //"Limpia" todo el lobby cuando un cliente o host se desconectan de la red
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
        ResetJoinButton();
    }
}

