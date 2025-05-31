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

    //Instancia de Singleton
    public static GameManager Instance { get; private set; }

    //Variables de acceso a elementos de la escena
    [SerializeField] NetworkManager _networkManager;
    [SerializeField] GameObject humanPrefab, zombiePrefab, inputCodeObj;
    [SerializeField] TMP_InputField inputCode;
    [SerializeField] MenuManager menu;

    //Lista de clientes (id)
    public List<ulong> clientIds;

    //Diccionario de nombres (id-nombre)
    [SerializeField] public Dictionary<ulong, string> clientNames;
    public string clientName;
    private UniqueIdGenerator uniqueIdGenerator;

    //control
    bool serverStarted = false;
    bool thisClientStarted = false;
    bool thisClientHasName = false;
    public NetworkVariable<bool> gameStarted = new NetworkVariable<bool>(false);

    //relay
    string joinCode;

    //Gestión de desconxiones y pausa
    public static event Action OnHostResume;
    public Action onHostDisconnect;
    GameObject pausePanel;
    public bool hostPaused;

    //ajustes de partida
    public int minPlayerNumber = 4;
    public int roomNumber;
    public bool modeCoins = true;
    public int coinDensity, totalTime;
    [SerializeField] GameObject coins, rooms, timeSlider;

    //selección de nombre en el lobby
    public GameObject nameInput;
    public TMP_InputField nameInputField;

    //lógica del boton de "listo"
    public int playersReady;
    public Dictionary<ulong, bool> playersReadyDictionary = new Dictionary<ulong, bool>();

    void Awake()
    {
        //Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        //Inicializacion de variables
        clientIds = new List<ulong>();
        clientNames = new Dictionary<ulong, string>();

        //Observers
        _networkManager.OnClientConnectedCallback += OnPlayerConnect;
        _networkManager.OnClientDisconnectCallback += OnPlayerDisconnect;
        Application.quitting += disconectSelf;
        SceneManager.sceneLoaded += OnSceneLoaded;
        menu.startHost = startServer;

        uniqueIdGenerator = GetComponent<UniqueIdGenerator>();


        //interfaz ajustes de partida
        modeCoins = true;
        OptionsHandleCoins();
        OptionsHandleRooms();
        OptionsHandleTime();

    }

    //CADA VEZ QUE SE CAMBIA DE ESCENA
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {

        
        if (scene.name == "GameScene")
        {
            pausePanel = GameObject.FindWithTag("pausePanel");
            pausePanel.SetActive(false);

            //si ya hay red, se toma el nombre del inputfield
            if (IsClient)
            {
                if (nameInputField.text == "")
                {
                    clientName = uniqueIdGenerator.GenerateUniqueID();
                    nameInputField.text = clientName;
                }
            }

            }
        else
        {
            nameInput = GameObject.FindWithTag("nameText");
            nameInputField = nameInput.GetComponentInChildren<TMP_InputField>();
        }

        //Si la red no está iniciada, la lista se vacía (para evitar que haya clientes en el lobby si hubo una dexconexion
        if (!NetworkManager.IsClient)
            clientIds.Clear();

    }


    // RELAY
    public async void startServer()
    {
        if (!serverStarted)
        {

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

            if (nameInputField.text == "")
            {
                clientName = uniqueIdGenerator.GenerateUniqueID();
                nameInputField.text = clientName;
            }
            else
                clientName = nameInputField.text;

            nameInputField.interactable = false;

            if (!clientNames.ContainsKey(0)) clientNames.Add(0, clientName);
            menu.ChangeLobbyName(clientName, joinCode);
            menu.StartHostButton();
            menu.addPlayerToLobby(clientName);

        }

    }
    public void startClient()
    {
        if (!gameStarted.Value)
            JoinRelay(inputCode.text);
        else
            Debug.LogWarning("No se puede entrar a una partida en curso");
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

    //SUSCRITO A APPLICATION.QUIT. SE LLAMA PARA DESCONECTARSE DE LA RED
    public void disconectSelf()
    {

        if (_networkManager.IsClient)
        {

            _networkManager.Shutdown();

            thisClientStarted = false;
        }
        if (_networkManager.IsServer)
        {
            //Si se cierra el host se elimina la información de la partida

            if (onHostDisconnect != null)
                onHostDisconnect();

            clientIds.Clear();
            clientNames.Clear();
            playersReady = 0;
            playersReadyDictionary.Clear();
            if (SceneManager.GetActiveScene().name == "MenuScene")
            {
                menu.Disconnect();
            }
            //_networkManager.Shutdown();
            Debug.Log("Se ha desconectado el servidor");
        }


    }

    //Se llama en el servidor cada vez que se conecta un jugador
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

            //Se añaden al clientList del cliente que se acaba de conectar todos los ids que ya estaban conectados
            var clientIdsCopy = clientIds.ToList();
            foreach (var id in clientIdsCopy)
            {
                AddClientToListClientRpc(id, clientRpcParams);
            }

            //Se añade el nuevo id a todos los clientList
            AddClientToListClientRpc(clientId);

            Debug.Log($"Se ha conectado el jugador: {clientId}");
            Debug.Log($"Numero de jugadores: {clientIds.Count}");


            //Se añade el nuevo jugador al diccionario de nombres, tanto del servidor como del propio cliente
            //(el diccionario y la lista de ids se tienen que mantener siempre sincronizados para gestionar
            //el lobby)
            if (!clientNames.ContainsKey(clientId) && clientId != 0)
            {
                CreateClientID(clientId);
                nameInputField.interactable = false;
            }

            //Se añade el nuevo jugador como "No listo"
            playersReadyDictionary.Add(clientId, false);

        }
    }

    //Se llama cada vez que un jugador se desconecta
    public void OnPlayerDisconnect(ulong clientId)
    {
        if (IsHost && clientId != 0)
        {
            //Se borra el id de todas las listas
            RemoveClientFromListClientRpc(clientId);
            //Se borra el nombre de todos los diccionarios
            if (clientNames.ContainsKey(clientId))
                RemovePlayerClientRpc(clientNames[clientId]);

            Debug.Log($"Se ha desconectado el jugador: {clientId}");
            Debug.Log($"Numero de jugadores: {clientIds.Count}");

            //Si el jugador que se desconectó estaba listo, se resta 1 al número de listos
            if (playersReadyDictionary.ContainsKey(clientId))
            {
                if (playersReadyDictionary[clientId])
                {
                    playersReady--;
                }
                playersReadyDictionary.Remove(clientId); //se borra del diccionario de listos
            }
            else
            {
                Debug.Log($"no se encontró {clientId} en el diccionario de listos");
            }
            menu.ShowReadyPlayers();
        }
        //Se llama en el cliente cuando este se desconecta del host. También cuando el host se desconecta 
        //y tira a todos los clientes

        if (!IsHost && clientId == _networkManager.LocalClientId) 
        {
            if (onHostDisconnect != null)
                onHostDisconnect(); //Se llama en levelManager. Lanza un Game Over especifico si el host se desconecta

            //Vaciar las estructuras de id al desconectarse de la red
            clientIds.Clear();
            clientNames.Clear();

            //Mostrar la desconexión en el lobby
            if (SceneManager.GetActiveScene().name == "MenuScene")
            {
                menu.Disconnect();
            }
            menu.isReady = false;

        }


    }


    //MÉTODOS PARA ACTUALIZAR LA LISTA DE IDS
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


    //Gestiona la conexión de un nuevo cliente. Le pasa todos los nombres del diccionario conectados hasta
    //el momento y le manda asignarse un nombre
    public void CreateClientID(ulong targetClientId)
    {
        var clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { targetClientId }
            }
        };

        //Se convierte la lista de String a una clase INetworkSerializable -> FixedString128Bytes[]
        List<FixedString128Bytes> clientNamesParameter = new List<FixedString128Bytes>();
        foreach (GameObject player in menu.players)
        {
            clientNamesParameter.Add(player.GetComponentInChildren<TMP_Text>().text);
        }
        ConnectPlayerClientRpc(clientNamesParameter.ToArray(), clientIds.ToArray(), clientNames[0], clientRpcParams);
    }

    //Como el nombre tiene que depender del campo text del inputField del cliente, hay que obtenerlo de un rpc.
    //Si el cliente no tiene nombre, se le asigna uno automático. Cuando el cliente ha terminado de "decidir"
    //su nombre, manda un ServerRpc con él
    [ClientRpc]
    private void ConnectPlayerClientRpc(FixedString128Bytes[] currentPlayers, ulong[] ids, string lobbyName, ClientRpcParams clientRpcParams = default)
    {
        if (!IsHost)
        {
            if (nameInputField.text == "")
            {
                clientName = uniqueIdGenerator.GenerateUniqueID();
            }
            else
            {
                clientName = nameInputField.text;
            }
            nameInputField.text = clientName;
            RegisterNameServerRpc(clientName, NetworkManager.Singleton.LocalClientId);
            thisClientStarted = true;
            thisClientHasName = true;
            for (int i = 0; i < currentPlayers.Length; i++)
            {
                menu.addPlayerToLobby(currentPlayers[i].ToString());
                clientNames.Add(ids[i], currentPlayers[i].ToString());
                Debug.Log(ids[i] + currentPlayers[i].ToString());
            }
            menu.StartClientButton();
            menu.ChangeLobbyName(lobbyName);
        }

    }

    //Avisa al servidor de que ya ha "decidido" el nombre, y manda ser registrado en el 
    //diccionario de nombres
    [ServerRpc(RequireOwnership = false)]
    private void RegisterNameServerRpc(string name, ulong id)
    {
        clientNames.Add(id, name);
        menu.addPlayerToLobby(name);
        AddPlayerClientRpc(name, id);
    }

    //MÉTODOS PARA SINCRONIZAR EL DICCIONARIO DE NOMBRES (Y EL LOBBY)
    [ClientRpc]
    private void AddPlayerClientRpc(string name, ulong id)
    {
        if (!IsHost)
        {
            menu.addPlayerToLobby(name);
            clientNames.Add(id, name);
            Debug.Log(id + name);
        }
    }

    [ClientRpc]
    private void RemovePlayerClientRpc(string name)
    {
        menu.RemovePlayerFromLobby(name);
        clientNames.Remove(clientNames.FirstOrDefault(pair => pair.Value == name).Key);
    }


    //MÉTODOS PARA GESTIONAR PAUSE Y RESUME

    [ClientRpc]
    public void PauseGameClientRpc()
    {
        hostPaused = true;
        pausePanel.GetComponentsInChildren<TMP_Text>()[0].text = "EL HOST HA PAUSADO EL JUEGO";
        if (!IsHost)
        {
            pausePanel.GetComponentsInChildren<Button>()[0].interactable = false; //Si el host pausa el juego, se le pausa a todos los clientes, y ninguno puede darle a "resume"
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
        OnHostResume?.Invoke(); //Cuando el host despausa el juego, todos los clientes lo hacen
        pausePanel.SetActive(false); // Oculta el panel de pausa

        Time.timeScale = 1f; // Reactiva el tiempo en el juego

        // Gestión del cursor
        Cursor.lockState = CursorLockMode.Locked; // Bloquea el cursor
        Cursor.visible = false; // Oculta el cursor
    }

    //MÉTODOS DE LOS AJUSTES DE PARTIDA EN EL LOBBY
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

    public void OptionsHandleTime()
    {
        totalTime = (int)timeSlider.GetComponentInChildren<Slider>().value;
        timeSlider.GetComponentsInChildren<TMP_Text>()[1].text = "" + totalTime;
    }

    public void ChangeGameMode(bool modeIsCoins)
    {
        modeCoins = modeIsCoins;
    }

    //Avisa al servidor de que un jugador está lista
    [ServerRpc(RequireOwnership = false)]
    public void PlayerReadyServerRpc(bool isReady, ulong id)
    {
        if (IsHost)
        {
            if (isReady)
            {
                playersReady++;
                playersReadyDictionary[id] = true;
            }
            else
            {
                playersReady--;
                playersReadyDictionary[id] = false;
            }
            menu.ShowReadyPlayers();
            Debug.Log($"Jugadores listos {playersReady}");
        }
    }

}
