using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    /*
    o Gestionar la conexi�n de los jugadores: implementar m�todos para la
    conexi�n y desconexi�n de los jugadores.

    o La asignaci�n de equipos: asignar los jugadores al conectarse a los
    personajes de los equipos humano o zombi.

    o La sincronizaci�n de los estados del juego: garantizar que las posiciones y
    estados de los jugadores se sincronicen entre todos los clientes.

    o Sincronizaci�n de eventos del juego: recolecci�n de monedas, conversi�n
    de humano a zombi y condiciones de fin de juego.
    */

    // Start is called before the first frame update
    public static GameManager Instance { get; private set; }

    [SerializeField] NetworkManager _networkManager;
    public NetworkVariable<int> playerNumber = new NetworkVariable<int>(0);
    [SerializeField] GameObject humanPrefab, zombiePrefab;
    public List<ulong> clientIds;
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
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void startServer()
    {
        
        _networkManager.StartServer();
        Debug.Log($"Iniciado el servidor");
    }

    public void startClient()
    {
        _networkManager.StartClient();
        
    }

    public void createPlayersPrefabs()
    {
        if(_networkManager.IsServer)
        {
            foreach(var id in clientIds)
            {
                GameObject newPlayer = Instantiate(humanPrefab);
                newPlayer.GetComponent<NetworkObject>().SpawnAsPlayerObject(id);
            }
            
        }
      
    }

    public void disconectSelf()
    {
        if(_networkManager.IsClient)
        {
            _networkManager.Shutdown();
        }
        if (_networkManager.IsServer)
        {
            _networkManager.Shutdown();
            Debug.Log("Se ha desconectado el servidor");
        }

    }

    public void OnPlayerConnect(ulong clientId)
    {
        AddClientToListRpc(clientId);
        playerNumber.Value += 1;
        Debug.Log($"Se ha conectado el jugador: {clientId}");
        Debug.Log($"Numero de jugadores: {clientIds.Count}");

    }

    public void OnPlayerDisconnect(ulong clientId)
    {
        
        playerNumber.Value -= 1;
        Debug.Log($"Se ha desconectado el jugador: {clientId}");
        Debug.Log($"Numero de jugadores: {clientIds.Count}");
        RemoveClientFromListRpc(clientId);
    }

    [Rpc(SendTo.Server)]
    void AddClientToListRpc(ulong clientid)
    {
        clientIds.Add(clientid);
    }

    void RemoveClientFromListRpc(ulong clientid)
    {
        clientIds.Remove(clientid);
    }
}
