using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
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

    //private UniqueIdGenerator uniqueIdGenerator;
    private LevelBuilder levelBuilder;

    private PlayerController playerController;

    private float remainingSeconds;
    private bool isGameOver = false;

    public GameObject gameOverPanel; // Asigna el panel desde el inspector
    [SerializeField] private TMP_Text gameOverText, reasonText;

    private Dictionary<ulong, Team> teams = new Dictionary<ulong, Team>();

    #endregion

    #region Unity game loop methods

    private void Awake()
    {
        Debug.Log("Despertando el nivel");

        // Obtener la referencia al UniqueIDGenerator
        //uniqueIdGenerator = GetComponent<UniqueIdGenerator>();

        // Obtener la referencia al LevelBuilder
        levelBuilder = GetComponent<LevelBuilder>();





        Time.timeScale = 1f; // Asegurarse de que el tiempo no esté detenido
    }

    private void OnEnable()
    {
        GameManager.Instance.onHostDisconnect += () => ShowGameOverPanel(GAMEOVER_DESCONEXION_HOST);
        NetworkManager.Singleton.OnClientDisconnectCallback += OnPlayerDisconnect;
    }

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



        if (NetworkManager.Singleton.IsServer)
            SpawnTeams();

        //createPlayersPrefabs();
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

        if (Input.GetKeyDown(KeyCode.Z)) // Presiona "Z" para convertirte en Zombie
        {
            // Comprobar si el jugador actual está usando el prefab de humano
            GameObject currentPlayer = GameObject.FindGameObjectWithTag("Player");
            if (currentPlayer != null && currentPlayer.name.Contains(playerPrefab.name))
            {
                ChangeToZombie();
            }
            else
            {
                Debug.Log("El jugador actual no es un humano.");
            }
        }
        else if (Input.GetKeyDown(KeyCode.H)) // Presiona "H" para convertirte en Humano
        {
            // Comprobar si el jugador actual está usando el prefab de zombie
            GameObject currentPlayer = GameObject.FindGameObjectWithTag("Player");
            if (currentPlayer != null && currentPlayer.name.Contains(zombiePrefab.name))
            {
                ChangeToHuman();
            }
            else
            {
                Debug.Log("El jugador actual no es un zombie.");
            }
        }
        if (NetworkManager.Singleton.IsServer)
        {
            UpdateGlobalTeamUI();
        }




        if (Input.GetKeyDown(KeyCode.L))
        {
            GlobalGameOver(GAMEOVER_MONEDAS);
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
            Transform cam = human.GetComponent<PlayerController>().cameraTransform;
            ulong id = human.GetComponent<PlayerController>().id;

            //teams[id] = Team.ConvertedZombie;

            // Destruir el humano actual
            Destroy(human);


            // Instanciar el prefab del zombie en la misma posición y rotación
            GameObject zombie = Instantiate(zombiePrefab, playerPosition, playerRotation);
            //ulong id = zombie.GetComponent<PlayerController>().id;
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
                    UpdateDictionaryClientRpc(id, Team.ConvertedZombie);
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

    private void ChangeToHuman()
    {
        Debug.Log("Cambiando a Humano");

        // Obtener la referencia al jugador actual
        GameObject currentPlayer = GameObject.FindGameObjectWithTag("Player");

        if (currentPlayer != null)
        {
            // Guardar la posición y rotación del jugador actual
            Vector3 playerPosition = currentPlayer.transform.position;
            Quaternion playerRotation = currentPlayer.transform.rotation;

            // Destruir el jugador actual
            Destroy(currentPlayer);

            // Instanciar el prefab del humano en la misma posición y rotación
            GameObject human = Instantiate(playerPrefab, playerPosition, playerRotation);
            human.tag = "Player";

            // Obtener la referencia a la cámara principal
            Camera mainCamera = Camera.main;

            if (mainCamera != null)
            {
                // Obtener el script CameraController de la cámara principal
                CameraController cameraController = mainCamera.GetComponent<CameraController>();

                if (cameraController != null)
                {
                    // Asignar el humano al script CameraController
                    cameraController.player = human.transform;
                }

                // Obtener el componente PlayerController del humano instanciado
                playerController = human.GetComponent<PlayerController>();
                // Asignar el transform de la cámara al PlayerController
                if (playerController != null)
                {
                    playerController.enabled = true;
                    playerController.cameraTransform = mainCamera.transform;
                    playerController.isZombie = false; // Cambiar el estado a humano
                    numberOfHumans++; // Aumentar el número de humanos
                    numberOfZombies--; // Reducir el número de zombis
                }
                else
                {
                    Debug.LogError("PlayerController no encontrado en el humano instanciado.");
                }
            }
            else
            {
                Debug.LogError("No se encontró la cámara principal.");
            }
        }
        else
        {
            Debug.LogError("No se encontró el jugador actual.");
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


            player.tag = "Player";
            player.GetComponent<PlayerController>().uniqueID = GameManager.Instance.clientNames[id];
            player.GetComponent<PlayerController>().id = id;
            player.GetComponent<PlayerController>().SubscribeToOnCoinPicked(AddTotalCoinClientRpc);
            player.GetComponent<NetworkObject>().SpawnAsPlayerObject(id);

            //GameObject camObject = Instantiate(camPrefab);
            //Camera mainCamera = camObject.GetComponent<Camera>();




            /*
            if (mainCamera != null)
            {
                Debug.Log($"Camara instanciada");
                // Obtener el script CameraController de la cámara principal
                CameraController cameraController = mainCamera.gameObject.GetComponent<CameraController>();

                if (cameraController != null)
                {
                    Debug.Log($"CameraController encontrado en la cámara principal.");
                    // Asignar el jugador al script CameraController
                    cameraController.player = player.transform;
                }

                Debug.Log($"Cámara principal encontrada en {mainCamera}");

                //mainCamera.GetComponent<NetworkObject>().SpawnWithOwnership(id);



                // Obtener el componente PlayerController del jugador instanciado
                playerController = player.GetComponent<PlayerController>();
                // Asignar el transform de la cámara al PlayerController
                if (playerController != null)
                {
                    Debug.Log($"PlayerController encontrado en el jugador instanciado.");
                    //playerController.enabled = true;
                    //playerController.cameraTransform = mainCamera.transform;
                    playerController.uniqueID = uniqueIdGenerator.GenerateUniqueID(); // Generar un identificador único

                }
                else
                {
                    Debug.LogError("PlayerController no encontrado en el jugador instanciado.");
                }
            }
            else
            {
                Debug.LogError("No se encontró la cámara principal.");
            }
            */
        }

        else
        {
            Debug.LogError("Faltan referencias al prefab o al punto de aparición.");
        }

    }

    private void SpawnTeams()
    {
        Debug.Log("Instanciando equipos");
        int playerNumber = GameManager.Instance.clientIds.Count;
        if (playerNumber % 2 == 0)
        {
            numberOfHumans = playerNumber / 2;
            numberOfZombies = playerNumber / 2;
        }
        else
        {
            numberOfHumans = playerNumber / 2;
            numberOfZombies = (playerNumber / 2) + 1;
        }
        //if (humanSpawnPoints.Count <= 0) { return; }
        //SpawnPlayer(humanSpawnPoints[0], playerPrefab);
        //Debug.Log($"Personaje jugable instanciado en {humanSpawnPoints[0]}");

        int n = 0;
        for (int i = 0; i < numberOfHumans; i++, n++)
        {
            if (i < humanSpawnPoints.Count)
            {
                ulong id = GameManager.Instance.clientIds[n];
                Debug.Log($"Creando humano para el jugador {id}");
                SpawnPlayer(humanSpawnPoints[i], playerPrefab, id);
                teams.Add(id, Team.Human);
                AddDictionaryClientRpc(id, Team.Human);
            }
        }

        for (int i = 0; i < numberOfZombies; i++, n++)
        {
            if (i < zombieSpawnPoints.Count)
            {
                ulong id = GameManager.Instance.clientIds[n];
                Debug.Log($"Creando zombie para el jugador {id}");
                SpawnPlayer(zombieSpawnPoints[i], zombiePrefab, id);
                teams.Add(id, Team.Zombie);
                AddDictionaryClientRpc(id, Team.Zombie);
            }
        }
    }

    /*
    private void SpawnNonPlayableCharacter(GameObject prefab, Vector3 spawnPosition)
    {
        if (prefab != null)
        {
            GameObject npc = Instantiate(prefab, spawnPosition, Quaternion.identity);
            // Desactivar el controlador del jugador en los NPCs
            var playerController = npc.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.enabled = false; // Desactivar el controlador del jugador
                playerController.uniqueID = uniqueIdGenerator.GenerateUniqueID(); // Asignar un identificador único
            }
            Debug.Log($"Personaje no jugable instanciado en {spawnPosition}");
        }
    }
    ^*/
    private void UpdateTeamUI(int humans, int zombies)
    {
        //Debug.Log("Actualizar ui");
        if (humansText != null)
        {
            humansText.text = $"{humans}";
        }

        if (zombiesText != null)
        {
            zombiesText.text = $"{zombies}";
        }
    }

    [Rpc(SendTo.NotServer)]
    public void UpdateUIClientRpc(int humans, int zombies)
    {
        Debug.Log("Actualizar ui");
        UpdateTeamUI(humans, zombies);
    }

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

    public void GlobalGameOver(int reason)
    {
        if (isGameOver == true) return;

        isGameOver = true;
        if (IsServer)
        {
            //ShowGameOverPanel(reason);
            ShowGameOverPanelClientRpc(reason);
        }
    }
    private void ShowGameOverPanel(int reason)
    {
        if (gameOverPanel != null)
        {
            Button returnButton = gameOverPanel.GetComponentInChildren<Button>();
            returnButton.GetComponent<Button>().onClick.RemoveAllListeners();
            returnButton.GetComponent<Button>().onClick.AddListener(ReturnToMainMenu);
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

    void ClientReturnToMenu()
    {
        MenuManager.Instance.ResetHostButton();
        SceneManager.LoadScene("MenuScene");

    }

    [ClientRpc]
    private void ShowGameOverPanelClientRpc(int reason)
    {
        ShowGameOverPanel(reason);
    }

    public void ReturnToMainMenu()
    {
        // Gestión del cursor
        //Cursor.lockState = CursorLockMode.Locked; // Bloquea el cursor
        //Cursor.visible = false; // Oculta el cursor

        // Cargar la escena del menú principal
        List<NetworkObject> objects = NetworkManager.Singleton.SpawnManager.SpawnedObjectsList.ToList();
        foreach (var obj in objects)
        {
            if (obj != null && obj.IsSpawned && obj.GetComponent<GameManager>() == null)
            {
                obj.Despawn();
            }
        }
        objects.Clear();


        NetworkManager.Singleton.SceneManager.LoadScene("MenuScene", LoadSceneMode.Single); // Cambia "MenuScene" por el nombre de tu escena principal
    }

    public void OnPlayerDisconnect(ulong clientId)
    {
        if (NetworkManager.Singleton.IsServer)
        {
            Console.WriteLine($"Jugador {clientId} desconectado");
            if (teams.ContainsKey(clientId))
            {
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
                if (numberOfHumans == 0 || numberOfZombies == 0)
                {
                    GlobalGameOver(0);
                }
                else
                {
                    UpdateGlobalTeamUI();
                }
                RemoveDictionaryClientRpc(clientId);
            }

        }
    }
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


    [ClientRpc]
    void AddTotalCoinClientRpc()
    {
        TotalCoinsCollected++;
    }

    #endregion

}





