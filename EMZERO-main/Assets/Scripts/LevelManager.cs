using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameMode
{
    Tiempo,
    Monedas
}

public class LevelManager : MonoBehaviour
{
    #region Properties

    [Header("Prefabs")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject zombiePrefab;

    [Header("Team Settings")]
    [Tooltip("Número de jugadores humanos")]
    [SerializeField] private int numberOfHumans = 2;

    [Tooltip("Número de zombis")]
    [SerializeField] private int numberOfZombies = 2;

    [Header("Game Mode Settings")]
    [Tooltip("Selecciona el modo de juego")]
    [SerializeField] private GameMode gameMode;

    [Tooltip("Tiempo de partida en minutos para el modo tiempo")]
    [SerializeField] private int minutes = 5;

    private List<Vector3> humanSpawnPoints = new List<Vector3>();
    private List<Vector3> zombieSpawnPoints = new List<Vector3>();

    // Referencias a los elementos de texto en el canvas
    private TextMeshProUGUI humansText;
    private TextMeshProUGUI zombiesText;
    private TextMeshProUGUI gameModeText;

    private int CoinsGenerated = 0;

    public string PlayerPrefabName => playerPrefab.name;
    public string ZombiePrefabName => zombiePrefab.name;

    private UniqueIdGenerator uniqueIdGenerator;
    private LevelBuilder levelBuilder;

    private PlayerController playerController;

    private float remainingSeconds;
    private bool isGameOver = false;

    public GameObject gameOverPanel; // Asigna el panel desde el inspector

    #endregion

    #region Unity game loop methods

    private void Awake()
    {
        Debug.Log("Despertando el nivel");

        // Obtener la referencia al UniqueIDGenerator
        uniqueIdGenerator = GetComponent<UniqueIdGenerator>();

        // Obtener la referencia al LevelBuilder
        levelBuilder = GetComponent<LevelBuilder>();

        Time.timeScale = 1f; // Asegurarse de que el tiempo no esté detenido
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

        remainingSeconds = minutes * 60;

        // Obtener los puntos de aparición y el número de monedas generadas desde LevelBuilder
        if (levelBuilder != null)
        {
            levelBuilder.Build();
            humanSpawnPoints = levelBuilder.GetHumanSpawnPoints();
            zombieSpawnPoints = levelBuilder.GetZombieSpawnPoints();
            CoinsGenerated = levelBuilder.GetCoinsGenerated();
        }

        SpawnTeams();
        
        UpdateTeamUI();
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
        UpdateTeamUI();

        if (isGameOver)
        {
            ShowGameOverPanel();
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
        Debug.Log("Cambiando a Zombie");

        if (human != null)
        {
            // Guardar la posición, rotación y uniqueID del humano actual
            Vector3 playerPosition = human.transform.position;
            Quaternion playerRotation = human.transform.rotation;
            string uniqueID = human.GetComponent<PlayerController>().uniqueID;

            // Destruir el humano actual
            Destroy(human);

            // Instanciar el prefab del zombie en la misma posición y rotación
            GameObject zombie = Instantiate(zombiePrefab, playerPosition, playerRotation);
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
                UpdateTeamUI();

                if (enabled)
                {
                    // Obtener la referencia a la cámara principal
                    Camera mainCamera = Camera.main;

                    if (mainCamera != null)
                    {
                        // Obtener el script CameraController de la cámara principal
                        CameraController cameraController = mainCamera.GetComponent<CameraController>();

                        if (cameraController != null)
                        {
                            // Asignar el zombie al script CameraController
                            cameraController.player = zombie.transform;
                        }

                        // Asignar el transform de la cámara al PlayerController
                        playerController.cameraTransform = mainCamera.transform;
                    }
                    else
                    {
                        Debug.LogError("No se encontró la cámara principal.");
                    }
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

    private void SpawnPlayer(Vector3 spawnPosition, GameObject prefab)
    {
        Debug.Log($"Instanciando jugador en {spawnPosition}");
        if (prefab != null)
        {
            Debug.Log($"Instanciando jugador en {spawnPosition}");
            // Crear una instancia del prefab en el punto especificado
            GameObject player = Instantiate(prefab, spawnPosition, Quaternion.identity);
            player.tag = "Player";

            // Obtener la referencia a la cámara principal
            Camera mainCamera = Camera.main;

            if (mainCamera != null)
            {
                // Obtener el script CameraController de la cámara principal
                CameraController cameraController = mainCamera.GetComponent<CameraController>();

                if (cameraController != null)
                {
                    Debug.Log($"CameraController encontrado en la cámara principal.");
                    // Asignar el jugador al script CameraController
                    cameraController.player = player.transform;
                }

                Debug.Log($"Cámara principal encontrada en {mainCamera}");
                // Obtener el componente PlayerController del jugador instanciado
                playerController = player.GetComponent<PlayerController>();
                // Asignar el transform de la cámara al PlayerController
                if (playerController != null)
                {
                    Debug.Log($"PlayerController encontrado en el jugador instanciado.");
                    playerController.enabled = true;
                    playerController.cameraTransform = mainCamera.transform;
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
        }
        else
        {
            Debug.LogError("Faltan referencias al prefab o al punto de aparición.");
        }
    }

    private void SpawnTeams()
    {
        Debug.Log("Instanciando equipos");
        if (humanSpawnPoints.Count <= 0) { return; }
        SpawnPlayer(humanSpawnPoints[0], playerPrefab);
        Debug.Log($"Personaje jugable instanciado en {humanSpawnPoints[0]}");

        for (int i = 1; i < numberOfHumans; i++)
        {
            if (i < humanSpawnPoints.Count)
            {
                SpawnNonPlayableCharacter(playerPrefab, humanSpawnPoints[i]);
            }
        }

        for (int i = 0; i < numberOfZombies; i++)
        {
            if (i < zombieSpawnPoints.Count)
            {
                SpawnNonPlayableCharacter(zombiePrefab, zombieSpawnPoints[i]);
            }
        }
    }

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

    private void UpdateTeamUI()
    {
        if (humansText != null)
        {
            humansText.text = $"{numberOfHumans}";
        }

        if (zombiesText != null)
        {
            zombiesText.text = $"{numberOfZombies}";
        }
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
            isGameOver = true;
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
        if (gameModeText != null && playerController != null)
        {
            gameModeText.text = $"{playerController.CoinsCollected}/{CoinsGenerated}";
            if (playerController.CoinsCollected == CoinsGenerated)
            {
                isGameOver = true;
            }
        }
    }

    private void ShowGameOverPanel()
    {
        if (gameOverPanel != null)
        {
            Time.timeScale = 0f;
            gameOverPanel.SetActive(true); // Muestra el panel de pausa

            // Gestión del cursor
            Cursor.lockState = CursorLockMode.None; // Desbloquea el cursor
            Cursor.visible = true; // Hace visible el cursor
        }
    }

    public void ReturnToMainMenu()
    {
        // Gestión del cursor
        Cursor.lockState = CursorLockMode.Locked; // Bloquea el cursor
        Cursor.visible = false; // Oculta el cursor

        // Cargar la escena del menú principal
        SceneManager.LoadScene("MenuScene"); // Cambia "MenuScene" por el nombre de tu escena principal
    }

    #endregion

}




