using UnityEngine;
using UnityEngine.SceneManagement; // Importante para carregar a cena

public class ColetavelVitoria : MonoBehaviour
{
    [Header("Configurações da Vitória")]
    public string nomeCenaVitoria = "MenuPrincipal"; // Digite o nome exato da cena de vitória aqui

    [Header("Configurações da Animação")]
    public float velocidadeFlutuar = 2f; // Quão rápido ele sobe e desce
    public float amplitudeFlutuar = 0.5f; // Quão alto/baixo ele vai
    public float velocidadeGiro = 50f; // (Opcional) Para ele girar também

    private Vector3 posicaoInicial;

    void Start()
    {
        // Salva a posição onde você colocou o objeto na cena
        posicaoInicial = transform.position;
    }

    void Update()
    {
        // 1. Animação de Flutuar (Senoide)
        // Mathf.Sin cria uma onda suave entre -1 e 1
        float novoY = posicaoInicial.y + Mathf.Sin(Time.time * velocidadeFlutuar) * amplitudeFlutuar;
        transform.position = new Vector3(transform.position.x, novoY, transform.position.z);

        // 2. Animação de Girar (Para ficar mais estiloso)
        transform.Rotate(Vector3.up * velocidadeGiro * Time.deltaTime);
    }

    // Essa função é chamada quando algo entra no "sensor" deste objeto
    void OnTriggerEnter(Collider other)
    {
        // Verifica se foi o Player que tocou (para evitar que inimigos ganhem o jogo)
        if (other.CompareTag("Player"))
        {
            Debug.Log("Vitória! Carregando próxima cena...");
            SceneManager.LoadScene(nomeCenaVitoria);
        }
    }
}