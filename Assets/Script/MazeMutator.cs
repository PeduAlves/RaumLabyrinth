using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Muta o labirinto em tempo de execução: a cada intervalo (aleatório, configurável),
/// escolhe células próximas ao player (no mesmo alcance do MazeChunkManager) e adiciona/
/// remove paredes aleatoriamente.
///
/// Conectividade garantida por MazeGenerator.TrySetPassage:
///  • remover parede é sempre seguro;
///  • adicionar parede só é aplicado se as células continuarem ligadas por outro caminho
///    (busca limitada), então nenhuma célula — nem a do player — fica inacessível.
///
/// Suavidade: as criações/destruições de parede são espalhadas em vários frames
/// (applyChangesPerFrame) em vez de tudo de uma vez, evitando o "congelamento". O navmesh
/// é atualizado dinamicamente pelo NavMeshObstacle (carving) de cada parede — sem rebake.
///
/// SETUP: coloque num GameObject e arraste o MazeGenerator e o MazeChunkManager.
/// </summary>
public class MazeMutator : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private MazeGenerator generator;
    [SerializeField] private MazeChunkManager chunkManager;

    [Header("Intervalo entre mutações (segundos)")]
    [SerializeField] private float minInterval = 8f;
    [SerializeField] private float maxInterval = 15f;

    [Header("Intensidade")]
    [Tooltip("Quantas células são mutadas por evento")]
    [SerializeField] private int cellsPerMutation = 4;
    [Range(0f, 1f)]
    [Tooltip("Chance de cada parede da célula tentar mudar de estado")]
    [SerializeField] private float wallChangeChance = 0.5f;

    [Header("Suavidade")]
    [Tooltip("Quantas paredes são criadas/destruídas por frame. Menor = mais suave (espalha em mais frames).")]
    [SerializeField] private int applyChangesPerFrame = 1;

    [Header("Conectividade")]
    [Tooltip("Máx. de células visitadas na busca que valida se ainda há caminho ao fechar uma parede. " +
             "Maior = fecha mais paredes (mais custo); menor = mais conservador (mais rápido).")]
    [SerializeField] private int maxConnectivitySearchNodes = 80;

    [Header("NavMesh")]
    [Tooltip("DEIXE DESLIGADO ao usar NavMeshObstacle com carving nas paredes (recomendado): o " +
             "navmesh atualiza sozinho. Ligue só como fallback se NÃO usar carving — causa hitch (rebake).")]
    [SerializeField] private bool updateNavMeshOnMutation = false;

    [Header("Aleatoriedade")]
    [Tooltip("0 = semente aleatória a cada execução")]
    [SerializeField] private int seed = 0;

    private System.Random rng;
    private readonly List<Vector2Int> candidateCells = new List<Vector2Int>();
    private readonly List<WallOp> pendingOps = new List<WallOp>();

    private static readonly MazeDir[] MutationDirs = { MazeDir.Top, MazeDir.Right, MazeDir.Bottom, MazeDir.Left };

    private struct WallOp
    {
        public int x, z;
        public MazeDir dir;
        public bool open;
        public WallOp(int x, int z, MazeDir dir, bool open) { this.x = x; this.z = z; this.dir = dir; this.open = open; }
    }

    void Start()
    {
        if (generator == null || chunkManager == null)
        {
            Debug.LogWarning("[MazeMutator] generator/chunkManager não atribuídos. Mutação desativada.");
            enabled = false;
            return;
        }

        rng = seed == 0 ? new System.Random() : new System.Random(seed);
        StartCoroutine(MutationLoop());
    }

    IEnumerator MutationLoop()
    {
        // Espera o labirinto ficar pronto (gerado + navmesh + player em jogo).
        while (!generator.MazeReady) yield return null;

        while (true)
        {
            float wait = minInterval + (float)rng.NextDouble() * Mathf.Max(0f, maxInterval - minInterval);
            yield return new WaitForSeconds(wait);
            yield return MutateRoutine();
        }
    }

    IEnumerator MutateRoutine()
    {
        if (!BuildMutationPlan()) yield break;

        // Aplica as operações espalhadas no tempo. Cada parede criada/destruída é uma
        // operação "cara"; só essas contam para o limite por frame.
        int appliedThisFrame = 0;
        int totalChanges = 0;
        int perFrame = Mathf.Max(1, applyChangesPerFrame);

        for (int i = 0; i < pendingOps.Count; i++)
        {
            WallOp op = pendingOps[i];

            // O estado é reavaliado a cada operação (a checagem de conectividade usa o estado
            // atual), então espalhar no tempo continua seguro: todo frame o labirinto está
            // num estado válido e conectado.
            if (generator.TrySetPassage(op.x, op.z, op.dir, op.open, maxConnectivitySearchNodes))
            {
                totalChanges++;
                appliedThisFrame++;
                if (appliedThisFrame >= perFrame)
                {
                    appliedThisFrame = 0;
                    yield return null; // espalha instanciar/destruir + carving entre frames
                }
            }
        }

        // Fallback (sem carving): rebake síncrono. Causa hitch — por isso default desligado.
        if (totalChanges > 0 && updateNavMeshOnMutation)
            generator.RebuildNavMeshImmediate();
    }

    // Monta a lista de operações (células sorteadas × paredes a tentar mudar). Barato.
    bool BuildMutationPlan()
    {
        pendingOps.Clear();

        Transform player = chunkManager.playerTransform;
        if (player == null) return false;

        Vector2Int playerCell = generator.WorldToCell(player.position);

        // Alcance em células = mesmo range visível do chunk manager.
        int rangeCells = Mathf.Max(1, chunkManager.renderDistance * chunkManager.chunkSizeInCells);

        // Coleta candidatas no alcance, exceto a célula do player (para não trancá-lo).
        candidateCells.Clear();
        for (int dx = -rangeCells; dx <= rangeCells; dx++)
            for (int dz = -rangeCells; dz <= rangeCells; dz++)
            {
                int cx = playerCell.x + dx;
                int cz = playerCell.y + dz;
                if (!generator.InBounds(cx, cz)) continue;
                if (cx == playerCell.x && cz == playerCell.y) continue;
                candidateCells.Add(new Vector2Int(cx, cz));
            }

        if (candidateCells.Count == 0) return false;

        int n = Mathf.Min(cellsPerMutation, candidateCells.Count);
        for (int i = 0; i < n; i++)
        {
            // Sorteia sem repetir (swap-remove com o último).
            int idx = rng.Next(candidateCells.Count);
            Vector2Int cell = candidateCells[idx];
            candidateCells[idx] = candidateCells[candidateCells.Count - 1];
            candidateCells.RemoveAt(candidateCells.Count - 1);

            foreach (MazeDir dir in MutationDirs)
            {
                if (rng.NextDouble() > wallChangeChance) continue;
                bool open = rng.Next(2) == 0; // alvo aleatório: abrir ou fechar
                pendingOps.Add(new WallOp(cell.x, cell.y, dir, open));
            }
        }

        return pendingOps.Count > 0;
    }
}
