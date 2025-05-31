using TMPro;
using Unity.Netcode;
using UnityEngine;
using System;


public class PlayerController : NetworkBehaviour
{
    [SerializeField] public TextMeshProUGUI coinText;
    [SerializeField] public GameObject coinParent;

    [Header("Stats")]
    public int CoinsCollected = 0;

    [Header("Character settings")]
    public bool isZombie = false; // Añadir una propiedad para el estado del jugador
    public string uniqueID; // Añadir una propiedad para el identificador único

    [Header("Movement Settings")]
    public float moveSpeed = 5f;           // Velocidad de movimiento
    public float zombieSpeedModifier = 0.8f; // Modificador de velocidad para zombies
    public Animator animator;              // Referencia al Animator
    public Transform cameraTransform;      // Referencia a la cámara

    private float horizontalInput;         // Entrada horizontal (A/D o flechas)
    private float verticalInput;           // Entrada vertical (W/S o flechas)

    public GameObject cameraPrefab;
    Vector3 moveDirection;

    public NetworkVariable<ulong> id = new NetworkVariable<ulong>();
    Action onCoinPicked;
    [SerializeField] FloatingText floatingText;

    void Start()
    {
        // Buscar el objeto "CanvasPlayer" en la escena
        GameObject canvas = GameObject.Find("CanvasPlayer");

        if (canvas != null)
        {
            Debug.Log("Canvas encontrado");

            // Buscar el Panel dentro del CanvasHud
            Transform panel = canvas.transform.Find("PanelHud");
            if (panel != null)
            {
                // Buscar el TextMeshProUGUI llamado "CoinsValue" dentro del Panel
                Transform coinTextTransform = panel.Find("Coins");
                if (coinTextTransform != null)
                {
                    coinText = coinTextTransform.GetComponentsInChildren<TextMeshProUGUI>()[1];
                    coinParent = coinTextTransform.gameObject;
                }
            }
        }


        if (IsOwner)
        {
            if (isZombie)
                coinParent.gameObject.SetActive(false);
            else
                coinParent.gameObject.SetActive(true);

        }

        //


        UpdateCoinUI();
    }

    public override void OnNetworkSpawn()
    {
        //CADA JUGADOR ES RESPONSABLE DE CREAR O ENCONTRAR SU PROPIA CAMARA
        //EN CADA CLIENTE SOLO HAY 1 UNICA CAMARA. SI EL PLAYERCONTROLLER SE INSTANCIA POR PRIMERA VEZ, CREA UNA CAMARA NUEVA
        //SI NO, REUTILIZA LA ANTERIOR

        GameObject camera = null;
        if (IsHost)
        {
            camera = GameObject.FindWithTag("HostCamera");

            if (camera == null)
            {
                camera = Instantiate(cameraPrefab);
                camera.tag = "HostCamera";
            }

        }
        else if (!IsHost && IsClient)
        {
            camera = GameObject.FindWithTag("Camera");

            if (camera == null)
            {
                camera = Instantiate(cameraPrefab);
                camera.tag = "Camera";
            }
        }

        if (camera != null && IsOwner)
        {
            cameraTransform = camera.transform;
            CameraController controller = camera.GetComponent<CameraController>();
            controller.enabled = true;
            controller.player = transform;
        }

        //Obtiene su id del diccionario de nombres del GameManager y lo muestra encima de su cabeza
        uniqueID = GameManager.Instance.clientNames[id.Value];
        if (floatingText != null)
        {
            floatingText.text.text = uniqueID;
        }



    }
    

    void Update()
    {
        if (!IsHost && IsOwner) //Los clientes mandan su imput al host
        {
            // Leer entrada del teclado
            horizontalInput = Input.GetAxis("Horizontal");
            verticalInput = Input.GetAxis("Vertical");

            if (cameraTransform != null)
            {
                UpdateMoveDirectionRpc(cameraTransform.forward, cameraTransform.right, horizontalInput, verticalInput);
            }
        }
        else if (IsHost && IsOwner) //El host se mueve directamente, sin pasar por rpc
        {
            horizontalInput = Input.GetAxis("Horizontal");
            verticalInput = Input.GetAxis("Vertical");

            if (cameraTransform != null)
            {
                //Calcular la dirección de movimiento en relación a la cámara
                moveDirection = (cameraTransform.forward * verticalInput + cameraTransform.right * horizontalInput).normalized;
                moveDirection.y = 0f; // Asegurarnos de que el movimiento es horizontal (sin componente Y)   
            }
        }



        // Mover el jugador en el servidor. Network Transform se encarga de propagar ese movimiento a los clientes
        if (IsServer)
        {
            MovePlayer();

            HandleAnimations();
        }

    }

    //Manda al servidor el imput para calcular movimiento y animaciones
    [Rpc(SendTo.Server)]
    public void UpdateMoveDirectionRpc(Vector3 cameraForward, Vector3 cameraRight, float horizontal, float vertical)
    {
        horizontalInput = horizontal;
        verticalInput = vertical;
        moveDirection = (cameraForward * verticalInput + cameraRight * horizontalInput).normalized;
        moveDirection.y = 0;
    }


    void MovePlayer()
    {
        // Mover el jugador usando el Transform
        if (moveDirection != Vector3.zero)
        {
            // Calcular la rotación en Y basada en la dirección del movimiento
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 720f * Time.deltaTime);

            // Ajustar la velocidad si es zombie
            float adjustedSpeed = isZombie ? moveSpeed * zombieSpeedModifier : moveSpeed;

            // Mover al jugador en la dirección deseada
            transform.Translate(moveDirection * adjustedSpeed * Time.deltaTime, Space.World);
        }

    }

    void HandleAnimations()
    {
        // Animaciones basadas en la dirección del movimiento
        animator.SetFloat("Speed", Mathf.Abs(horizontalInput) + Mathf.Abs(verticalInput));  // Controla el movimiento (caminar/correr)
    }

    public void CoinCollected()
    {
        if (!isZombie) // Solo los humanos pueden recoger monedas
        {
            this.CoinsCollected++;
            Console.WriteLine("monedo");
            UpdateCoinUI();
        }
    }

    [ClientRpc]
    public void CollectCoinClientRpc()
    {
        if (IsHost) onCoinPicked();
        if (IsOwner)
            CoinCollected();
    }


    void UpdateCoinUI()
    {
        if (coinText != null)
        {
            coinText.text = $"<{CoinsCollected}>";
        }
    }


    public void SubscribeToOnCoinPicked(Action action)
    {
        onCoinPicked += action;
    }

}

