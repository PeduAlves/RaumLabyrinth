using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gerencia a visibilidade de chunks do labirinto com base na posição do player.
/// Ativa apenas os chunks próximos ao player, desativando o restante.
/// 
/// SETUP: Adicione este script num GameObject vazio na cena (ex: "ChunkManager").
/// No MazeGenerator, arraste esse GameObject no campo "Chunk Manager".
/// </summary>
public class MazeChunkManager : MonoBehaviour
{
    [Header("Configuração de Chunks")]
    [Tooltip("Tamanho de cada chunk em células do labirinto (ex: 3 = área de 3x3 células)")]
    public int chunkSizeInCells = 3;

    [Tooltip("Quantos chunks ao redor do player ficam visíveis (ex: 2 = raio de 2 chunks)")]
    public int renderDistance = 2;

    [Header("Referências (preenchidas automaticamente)")]
    [Tooltip("Transform do player. Preenchido automaticamente quando o player é spawnado.")]
    public Transform playerTransform;

    // Armazena todos os GameObjects por posição de chunk
    private Dictionary<Vector2Int, List<GameObject>> chunks = new Dictionary<Vector2Int, List<GameObject>>();

    // Último chunk onde o player estava (evita recalcular toda frame)
    private Vector2Int lastPlayerChunk = new Vector2Int(int.MinValue, int.MinValue);

    // Referência ao spacing do labirinto (preenchida pelo MazeGenerator)
    private float spacing = 4f;

    // Flag para saber se já foi inicializado
    private bool isInitialized = false;

    // ─────────────────────────────────────────────
    //  INICIALIZAÇÃO (chamado pelo MazeGenerator)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Chamado pelo MazeGenerator após a geração completa.
    /// </summary>
    public void Initialize(float mazeSpacing)
    {
        spacing = mazeSpacing;
        isInitialized = true;
        Debug.Log($"[ChunkManager] Inicializado. Spacing: {spacing} | Chunk size: {chunkSizeInCells} células | Render distance: {renderDistance} chunks");
    }

    /// <summary>
    /// Define o Transform do player (chamado pelo MazeGenerator após spawn).
    /// </summary>
    public void SetPlayer(Transform player)
    {
        playerTransform = player;
        // Atualiza visibilidade imediatamente (não espera o próximo Update)
        // para garantir que o chão esteja ativo antes da física do player rodar.
        Vector2Int startChunk = WorldToChunk(playerTransform.position);
        lastPlayerChunk = startChunk;
        UpdateVisibility(startChunk);
        Debug.Log($"[ChunkManager] Player registrado. Visibilidade atualizada para chunk {startChunk}.");
    }

    // ─────────────────────────────────────────────
    //  REGISTRO DE OBJETOS (chamado pelo MazeGenerator)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Registra um GameObject no chunk correspondente à sua posição no mundo.
    /// Chame isso para cada parede, piso e grama instanciados.
    /// </summary>
    public void RegisterObject(GameObject obj, Vector3 worldPosition)
    {
        Vector2Int chunkPos = WorldToChunk(worldPosition);

        if (!chunks.ContainsKey(chunkPos))
            chunks[chunkPos] = new List<GameObject>();

        chunks[chunkPos].Add(obj);

        // Objetos começam desativados, só o Update vai ativar os próximos ao player
        obj.SetActive(false);
    }

    // ─────────────────────────────────────────────
    //  ATUALIZAÇÃO DE VISIBILIDADE
    // ─────────────────────────────────────────────

    void Update()
    {
        if (!isInitialized || playerTransform == null) return;

        Vector2Int currentChunk = WorldToChunk(playerTransform.position);

        // Só recalcula quando o player muda de chunk (barato de verificar)
        if (currentChunk != lastPlayerChunk)
        {
            lastPlayerChunk = currentChunk;
            UpdateVisibility(currentChunk);
        }
    }

    void UpdateVisibility(Vector2Int playerChunk)
    {
        // 1. Desativa TODOS os chunks
        foreach (var kvp in chunks)
            SetChunkActive(kvp.Key, false);

        // 2. Ativa apenas os chunks dentro do raio
        for (int x = -renderDistance; x <= renderDistance; x++)
        {
            for (int z = -renderDistance; z <= renderDistance; z++)
            {
                // Opcional: usar círculo ao invés de quadrado
                // if (x * x + z * z > renderDistance * renderDistance) continue;

                Vector2Int nearbyChunk = playerChunk + new Vector2Int(x, z);
                SetChunkActive(nearbyChunk, true);
            }
        }

        // Log útil durante desenvolvimento (remova em produção)
        int activeChunks = (renderDistance * 2 + 1) * (renderDistance * 2 + 1);
        int totalChunks = chunks.Count;
        // Debug.Log($"[ChunkManager] Chunk do player: {playerChunk} | Ativos: {activeChunks}/{totalChunks}");
    }

    void SetChunkActive(Vector2Int chunkPos, bool active)
    {
        if (!chunks.TryGetValue(chunkPos, out List<GameObject> objects)) return;

        foreach (var obj in objects)
        {
            if (obj != null)
                obj.SetActive(active);
        }
    }

    // ─────────────────────────────────────────────
    //  UTILITÁRIO
    // ─────────────────────────────────────────────

    /// <summary>
    /// Converte posição no mundo para índice de chunk.
    /// </summary>
    Vector2Int WorldToChunk(Vector3 worldPos)
    {
        float chunkWorldSize = chunkSizeInCells * spacing;
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x / chunkWorldSize),
            Mathf.FloorToInt(worldPos.z / chunkWorldSize)
        );
    }

    /// <summary>
    /// Retorna o número total de objetos registrados (debug).
    /// </summary>
    public int GetTotalRegisteredObjects()
    {
        int total = 0;
        foreach (var kvp in chunks) total += kvp.Value.Count;
        return total;
    }

    /// <summary>
    /// Ativa TODOS os chunks (útil para o mapa aéreo).
    /// </summary>
    public void ShowAll()
    {
        foreach (var kvp in chunks)
            SetChunkActive(kvp.Key, true);
    }

    /// <summary>
    /// Desativa todos exceto os do player (volta ao modo normal).
    /// </summary>
    public void HideAll()
    {
        lastPlayerChunk = new Vector2Int(int.MinValue, int.MinValue); // Força re-render
    }
}
