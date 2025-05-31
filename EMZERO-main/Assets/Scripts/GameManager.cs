using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine.UI;
using Unity.Collections;
using System;
using System.Linq;
using Newtonsoft.Json.Linq;

public class GameManager : NetworkBehaviour
{
    /*
    o Gestionar la conexión de los jugadores: implementar métodos para la
    conexión y desconexión de los jugadores.

    o La asignación de equipos: asignar los jugadores al conectarse a los
    personajes de los equipos humano o zombi.

    o La sincronización de los estados del juego: garantizar que las posiciones y
    estados de los jugadores se sincronicen entre todos los clientes.

    o Sincronización de eventos del juego: recolección de monedas, conversión
    de humano a zombi y condiciones de fin de juego.
    */

    // Start is called before the first frame update
    public static GameManager Instance { get; private set; }
    [SerializeField] NetworkManager _networkManager;
    [SerializeField] GameObject humanPrefab, zombiePrefab, inputCodeObj;
    [SerializeField] TMP_InputField inputCode;
    [SerializeField] MenuManager menu;
    public List<ulong> clientIds;
    public Dictionary<ulong, string> clientNames;
    public string clientName;
    private UniqueIdGenerator uniqueIdGenerator;
    public int minPlayerNumber = 4;

    bool serverStarted = false;
    bool thisClientStarted = false;
    bool thisClientHasName = false;

    public bool gameStarted = false;

    bool isStarted = false;

    string joinCode;
    GameObject pausePanel;
    public bool hostPaused;
    public static event Action OnHostResume;

    public Action onHostDisconnect;

    public int roomNumber;
    public bool modeCoins = true;
    public int coinDensity;
    [SerializeField] GameObject coins, rooms;
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        clientIds = new List<ulong>();
        clientNames = new Dictionary<ulong, string>();

        _networkManager.OnClientConnectedCallback += OnPlayerConnect;
        _networkManager.OnClientDisconnectCallback += OnPlayerDisconnect;
        Application.quitting += disconectSelf;

        uniqueIdGenerator = GetComponent<UniqueIdGenerator>();
        menu.startHost = startServer;
        SceneManager.sceneLoaded += OnSceneLoaded;


        OptionsHandleCoins();
        OptionsHandleRooms();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {

        //inputCodeObj = GameObject.FindWithTag("codeText"); // cuadro de texto codigo
        //inputCode = inputCodeObj.GetComponentInChildren<TMP_InputField>();
        if (scene.name == "GameScene")
        {
            pausePanel = GameObject.FindWithTag("pausePanel");
            pausePanel.SetActive(false);
        }


        if (!isStarted)
        {
            isStarted = true;
        }

        //Si la red no está iniciada, la lista se vacía (para evitar que haya clientes en el lobby si hubo una dexconexion
        if (!NetworkManager.IsClient)
            clientIds.Clear();

    }

    // Update is called once per frame
    void Update()
    {

    }

    // RELAY
    public async void startServer()
    {
        if (!serverStarted)
        {
            //clientName = uniqueIdGenerator.GenerateUniqueID();
            //_networkManager.StartHost();
            //serverStarted = true;
            //Debug.Log($"Iniciado el servidor");
            //onlineTypeInfo.text = $"{clientName} [Servidor]";

            try
            {
                await UnityServices.InitializeAsync();

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(10); // Este es el numero maximo de jugadores
                joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                Debug.Log(joinCode);
                RelayServerData relayServerData = new RelayServerData(allocation, "dtls");
                _networkManager.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);
                _networkManager.StartHost();
            }
            catch (RelayServiceException e)
            {
                Debug.Log(e);
            }


            clientName = uniqueIdGenerator.GenerateUniqueID();
            if (!clientNames.ContainsKey(0)) clientNames.Add(0, clientName);
            menu.ChangeLobbyName(clientName, joinCode);
            menu.StartHostButton();
            menu.ChangePlayerName(clientName, true);
            menu.addPlayerToLobby(clientName);

        }

    }

    public void startClient()
    {
        if (!thisClientStarted)
        {
            JoinRelay(inputCode.text);

        }
    }

    public async void JoinRelay(string joinCode)
    {
        try
        {
            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
            Debug.Log("Uniendose al Relay " + joinCode);
            JoinAllocation joinallocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            RelayServerData relayServerData = new RelayServerData(joinallocation, "dtls");
            _networkManager.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);
            _networkManager.StartClient();
        }
        catch (RelayServiceException e)
        {
            Debug.Log(e);
        }
    }


    public void disconectSelf()
    {

        if (_networkManager.IsClient)
        {

            _networkManager.Shutdown();

            thisClientStarted = false;
        }
        if (_networkManager.IsServer)
        {
            if (onHostDisconnect != null)
                onHostDisconnect();
            clientIds.Clear();
            clientNames.Clear();
            if (SceneManager.GetActiveScene().name == "MenuScene")
            {
                menu.Disconnect();
            }
            //_networkManager.Shutdown();
            Debug.Log("Se ha desconectado el servidor");
        }


    }

    public void OnPlayerConnect(ulong clientId)
    {
        if (_networkManager.IsServer)
        {
            var clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { clientId }
                }
            };

            var clientIdsCopy = clientIds.ToList();
            foreach (var id in clientIdsCopy)
            {
                AddClientToListClientRpc(id, clientRpcParams);
            }
            AddClientToListClientRpc(clientId);

            Debug.Log($"Se ha conectado el jugador: {clientId}");
            Debug.Log($"Numero de jugadores: {clientIds.Count}");


            if (!clientNames.ContainsKey(clientId) && clientId != 0)
            {
                string clientName = CreateClientID(clientId);
                //menu.addPlayerToLobby(clientName);
                AddPlayerClientRpc(clientName, clientId);
            }



        }
    }

    public void OnPlayerDisconnect(ulong clientId)
    {
        if (_networkManager.IsHost)
        {
            RemoveClientFromListClientRpc(clientId);
            Debug.Log($"Se ha desconectado el jugador: {clientId}");
            Debug.Log($"Numero de jugadores: {clientIds.Count}");


        }
        if (!IsHost && clientId == _networkManager.LocalClientId)
        {
            if (onHostDisconnect != null)
                onHostDisconnect(); //cuando el jugador se desconecta del host

            clientIds.Clear();
            clientNames.Clear();
            if (SceneManager.GetActiveScene().name == "MenuScene")
            {
                menu.Disconnect();
            }

        }
        RemovePlayerClientRpc(clientNames[clientId]);

    }


    [ClientRpc]
    void AddClientToListClientRpc(ulong clientid, ClientRpcParams clientRpcParams = default)
    {
        clientIds.Add(clientid);
    }

    [ClientRpc]
    void ClearClientListsClientRpc()
    {
        clientIds.Clear();
    }

    [ClientRpc]
    void RemoveClientFromListClientRpc(ulong clientid)
    {
        clientIds.Remove(clientid);
    }


    public string CreateClientID(ulong targetClientId)
    {
        var clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { targetClientId }
            }
        };

        string clientName = uniqueIdGenerator.GenerateUniqueID();
        List<FixedString128Bytes> clientNamesParameter = new List<FixedString128Bytes>();
        foreach (GameObject player in menu.players)
        {
            clientNamesParameter.Add(player.GetComponentInChildren<TMP_Text>().text);
        }
        ConnectPlayerClientRpc(clientName, clientNamesParameter.ToArray(), clientIds.ToArray(), clientNames[0], clientRpcParams);
        return clientName;
    }

    [ClientRpc]
    private void ConnectPlayerClientRpc(string message, FixedString128Bytes[] currentPlayers, ulong[] ids, string lobbyName, ClientRpcParams clientRpcParams = default)
    {
        if (!IsHost)
        {
            clientName = message;
            thisClientStarted = true;
            thisClientHasName = true;
            for (int i = 0; i < currentPlayers.Length; i++)
            {
                menu.addPlayerToLobby(currentPlayers[i].ToString());
                clientNames.Add(ids[i], currentPlayers[i].ToString());
            }
            menu.StartClientButton();
            menu.ChangePlayerName(clientName, false);
            menu.ChangeLobbyName(lobbyName);
        }

    }


    [ClientRpc]
    private void RemovePlayerClientRpc(string name)
    {
        menu.RemovePlayerFromLobby(name);
        clientNames.Remove(clientNames.FirstOrDefault(pair => pair.Value == name).Key);
    }

    [ClientRpc]
    private void AddPlayerClientRpc(string name, ulong id)
    {
        menu.addPlayerToLobby(name);
        clientNames.Add(id, name);
    }

    [ClientRpc]
    public void PauseGameClientRpc()
    {
        hostPaused = true;
        pausePanel.GetComponentsInChildren<TMP_Text>()[0].text = "EL HOST HA PAUSADO EL JUEGO";
        if (!IsHost)
        {
            pausePanel.GetComponentsInChildren<Button>()[0].interactable = false;
        }
        pausePanel.SetActive(true); // Muestra el panel de pausa
        Time.timeScale = 0f; // Detiene el tiempo en el juego

        // Gestión del cursor
        Cursor.lockState = CursorLockMode.None; // Desbloquea el cursor
        Cursor.visible = true; // Hace visible el cursor
    }

    [ClientRpc]
    public void ResumeGameClientRpc()
    {
        hostPaused = false;
        OnHostResume?.Invoke();
        pausePanel.SetActive(false); // Oculta el panel de pausa

        Time.timeScale = 1f; // Reactiva el tiempo en el juego

        // Gestión del cursor
        Cursor.lockState = CursorLockMode.Locked; // Bloquea el cursor
        Cursor.visible = false; // Oculta el cursor
    }

    public void OptionsHandleCoins()
    {
        coinDensity = (int)coins.GetComponentInChildren<Slider>().value;
        coins.GetComponentsInChildren<TMP_Text>()[1].text = coinDensity + "%";
    }

    public void OptionsHandleRooms()
    {
        roomNumber = (int)rooms.GetComponentInChildren<Slider>().value;
        rooms.GetComponentsInChildren<TMP_Text>()[1].text = "" + roomNumber;
    }

    public void ChangeGameMode(bool modeIsCoins)
    {
        modeCoins = modeIsCoins;
    }
}
