using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

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
        if (_networkManager.IsServer)
        {
            foreach (var id in clientIds)
            {
                GameObject newPlayer = Instantiate(humanPrefab);
                newPlayer.GetComponent<NetworkObject>().SpawnAsPlayerObject(id);
            }

        }

    }

    public void disconectSelf()
    {
        if (_networkManager.IsClient)
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

        Debug.Log($"Se ha conectado el jugador: {clientId}");
        Debug.Log($"Numero de jugadores: {clientIds.Count}");

    }

    public void OnPlayerDisconnect(ulong clientId)
    {

        RemoveClientFromListRpc(clientId);
        Debug.Log($"Se ha desconectado el jugador: {clientId}");
        Debug.Log($"Numero de jugadores: {clientIds.Count}");

    }


    void AddClientToListRpc(ulong clientid)
    {
        if (NetworkManager.IsServer)
            clientIds.Add(clientid);
    }


    void RemoveClientFromListRpc(ulong clientid)
    {
        if (NetworkManager.IsServer)
            clientIds.Remove(clientid);
    }
}
