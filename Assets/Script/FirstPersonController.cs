using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Movimento")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float runSpeed = 10f;
    [Tooltip("Altura do pulo em metros")]
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -9.81f;
    [Tooltip("Velocidade vertical mantida no chão para 'colar' o player na rampa/piso")]
    [SerializeField] private float groundedStickForce = -2f;

    [Header("Câmera")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float minPitch = -90f;
    [SerializeField] private float maxPitch = 90f;
    [Tooltip("Deixe vazio para achar a primeira Camera filha automaticamente")]
    [SerializeField] private Transform playerCamera;

    [Header("Teclas")]
    [SerializeField] private KeyCode runKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField] private KeyCode mapKey = KeyCode.Tab;

    [Header("UI")]
    public GameObject mapaUIPlayer; // Atribuído pelo MazeGenerator após o spawn

    private CharacterController controller;
    private float verticalVelocity; // só o eixo Y persiste entre frames (gravidade/pulo)
    private float pitch;            // rotação vertical acumulada da câmera

    void Start()
    {
        controller = GetComponent<CharacterController>();

        // Se não atribuiu a câmera no Inspector, tenta achar a primeira filha.
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>().transform;

        // Garante um CameraShake na câmera (para o game juice funcionar sem setup manual).
        // Pode ser adicionado manualmente no prefab para ajustar os valores no Inspector.
        if (playerCamera.GetComponent<CameraShake>() == null)
            playerCamera.gameObject.AddComponent<CameraShake>();

        // Trava o mouse no centro da tela e o esconde.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleMouseLook();
        HandleMovement();
        HandleMapInput();
    }

    void HandleMouseLook()
    {
        // GetAxis("Mouse ...") já é delta de movimento; não multiplica por deltaTime.
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Olhar para cima/baixo (câmera) com trava para não virar o pescoço 360°.
        pitch = Mathf.Clamp(pitch - mouseY, minPitch, maxPitch);
        playerCamera.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        // Olhar para os lados (corpo).
        transform.Rotate(Vector3.up * mouseX);
    }

    void HandleMovement()
    {
        bool grounded = controller.isGrounded;

        // Ao aterrissar, zera a queda acumulada para o player ficar "colado" no chão.
        if (grounded && verticalVelocity < 0f)
            verticalVelocity = groundedStickForce;

        // Movimento horizontal relativo à direção que o player está olhando.
        float x = Input.GetAxis("Horizontal"); // A/D
        float z = Input.GetAxis("Vertical");   // W/S
        float speed = Input.GetKey(runKey) ? runSpeed : walkSpeed;
        Vector3 horizontal = (transform.right * x + transform.forward * z) * speed;

        // Pulo único: só dispara quando está no chão (no ar, isGrounded é false → sem double jump).
        if (grounded && Input.GetKeyDown(jumpKey))
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        // Gravidade.
        verticalVelocity += gravity * Time.deltaTime;

        // Um único Move com horizontal + vertical (uma só resolução de colisão por frame).
        Vector3 motion = horizontal + Vector3.up * verticalVelocity;
        controller.Move(motion * Time.deltaTime);
    }

    void HandleMapInput()
    {
        if (mapaUIPlayer == null) return;

        // Segurar a tecla abre o mapa; soltar fecha. Só chama SetActive quando muda.
        bool showMap = Input.GetKey(mapKey);
        if (mapaUIPlayer.activeSelf != showMap)
            mapaUIPlayer.SetActive(showMap);
    }
}
