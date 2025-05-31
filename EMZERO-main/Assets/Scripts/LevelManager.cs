using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum GameMode
{
    Tiempo,
    Monedas
}

public enum Team
{
    Human,
    Zombie,
    ConvertedZombie
}

public class LevelManager : NetworkBehaviour
{
    #region Properties
    // Constatntes de GameOver
    public const int GAMEOVER_DESCONEXION = 0, GAMEOVER_ZOMBIES = 1, GAMEOVER_TIEMPO = 2, GAMEOVER_MONEDAS = 3, GAMEOVER_DESCONEXION_HOST = 5;

    [Header("Prefabs")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject zombiePrefab;

    [Header("Team Settings")]
    [Tooltip("Número de jugadores humanos")]
    private int numberOfHumans = 0;

    [Tooltip("Número de zombis")]
    private int numberOfZombies = 0;

    [Header("Game Mode Settings")]
    [Tooltip("Selecciona el modo de juego")]
    [SerializeField] private GameMode gameMode;

    [Tooltip("Tiempo de partida en minutos para el modo tiempo")]
    [SerializeField] private int minutes = 5;

    [Tooltip("Camara que se instanciará para cada jugador")]
    [SerializeField] private GameObject camPrefab;

    private List<Vector3> humanSpawnPoints = new List<Vector3>();
    private List<Vector3> zombieSpawnPoints = new List<Vector3>();

    // Referencias a los elementos de texto en el canvas
    private TextMeshProUGUI humansText;
    private TextMeshProUGUI zombiesText;
    private TextMeshProUGUI gameModeText;

    private int CoinsGenerated = 0;
    private int TotalCoinsCollected = 0;

    public string PlayerPrefabName => playerPrefab.name;
    public string ZombiePrefabName => zombiePrefab.name;

    //Referencias varias
    private LevelBuilder levelBuilder;
    private PlayerController playerController;

    //Gestión de juego
    private float remainingSeconds;
    private bool isGameOver = false;

    //UI
    public GameObject gameOverPanel; 
    [SerializeField] private TMP_Text gameOverText, reasonText;

    // Diccionario de equipos
    private Dictionary<ulong, Team> teams = new Dictionary<ulong, Team>();

    #endregion

    #region Unity game loop methods

    private void Awake()
    {
        Debug.Log("Despertando el nivel");

        // Obtener la referencia al LevelBuilder
        levelBuilder = GetComponent<LevelBuilder>();

        Time.timeScale = 1f; // Asegurarse de que el tiempo no esté detenido
    }

    // Cuando se activa se suscribe al metodo de desconexion.
    private void OnEnable()
    {
        GameManager.Instance.onHostDisconnect += () => ShowGameOverPanel(GAMEOVER_DESCONEXION_HOST);
        NetworkManager.Singleton.OnClientDisconnectCallback += OnPlayerDisconnect;
    }

    // Se desuscribe para evitar llamadas en la escena incorrecta
    private void OnDisable()
    {
        GameManager.Instance.onHostDisconnect -= () => ShowGameOverPanel(GAMEOVER_DESCONEXION_HOST);
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnPlayerDisconnect;
    }

    private void Start()
    {
        Debug.Log("Iniciando el nivel");

        // Buscar el objeto "CanvasPlayer" en la escena
        GameObject canvas = GameObject.Find("CanvasPlayer");
        if (canvas != null)
        {
            Debug.Log("Canvas encontrado");

            // Buscar el Panel dentro del CanvasHud
            Transform panel = canvas.transform.Find("PanelHud");
            if (panel != null)
            {
                // Buscar los TextMeshProUGUI llamados "HumansValue" y "ZombiesValue" dentro del Panel
                Transform humansTextTransform = panel.Find("HumansValue");
                Transform zombiesTextTransform = panel.Find("ZombiesValue");
                Transform gameModeTextTransform = panel.Find("GameModeConditionValue");

                if (humansTextTransform != null)
                {
                    humansText = humansTextTransform.GetComponent<TextMeshProUGUI>();
                }

                if (zombiesTextTransform != null)
                {
                    zombiesText = zombiesTextTransform.GetComponent<TextMeshProUGUI>();
                }

                if (gameModeTextTransform != null)
                {
                    gameModeText = gameModeTextTransform.GetComponent<TextMeshProUGUI>();
                }
            }
        }

        // El host envia a todos los clientes el modo de juego seleccionado,así como el tiempo total para el minijuego pertinente
        if (IsHost)
            SendGameModeClientRpc( GameManager.Instance.modeCoins, GameManager.Instance.totalTime);

        remainingSeconds = minutes * 60;

        // Obtener los puntos de aparición y el número de monedas generadas desde LevelBuilder
        if (levelBuilder != null)
        {
            levelBuilder.Build();
            humanSpawnPoints = levelBuilder.GetHumanSpawnPoints();
            zombieSpawnPoints = levelBuilder.GetZombieSpawnPoints();
            CoinsGenerated = levelBuilder.GetCoinsGenerated();
        }

        // Generar equipos
        if (NetworkManager.Singleton.IsServer)
            SpawnTeams();

        // Actualizar UI
        if (NetworkManager.Singleton.IsServer)
        {
            UpdateGlobalTeamUI();
        }
    }

    [ClientRpc]
    void SendGameModeClientRpc(bool modeCoins, int time)
    {
        minutes = time;
        if (modeCoins)
            gameMode = GameMode.Monedas;
        else
            gameMode = GameMode.Tiempo;
    }

    private void Update()
    {
        if (gameMode == GameMode.Tiempo)
        {
            // Lógica para el modo de juego basado en tiempo
            HandleTimeLimitedGameMode();
        }
        else if (gameMode == GameMode.Monedas)
        {
            // Lógica para el modo de juego basado en monedas
            HandleCoinBasedGameMode();
        }

        if (NetworkManager.Singleton.IsServer)
        {
            //Mantener UI actualizada
            UpdateGlobalTeamUI();
        }

    }

    #endregion

    #region Team management methods

    private void ChangeToZombie()
    {
        GameObject currentPlayer = GameObject.FindGameObjectWithTag("Player");
        ChangeToZombie(currentPlayer, true);
    }

    public void ChangeToZombie(GameObject human, bool enabled)
    {
        if (human != null)
        {
            Debug.Log("Cambiando a Zombie");
            // Guardar la posición, rotación y uniqueID del humano actual
            Vector3 playerPosition = human.transform.position;
            Quaternion playerRotation = human.transform.rotation;
            string uniqueID = human.GetComponent<PlayerController>().uniqueID;
            ulong id = human.GetComponent<PlayerController>().id.Value;

            // Destruir el humano actual
            Destroy(human);

            // Instanciar el prefab del zombie en la misma posición y rotación
            GameObject zombie = Instantiate(zombiePrefab, playerPosition, playerRotation);
            zombie.GetComponent<NetworkObject>().SpawnAsPlayerObject(id);

            if (enabled) { zombie.tag = "Player"; }

            // Obtener el componente PlayerController del zombie instanciado
            PlayerController playerController = zombie.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.enabled = enabled;
                playerController.isZombie = true; // Cambiar el estado a zombie
                playerController.uniqueID = uniqueID; // Mantener el identificador único
                numberOfHumans--; // Reducir el número de humanos
                numberOfZombies++; // Aumentar el número de zombis
                if (numberOfHumans == 0)
                {
                    GlobalGameOver(GAMEOVER_ZOMBIES); 
                }
                else
                {
                    // ConvertedZombie es un nuevo equipo a parte del de los zombies para gestionar las victorias parciales
                    UpdateDictionaryClientRpc(id, Team.ConvertedZombie); // Actualizar en todos los clientes el nuevo equipo del jugador
                }

                if (NetworkManager.Singleton.IsServer)
                {
                    UpdateGlobalTeamUI();
                }

            }
            else
            {
                Debug.LogError("PlayerController no encontrado en el zombie instanciado.");
            }
        }
        else
        {
            Debug.LogError("No se encontró el humano actual.");
        }
    }


    private void SpawnPlayer(Vector3 spawnPosition, GameObject prefab, ulong id)
    {

        Debug.Log($"Instanciando jugador en {spawnPosition}");
        if (prefab != null)
        {
            Debug.Log($"Instanciando jugador en {spawnPosition}");
            // Crear una instancia del prefab en el punto especificado
            GameObject player = Instantiate(prefab, spawnPosition, Quaternion.identity);

            // Asignar variablesen host
            player.tag = "Player";
            player.GetComponent<PlayerController>().uniqueID = GameManager.Instance.clientNames[id];
            player.GetComponent<PlayerController>().id.Value = id; // Identificador en red del jugador
            player.GetComponent<PlayerController>().SubscribeToOnCoinPicked(AddTotalCoinClientRpc);
            player.GetComponent<NetworkObject>().SpawnAsPlayerObject(id); // Generar en todos los clientes

        }
        else
        {
            Debug.LogError("Faltan referencias al prefab o al punto de aparición.");
        }

    }

    private void SpawnTeams()
    {
        Debug.Log("Instanciando equipos");
        // Generar listas auxiliares para después mezclar su contenido.Con ello se consigue spawns aleatorios y diferentes roles
        // (humanos o zombie) para cada jugador en diferentes partidas
        List<ulong> clientIdsRng = new List<ulong>(GameManager.Instance.clientIds);
        ShuffleList(clientIdsRng);

        List<Vector3> humanSpawnPointsRng = new List<Vector3>(humanSpawnPoints);
        ShuffleList(humanSpawnPointsRng);

        List<Vector3> zombieSpawnPointsRng = new List<Vector3>(zombieSpawnPoints);
        ShuffleList(zombieSpawnPointsRng);

        int playerNumber = clientIdsRng.Count;
        numberOfHumans = playerNumber / 2;
        numberOfZombies = playerNumber - numberOfHumans;

        // Generar humanos y asignarlos a un jugador
        for (int i = 0; i < numberOfHumans; i++)
        {
            if (i < humanSpawnPoints.Count)
            {
                ulong id = clientIdsRng[i];
                Debug.Log($"Creando humano para el jugador {id}");
                SpawnPlayer(humanSpawnPointsRng[i], playerPrefab, id);
                teams.Add(id, Team.Human);
                AddDictionaryClientRpc(id, Team.Human);
            }
        }
        // Generar zombies y asignarlos a un jugador
        for (int i = 0; i < numberOfZombies; i++)
        {
            if (i < zombieSpawnPoints.Count)
            {
                ulong id = clientIdsRng[numberOfHumans + i];
                Debug.Log($"Creando zombie para el jugador {id}");
                SpawnPlayer(zombieSpawnPointsRng[i], zombiePrefab, id);
                teams.Add(id, Team.Zombie);
                AddDictionaryClientRpc(id, Team.Zombie);
            }
        }
    }
    // METODO PARA MEZCLAR UNA LISTA
    public static void ShuffleList<T>(List<T> list)
    {
        System.Random rng = new System.Random();
        int count = list.Count;
        while (count > 1)
        {
            count--;
            int k = rng.Next(count + 1);
            (list[k], list[count]) = (list[count], list[k]);
        }
    }
    // Actualizar UI en host
    private void UpdateTeamUI(int humans, int zombies)
    {
        if (humansText != null)
        {
            humansText.text = $"{humans}";
        }

        if (zombiesText != null)
        {
            zombiesText.text = $"{zombies}";
        }
    }

    //Actualizar UI en clientes
    [Rpc(SendTo.NotServer)]
    public void UpdateUIClientRpc(int humans, int zombies)
    {
        Debug.Log("Actualizar ui");
        UpdateTeamUI(humans, zombies);
    }

    // Actualizar en todos lados
    public void UpdateGlobalTeamUI()
    {
        UpdateTeamUI(numberOfHumans, numberOfZombies);
        UpdateUIClientRpc(numberOfHumans, numberOfZombies);
    }
    #endregion

    #region Modo de juego

    private void HandleTimeLimitedGameMode()
    {
        // Implementar la lógica para el modo de juego basado en tiempo
        if (isGameOver) return;

        // Decrementar remainingSeconds basado en Time.deltaTime
        remainingSeconds -= Time.deltaTime;

        // Comprobar si el tiempo ha llegado a cero
        if (remainingSeconds <= 0)
        {
            GlobalGameOver(GAMEOVER_TIEMPO);
            remainingSeconds = 0;
        }

        // Convertir remainingSeconds a minutos y segundos
        int minutesRemaining = Mathf.FloorToInt(remainingSeconds / 60);
        int secondsRemaining = Mathf.FloorToInt(remainingSeconds % 60);

        // Actualizar el texto de la interfaz de usuario
        if (gameModeText != null)
        {
            gameModeText.text = $"{minutesRemaining:D2}:{secondsRemaining:D2}";
        }

    }

    private void HandleCoinBasedGameMode()
    {
        if (isGameOver) return;

        // Implementar la lógica para el modo de juego basado en monedas
        if (gameModeText != null)
        {
            gameModeText.text = $"{TotalCoinsCollected}/{CoinsGenerated}";
            if (TotalCoinsCollected == CoinsGenerated)
            {
                GlobalGameOver(GAMEOVER_MONEDAS);
            }
        }
    }
    // Llama a GameOver en todos los clientes
    public void GlobalGameOver(int reason)
    {
        if (isGameOver == true) return;

        isGameOver = true;
        if (IsServer)
        {
            ShowGameOverPanelClientRpc(reason);
        }
    }

    [ClientRpc]
    private void ShowGameOverPanelClientRpc(int reason)
    {
        ShowGameOverPanel(reason);
    }

    // Dependiendo del motivo del GameOver se mostrarápor pantalla un mensaje u otro
    private void ShowGameOverPanel(int reason)
    {
        if (gameOverPanel != null)
        {
            Button returnButton = gameOverPanel.GetComponentInChildren<Button>();
            returnButton.GetComponent<Button>().onClick.RemoveAllListeners();
            returnButton.GetComponent<Button>().onClick.AddListener(ReturnToMainMenu);
            // Solo el host es capaz de volver al menú principal. Cuando él vuelve, veuleven todos los clientes.
            if (IsHost)
            {
                returnButton.interactable = true;
                returnButton.GetComponentInChildren<TMP_Text>().text = "Volver al menú";
            }
            else
            {
                returnButton.interactable = false;
                returnButton.GetComponentInChildren<TMP_Text>().text = "Esperando al host";
            }
            gameOverPanel.SetActive(true); // Muestra el panel de pausa
            Team team = teams[NetworkManager.LocalClientId];
            switch (reason)
            {
                case 0:
                    gameOverText.text = "Fin de la partida";
                    reasonText.text = "Un bando se ha desconectado";
                    break;
                case 1:

                    if (team == Team.ConvertedZombie)
                    {
                        gameOverText.text = "Victoria parcial";
                    }
                    else if (team == Team.Zombie)
                    {
                        gameOverText.text = "¡Victoria!";
                    }
                    else
                    {
                        gameOverText.text = "Derrota...";
                    }
                    reasonText.text = "Los zombies han convertido a todos los humanos.";
                    break;
                case 2:

                    if (team == Team.Human)
                    {
                        gameOverText.text = "¡Victoria!";
                    }
                    else
                    {
                        gameOverText.text = "Derrota...";
                    }
                    reasonText.text = "¡Los humanos han sobrevivido!";
                    break;
                case 3:

                    if (team == Team.Human)
                    {
                        gameOverText.text = "¡Victoria!";
                    }
                    else
                    {
                        gameOverText.text = "Derrota...";
                    }
                    reasonText.text = "¡Los humanos han conseguido todas las monedas!";
                    break;
                case 5:
                    gameOverText.text = "Fin de la partida";
                    reasonText.text = "El host se ha desconectado.";
                    returnButton.interactable = true;
                    returnButton.GetComponentInChildren<TMP_Text>().text = "Volver al menú";
                    returnButton.GetComponent<Button>().onClick.RemoveAllListeners();
                    returnButton.GetComponent<Button>().onClick.AddListener(ClientReturnToMenu);
                    break;
            }
            Time.timeScale = 0f;
            // Gestión del cursor
            Cursor.lockState = CursorLockMode.None; // Desbloquea el cursor
            Cursor.visible = true; // Hace visible el cursor
        }
    }

    // Volver al Menú principal desde el cliente
    void ClientReturnToMenu()
    {
        MenuManager.Instance.ResetHostButton();
        SceneManager.LoadScene("MenuScene");

    }

    public void ReturnToMainMenu()
    {
        // Cargar la escena del menú principal
        // Eliminar networkObjects de la escena anterior
        List<NetworkObject> objects = NetworkManager.Singleton.SpawnManager.SpawnedObjectsList.ToList();
        foreach (var obj in objects)
        {
            if (obj != null && obj.IsSpawned && obj.GetComponent<GameManager>() == null)
            {
                obj.Despawn();
            }
        }
        objects.Clear();

        NetworkManager.Singleton.SceneManager.LoadScene("MenuScene", LoadSceneMode.Single); // Cargar escena del menu
    }

    // Gestión de desconexiones durante la partida y actualización del UI.
    public void OnPlayerDisconnect(ulong clientId)
    {
        if (NetworkManager.Singleton.IsServer)
        {
            Console.WriteLine($"Jugador {clientId} desconectado");
            if (teams.ContainsKey(clientId))
            {
                // Se comprueba de qué equipo es el jugador que se desconecta
                Team team = teams[clientId];
                Console.WriteLine($"Restando 1 {team}");
                if (team == Team.Human)
                {
                    numberOfHumans--;
                }
                else
                {
                    numberOfZombies--;
                }
                if (numberOfHumans == 0 || numberOfZombies == 0) // Si un bando se queda sin jugadores, se lanza un GameOver
                {
                    GlobalGameOver(GAMEOVER_DESCONEXION);
                }
                else
                {
                    UpdateGlobalTeamUI();
                }
                RemoveDictionaryClientRpc(clientId); // Se elimina el jugador desconectado del diccionario de equipos
            }

        }
    }

    //METODOS DE GESTION DEL DICCIONARIO DE EQUIPOS
    [ClientRpc]
    void UpdateDictionaryClientRpc(ulong id, Team team)
    {

        teams[id] = team;
    }

    [ClientRpc]
    void AddDictionaryClientRpc(ulong id, Team team)
    {
        if (!IsHost)
            teams.Add(id, team);
    }
    [ClientRpc]
    void RemoveDictionaryClientRpc(ulong id)
    {
        teams.Remove(id);
    }

    // Metodo de actualización de monedas recogidas
    [ClientRpc]
    void AddTotalCoinClientRpc()
    {
        TotalCoinsCollected++;
    }

    #endregion

}





