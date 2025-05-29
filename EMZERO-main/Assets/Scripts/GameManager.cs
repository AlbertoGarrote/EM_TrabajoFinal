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
    [SerializeField] GameObject humanPrefab, zombiePrefab, onlineInfoText, inputCodeObj;
    [SerializeField] TMP_InputField inputCode;
    public List<ulong> clientIds;
    TMP_Text onlinePlayerNumberInfo, onlineTypeInfo;
    public string clientName;
    private UniqueIdGenerator uniqueIdGenerator;
    public int minPlayerNumber = 4;

    bool serverStarted = false;
    bool thisClientStarted = false;
    bool thisClientHasName = false;

    public bool gameStarted = false;

    bool isStarted = false;
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

        _networkManager.OnClientConnectedCallback += OnPlayerConnect;
        _networkManager.OnClientDisconnectCallback += OnPlayerDisconnect;

        uniqueIdGenerator = GetComponent<UniqueIdGenerator>();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        onlineInfoText = GameObject.FindWithTag("onlineInfo");
        inputCodeObj = GameObject.FindWithTag("codeText"); // cuadro de texto codigo
        inputCode = inputCodeObj.GetComponentInChildren<TMP_InputField>();
        onlineTypeInfo = onlineInfoText.GetComponentsInChildren<TMP_Text>()[0];
        onlinePlayerNumberInfo = onlineInfoText.GetComponentsInChildren<TMP_Text>()[1];
        

        if (!isStarted)
        {
            onlineInfoText.SetActive(false);
            isStarted = true;
        }

        if (NetworkManager.Singleton.IsHost)
        {

            onlineTypeInfo.text = $"{clientName} [Servidor]";
            onlinePlayerNumberInfo.text = onlinePlayerNumberInfo.text = $"Jugadores: {clientIds.Count}/{minPlayerNumber}";

        }
        else
        {
            onlineTypeInfo.text = $"{clientName}";
            onlinePlayerNumberInfo.text = "Conectado!";
        }


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
                string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                Debug.Log(joinCode);
                RelayServerData relayServerData = new RelayServerData(allocation, "dtls");
                _networkManager.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);
                _networkManager.StartHost();
            }
            catch (RelayServiceException e)
            {
                Debug.Log(e);
            }

            onlineInfoText.SetActive(true);
            onlinePlayerNumberInfo.gameObject.SetActive(true);

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
            onlinePlayerNumberInfo.text = "";
            thisClientStarted = false;
        }
        if (_networkManager.IsServer)
        {
            _networkManager.Shutdown();
            Debug.Log("Se ha desconectado el servidor");
        }

    }

    public void OnPlayerConnect(ulong clientId)
    {
        if (_networkManager.IsServer)
        {
            AddClientToList(clientId);

            Debug.Log($"Se ha conectado el jugador: {clientId}");
            Debug.Log($"Numero de jugadores: {clientIds.Count}");

            onlinePlayerNumberInfo.text = $"Jugadores: {clientIds.Count}/{minPlayerNumber}";

            if (!thisClientHasName)
                CreateClientID(clientId);
        }
    }

    public void OnPlayerDisconnect(ulong clientId)
    {
        if (_networkManager.IsServer)
        {

            RemoveClientFromList(clientId);
            Debug.Log($"Se ha desconectado el jugador: {clientId}");
            Debug.Log($"Numero de jugadores: {clientIds.Count}");

            onlinePlayerNumberInfo.text = $"Jugadores: {clientIds.Count}/{minPlayerNumber}";

        }
    }


    void AddClientToList(ulong clientid)
    {
        if (NetworkManager.IsServer)
            clientIds.Add(clientid);
    }


    void RemoveClientFromList(ulong clientid)
    {
        if (NetworkManager.IsServer)
            clientIds.Remove(clientid);
    }

    public void CreateClientID(ulong targetClientId)
    {
        var clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { targetClientId }
            }
        };

        string clientName = uniqueIdGenerator.GenerateUniqueID();
        ConnectPlayerClientRpc(clientName, clientRpcParams);
    }

    [ClientRpc]
    private void ConnectPlayerClientRpc(string message, ClientRpcParams clientRpcParams = default)
    {
        if (!IsHost)
        {
            clientName = message;
            onlineTypeInfo.text = message;
            onlinePlayerNumberInfo.text = "Conectado!";
            thisClientStarted = true;
            thisClientHasName = true;
            onlineInfoText.SetActive(true);
        }

    }



}
