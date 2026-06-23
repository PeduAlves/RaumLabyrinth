using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Gerencia a visibilidade (culling) de chunks do labirinto com base na posição do player.
///
/// Princípios desta versão:
///  • Os objetos NÃO são desativados ao serem registrados. Durante a geração e a
///    câmera aérea o labirinto inteiro fica visível. O culling só começa quando o
///    player assume o controle (BeginCulling).
///  • O culling é INCREMENTAL: ao cruzar a fronteira de um chunk, apenas a diferença
///    (chunks que entraram / saíram do alcance) é alternada, em vez de desligar tudo
///    e religar os próximos.
///  • Navmesh-aware: objetos com NavMeshAgent (inimigos) NUNCA são cullled —
///    desativá-los desligaria o agente e geraria erros "not placed on a NavMesh".
///    O NavMesh é baked uma vez e continua válido mesmo com pisos/paredes desativados,
///    então pisos (que carregam a grama animada) também são cullados por distância.
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

    [Tooltip("Usar alcance circular em vez de quadrado (visual mais natural, menos chunks)")]
    public bool useCircularDistance = true;

    [Header("Referências (preenchidas automaticamente)")]
    [Tooltip("Transform do player. Preenchido automaticamente quando o player é spawnado.")]
    public Transform playerTransform;

    // Apenas objetos CULLÁVEIS (paredes, decoração) entram aqui, agrupados por chunk.
    // Pisos e inimigos não são registrados aqui — ficam sempre ativos.
    private readonly Dictionary<Vector2Int, List<GameObject>> chunks = new Dictionary<Vector2Int, List<GameObject>>();

    // Conjuntos de chunks ativos. Dois buffers que alternam (ping-pong) para evitar
    // alocação a cada atualização de visibilidade.
    private HashSet<Vector2Int> activeChunks = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> desiredChunks = new HashSet<Vector2Int>();

    // Último chunk onde o player estava (evita recalcular toda frame).
    private Vector2Int lastPlayerChunk = new Vector2Int(int.MinValue, int.MinValue);

    // Tamanho de cada chunk em células, preenchido pelo MazeGenerator.
    private float spacing = 4f;

    private bool isInitialized = false;

    // O culling só roda depois que o player está em jogo (BeginCulling).
    private bool cullingEnabled = false;

    // ─────────────────────────────────────────────
    //  INICIALIZAÇÃO (chamado pelo MazeGenerator)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Chamado pelo MazeGenerator antes de registrar objetos (precisa do spacing
    /// correto para o WorldToChunk).
    /// </summary>
    public void Initialize(float mazeSpacing)
    {
        spacing = mazeSpacing;
        isInitialized = true;
        Debug.Log($"[ChunkManager] Inicializado. Spacing: {spacing} | Chunk: {chunkSizeInCells} células | Render distance: {renderDistance} chunks");
    }

    /// <summary>
    /// Apenas guarda o Transform do player. NÃO inicia o culling — isso é feito por
    /// BeginCulling() quando o player de fato assume o controle.
    /// </summary>
    public void SetPlayer(Transform player)
    {
        playerTransform = player;
    }

    // ─────────────────────────────────────────────
    //  REGISTRO DE OBJETOS (chamado pelo MazeGenerator)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Registra um GameObject para culling por chunk.
    /// Objetos com NavMeshAgent (inimigos) são automaticamente marcados como
    /// não-culláveis para não desligar o agente.
    /// </summary>
    /// <param name="cullable">
    /// false para objetos que devem ficar sempre ativos (ex: pisos, pelo chão do
    /// navmesh e raycasts). Nesse caso o objeto não é gerenciado e permanece ativo.
    /// </param>
    public void RegisterObject(GameObject obj, Vector3 worldPosition, bool cullable = true)
    {
        if (obj == null) return;

        // Segurança navmesh: nunca cullar algo que tenha um agente, senão o agente
        // é desligado e quebra ("can only be called on an agent placed on a NavMesh").
        if (cullable && obj.GetComponentInChildren<NavMeshAgent>(true) != null)
            cullable = false;

        if (!cullable) return; // permanece sempre ativo, não é gerenciado

        Vector2Int chunkPos = WorldToChunk(worldPosition);

        if (!chunks.TryGetValue(chunkPos, out List<GameObject> list))
        {
            list = new List<GameObject>();
            chunks[chunkPos] = list;
        }

        list.Add(obj);
        // Permanece ATIVO. O culling só começa em BeginCulling().
    }

    /// <summary>
    /// Remove um objeto do gerenciamento (ex: parede destruída por uma mutação em runtime),
    /// evitando referências nulas acumuladas na lista do chunk.
    /// </summary>
    public void UnregisterObject(GameObject obj, Vector3 worldPosition)
    {
        if (obj == null) return;
        if (chunks.TryGetValue(WorldToChunk(worldPosition), out List<GameObject> list))
            list.Remove(obj);
    }

    // ─────────────────────────────────────────────
    //  CONTROLE DE CULLING
    // ─────────────────────────────────────────────

    /// <summary>
    /// Ativa TODOS os chunks e desliga o culling.
    /// Use durante a câmera aérea e antes do bake do NavMesh (geometria completa).
    /// </summary>
    public void ShowAll()
    {
        cullingEnabled = false;
        foreach (var kvp in chunks)
            SetChunkActive(kvp.Key, true);
        activeChunks.Clear();
        lastPlayerChunk = new Vector2Int(int.MinValue, int.MinValue);
    }

    /// <summary>
    /// Inicia o culling por chunks. Chame quando o player assume o controle
    /// (após a transição da câmera aérea). Faz o primeiro corte imediatamente.
    /// </summary>
    public void BeginCulling()
    {
        if (playerTransform == null)
        {
            Debug.LogWarning("[ChunkManager] BeginCulling chamado sem playerTransform. Culling não iniciado.");
            return;
        }

        cullingEnabled = true;

        // Neste momento tudo está ativo (geração/aérea). Marca todos os chunks como
        // ativos para que o primeiro RefreshVisibility desligue corretamente os que
        // estão fora do alcance.
        activeChunks.Clear();
        foreach (var key in chunks.Keys)
            activeChunks.Add(key);

        lastPlayerChunk = WorldToChunk(playerTransform.position);
        RefreshVisibility(lastPlayerChunk);

        Debug.Log($"[ChunkManager] Culling iniciado no chunk {lastPlayerChunk}.");
    }

    // ─────────────────────────────────────────────
    //  ATUALIZAÇÃO DE VISIBILIDADE
    // ─────────────────────────────────────────────

    void Update()
    {
        if (!cullingEnabled || playerTransform == null) return;

        Vector2Int currentChunk = WorldToChunk(playerTransform.position);

        // Só recalcula quando o player muda de chunk (barato de verificar).
        if (currentChunk != lastPlayerChunk)
        {
            lastPlayerChunk = currentChunk;
            RefreshVisibility(currentChunk);
        }
    }

    /// <summary>
    /// Aplica visibilidade alternando apenas a DIFERENÇA entre o conjunto atual e o
    /// desejado (chunks que entraram / saíram do alcance).
    /// </summary>
    void RefreshVisibility(Vector2Int playerChunk)
    {
        // 1. Monta o conjunto desejado no buffer reutilizável.
        desiredChunks.Clear();
        int sqrRadius = renderDistance * renderDistance;

        for (int x = -renderDistance; x <= renderDistance; x++)
        {
            for (int z = -renderDistance; z <= renderDistance; z++)
            {
                if (useCircularDistance && (x * x + z * z) > sqrRadius) continue;
                desiredChunks.Add(playerChunk + new Vector2Int(x, z));
            }
        }

        // 2. Desativa o que estava ativo mas não é mais desejado.
        foreach (var chunk in activeChunks)
            if (!desiredChunks.Contains(chunk))
                SetChunkActive(chunk, false);

        // 3. Ativa o que é desejado mas ainda não estava ativo.
        foreach (var chunk in desiredChunks)
            if (!activeChunks.Contains(chunk))
                SetChunkActive(chunk, true);

        // 4. Ping-pong dos buffers: o desejado vira o ativo; o antigo ativo é
        //    reaproveitado (será limpo no início do próximo RefreshVisibility).
        var tmp = activeChunks;
        activeChunks = desiredChunks;
        desiredChunks = tmp;
    }

    void SetChunkActive(Vector2Int chunkPos, bool active)
    {
        if (!chunks.TryGetValue(chunkPos, out List<GameObject> objects)) return;

        for (int i = 0; i < objects.Count; i++)
        {
            var obj = objects[i];
            if (obj != null && obj.activeSelf != active)
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
    /// Retorna o número de objetos culláveis gerenciados (debug).
    /// </summary>
    public int GetTotalRegisteredObjects()
    {
        int total = 0;
        foreach (var kvp in chunks) total += kvp.Value.Count;
        return total;
    }
}
