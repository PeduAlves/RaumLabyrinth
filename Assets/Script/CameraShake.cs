using UnityEngine;

/// <summary>
/// Camera shake suave baseado em "trauma" + ruído de Perlin.
///
/// Dois canais de entrada:
///  • AddTrauma(amount)            -> impulsos pontuais que decaem sozinhos
///                                    (tiro, impacto, pisão do inimigo).
///  • ReportContinuousTrauma(a)    -> shake sustentado, reaplicado a cada frame
///                                    (ex: perseguição rápida do inimigo). Vários
///                                    inimigos podem reportar; o maior valor vence.
///
/// Roda em LateUpdate (depois do FirstPersonController posicionar a câmera) e usa
/// tempo NÃO escalado, então o shake continua durante o frame stop (HitStop).
///
/// SETUP: pode ser adicionado manualmente à câmera do player para ajustar os valores
/// no Inspector. Se não houver, o FirstPersonController adiciona um automaticamente.
/// </summary>
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    [Header("Trauma de impulso (decai sozinho)")]
    [Range(0f, 1f)][SerializeField] private float trauma = 0f;
    [Tooltip("Quão rápido o trauma de impulso volta a zero (por segundo)")]
    [SerializeField] private float traumaDecay = 1.6f;
    [Tooltip("Expoente da curva (>1 deixa o final do shake mais suave)")]
    [SerializeField] private float traumaExponent = 2f;

    [Header("Intensidade máxima (com trauma = 1)")]
    [SerializeField] private float maxPositionOffset = 0.22f;
    [SerializeField] private float maxRotationOffset = 3f; // em graus
    [SerializeField] private float frequency = 22f;

    // Shake contínuo reportado neste frame (resetado todo LateUpdate).
    private float continuousTraumaThisFrame = 0f;

    private Vector3 basePosition;
    private float noiseSeed;

    void Awake()
    {
        Instance = this;
        basePosition = transform.localPosition;
        noiseSeed = Random.value * 1000f;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Adiciona um impulso de shake (0..1), somado ao trauma atual.</summary>
    public void AddTrauma(float amount)
    {
        trauma = Mathf.Clamp01(trauma + amount);
    }

    /// <summary>
    /// Reporta um shake contínuo para ESTE frame (0..1). Deve ser chamado todo frame
    /// enquanto o efeito durar. Entre várias chamadas, o maior valor é usado.
    /// </summary>
    public void ReportContinuousTrauma(float amount)
    {
        continuousTraumaThisFrame = Mathf.Max(continuousTraumaThisFrame, Mathf.Clamp01(amount));
    }

    void LateUpdate()
    {
        // Decai o trauma de impulso (tempo não escalado para sobreviver ao HitStop).
        if (trauma > 0f)
            trauma = Mathf.Max(0f, trauma - traumaDecay * Time.unscaledDeltaTime);

        // Combina impulso + contínuo: usa o maior e soma metade do menor para dar peso.
        float a = trauma;
        float b = continuousTraumaThisFrame;
        float effective = Mathf.Clamp01(Mathf.Max(a, b) + Mathf.Min(a, b) * 0.5f);

        // Consome o contínuo deste frame (precisa ser reportado de novo no próximo).
        continuousTraumaThisFrame = 0f;

        float shake = Mathf.Pow(effective, traumaExponent);

        if (shake <= 0.0001f)
        {
            transform.localPosition = basePosition;
            return;
        }

        float t = Time.unscaledTime * frequency;
        float nx = Mathf.PerlinNoise(noiseSeed, t) * 2f - 1f;
        float ny = Mathf.PerlinNoise(noiseSeed + 17.3f, t) * 2f - 1f;
        float nz = Mathf.PerlinNoise(noiseSeed + 41.7f, t) * 2f - 1f;

        // Posição: offset livre em torno da posição base (não conflita com o controller).
        transform.localPosition = basePosition + new Vector3(nx, ny, 0f) * (maxPositionOffset * shake);

        // Rotação: pós-multiplica o pitch que o controller já aplicou neste frame.
        // Como o controller reescreve localRotation todo Update (antes do LateUpdate),
        // isso não acumula entre frames.
        transform.localRotation *= Quaternion.Euler(
            ny * maxRotationOffset * shake * 0.5f,
            nx * maxRotationOffset * shake * 0.5f,
            nz * maxRotationOffset * shake
        );
    }
}
