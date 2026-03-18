using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Configurações de Movimento")]
    public float walkSpeed = 5f;
    public float runSpeed = 10f;
    public float gravity = -9.81f;

    [Header("Configurações de Câmera")]
    public float mouseSensitivity = 2f;
    public Transform playerCamera; // Arraste a Camera aqui se não achar automático

    // Variáveis internas
    private CharacterController controller;
    private Vector3 velocity;
    private float xRotation = 0f;

    public GameObject mapaUIPlayer; // Arraste o painel do mapa aqui

    void Start()
    {
        controller = GetComponent<CharacterController>();

        // Se não atribuiu a câmera no Inspector, tenta achar a primeira filha
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>().transform;

        // Trava o mouse no centro da tela e o esconde
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
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Rotaciona a Câmera verticalmente (olhar para cima/baixo)
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f); // Impede pescoço de girar 360
        playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Rotaciona o Corpo horizontalmente (olhar para lados)
        transform.Rotate(Vector3.up * mouseX);
    }

    void HandleMovement()
    {
        // Verifica se está segurando Shift para correr
        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;

        float x = Input.GetAxis("Horizontal"); // A/D
        float z = Input.GetAxis("Vertical");   // W/S

        // Move na direção que o player está olhando
        Vector3 move = transform.right * x + transform.forward * z;

        controller.Move(move * currentSpeed * Time.deltaTime);

        // Aplica gravidade simples
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Mantém o player "colado" no chão
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
    void HandleMapInput()
    {
        if (mapaUIPlayer == null) return;

        // Input.GetKey retorna true ENQUANTO a tecla está pressionada
        // Isso faz com que: Segurou = Abre (true), Soltou = Fecha (false)
        bool mostrarMapa = Input.GetKey(KeyCode.Tab);
        
        // Só chama o SetActive se o estado mudar, para otimizar um pouco
        if (mapaUIPlayer.activeSelf != mostrarMapa)
        {
            mapaUIPlayer.SetActive(mostrarMapa);
        }
    }
    
}