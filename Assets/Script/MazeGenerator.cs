using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;
using Unity.AI.Navigation;

public class MazeGenerator : MonoBehaviour
{
    [Header("Maze configuration")]
    public int Width = 30;
    public int Height = 30;
    public int Seed = 0;
    public float spacing = 4f;
    [Range(0.01f, 0.5f)] public float wallThicknessRatio = 0.1f;
    [Range(0.1f, 5f)] public float wallHeightRatio = 0.8f;

    [Header("Game Objects")]
    public List<ObjectSpawnData> ObjectsToSpawn;
    public NavMeshSurface enemyNavmeshSurface;

    public GameObject run;
    public GameObject map;

    [Header("Camera & Animations")]
    public Camera mainCamera;
    public Camera mapCamera;
    public float cameraFlyingTime = 2.0f;
    public int wallPerFrame = 5;

    [Header("Prefabs")]
    public GameObject WallPrefab;
    public GameObject FloorPrefab;
    public GameObject PlayerPrefab;
    public GameObject mapUI;

    [Header("Vitória")]
    [Tooltip("Torre que, ao ser tocada pelo player, vence o jogo")]
    public GameObject victoryTowerPrefab;
    [Tooltip("Altura (Y) onde a torre é instanciada")]
    [SerializeField] private float victoryTowerHeight = 0f;
    [Tooltip("Distância mínima do spawn, de 0 a 1 do máximo, para a torre ficar longe do início")]
    [Range(0f, 1f)][SerializeField] private float victoryMinDistancePercent = 0.6f;
    [Tooltip("Raio do gatilho de toque (usado só se a torre não tiver um VictoryTrigger no prefab)")]
    [SerializeField] private float victoryTouchRadius = 1.5f;

    // ✅ NOVO: Referência ao ChunkManager
    [Header("Otimização")]
    [Tooltip("Arraste aqui o GameObject com o script MazeChunkManager")]
    public MazeChunkManager chunkManager;

    // Dados internos
    private MazeCell[,] grid;
    private List<MazeCell> generationOrder;

    // True quando a geração + bake do NavMesh + spawn terminaram (usado pelo MazeMutator).
    public bool MazeReady { get; private set; }

    [System.Serializable]
    public struct ObjectSpawnData
    {
        public GameObject ObjectToSpawn;
        public int Quantity;
        public float ObjectHight;
    }

    void Start()
    {
        StartCoroutine(GenerationSequence());
    }

    IEnumerator GenerationSequence()
    {
        if (mainCamera != null) PositionAerialCamera();

        // Inicializa o ChunkManager ANTES de registrar qualquer objeto,
        // para que o spacing correto seja usado no WorldToChunk durante o registro.
        if (chunkManager != null)
            chunkManager.Initialize(spacing);

        Stopwatch timer = new Stopwatch();
        timer.Start();

        GenerateMazeData();
        DrawFloors();

        timer.Stop();
        UnityEngine.Debug.Log($"[Performance] Tempo para gerar matriz ({Width}x{Height}) e instanciar pisos: {timer.ElapsedMilliseconds} ms");

        yield return new WaitForSeconds(0.5f);

        yield return StartCoroutine(DrawWallsAnimated());

        UnityEngine.Debug.Log($"[Performance] Total de objetos registrados nos chunks: {chunkManager?.GetTotalRegisteredObjects()}");

        // Garante o labirinto inteiro ativo para o bake do NavMesh (geometria completa).
        // O culling por chunks só começa em BeginCulling(), quando o player assume o controle.
        chunkManager?.ShowAll();
        enemyNavmeshSurface.BuildNavMesh();

        if (mainCamera != null) SpawnRandomObjects();
        if (mainCamera != null) SpawnVictoryTower();
        if (mainCamera != null) yield return StartCoroutine(SpawnPlayerAndTransition());

        // Labirinto pronto: libera o MazeMutator a começar.
        MazeReady = true;
    }

    void PositionAerialCamera()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        float centerX = (Width * spacing) / 2f;
        float centerZ = (Height * spacing) / 2f;
        float altura = Mathf.Max(Width, Height) * spacing * 0.8f;

        mainCamera.transform.position = new Vector3(centerX, altura, centerZ);
        mainCamera.transform.rotation = Quaternion.Euler(90, 0, 0);

        mapCamera.transform.position = new Vector3(centerX, altura + 10f, centerZ);
        mapCamera.transform.rotation = Quaternion.Euler(90, 0, 0);
    }

    public void GenerateMazeData()
    {
        if (Seed == 0)
        {
            Seed = UnityEngine.Random.Range(1, 1000000);
            UnityEngine.Debug.Log("Seed 0 detectada. Seed aleatória gerada: " + Seed);
        }
        System.Random rng = new System.Random(Seed);
        grid = new MazeCell[Width, Height];
        generationOrder = new List<MazeCell>();

        for (int x = 0; x < Width; x++)
            for (int z = 0; z < Height; z++)
                grid[x, z] = new MazeCell(x, z);

        Stack<MazeCell> stack = new Stack<MazeCell>();
        MazeCell current = grid[0, 0];

        current.IsVisited = true;
        generationOrder.Add(current);

        current.MyWallThickness = spacing * wallThicknessRatio;
        current.MyWallHeight = spacing * wallHeightRatio;

        stack.Push(current);

        while (stack.Count > 0)
        {
            current = stack.Peek();
            List<MazeCell> neighbors = GetUnvisitedNeighbors(current);

            if (neighbors.Count > 0)
            {
                MazeCell neighbor = neighbors[rng.Next(neighbors.Count)];
                RemoveWalls(current, neighbor);

                neighbor.IsVisited = true;
                generationOrder.Add(neighbor);

                neighbor.MyWallThickness = current.MyWallThickness;
                neighbor.MyWallHeight = current.MyWallHeight;

                stack.Push(neighbor);
            }
            else
            {
                stack.Pop();
            }
        }

        int becosAntes = CountDeadEnds();
        RemoveDeadEnds();
        int becosDepois = CountDeadEnds();

        UnityEngine.Debug.Log($"[Design] Becos sem saída ANTES do Braiding: {becosAntes}");
        UnityEngine.Debug.Log($"[Design] Becos sem saída DEPOIS do Braiding (Taxa {BraidingRate}%): {becosDepois}");
        UnityEngine.Debug.Log($"[Design] Total de becos removidos: {becosAntes - becosDepois}");
    }

    void DrawFloors()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int z = 0; z < Height; z++)
            {
                Vector3 position = new Vector3(x * spacing, 0, z * spacing);
                GameObject floor = Instantiate(FloorPrefab, position, Quaternion.identity, transform);
                floor.transform.localScale = new Vector3(spacing, 1, spacing);

                // Piso é cullável: o prefab carrega muita grama animada (cara), então
                // cullar o floor por distância também desliga a grama. É seguro pois o
                // NavMesh é baked uma vez e persiste mesmo com o floor inativo, e nem o
                // player (chunk próprio sempre ativo) nem os inimigos (já pousados e
                // guiados pelo NavMeshAgent) dependem do colisor do chão distante.
                chunkManager?.RegisterObject(floor, position);
            }
        }
    }

    IEnumerator DrawWallsAnimated()
    {
        int count = 0;

        foreach (MazeCell cell in generationOrder)
        {
            Vector3 position = new Vector3(cell.X * spacing, 0, cell.Z * spacing);
            float offset = spacing / 2f;
            float length = spacing;

            // Guarda o GameObject na célula (WallTopObject/WallRightObject/...) para que a
            // mutação em runtime consiga localizar e destruir/criar a parede compartilhada.
            // Paredes são registradas como NÃO-culláveis (cullable: false): ficam sempre
            // ativas (são baratas; o caro é a grama do piso), então o NavMeshObstacle de
            // cada parede faz o carving o tempo todo e o navmesh reflete as mutações sem rebake.
            if (cell.WallTop)
            {
                Vector3 wallPos = position + new Vector3(0, 0, offset);
                GameObject wall = BuildWall(wallPos, Vector3.zero, length, cell.MyWallThickness, cell.MyWallHeight);
                cell.WallTopObject = wall;
                chunkManager?.RegisterObject(wall, wallPos, cullable: false);
            }

            if (cell.WallRight)
            {
                Vector3 wallPos = position + new Vector3(offset, 0, 0);
                GameObject wall = BuildWall(wallPos, new Vector3(0, 90, 0), length, cell.MyWallThickness, cell.MyWallHeight);
                cell.WallRightObject = wall;
                chunkManager?.RegisterObject(wall, wallPos, cullable: false);
            }

            if (cell.Z == 0 && cell.WallBottom)
            {
                Vector3 wallPos = position + new Vector3(0, 0, -offset);
                GameObject wall = BuildWall(wallPos, Vector3.zero, length, cell.MyWallThickness, cell.MyWallHeight);
                cell.WallBottomObject = wall;
                chunkManager?.RegisterObject(wall, wallPos, cullable: false);
            }

            if (cell.X == 0 && cell.WallLeft)
            {
                Vector3 wallPos = position + new Vector3(-offset, 0, 0);
                GameObject wall = BuildWall(wallPos, new Vector3(0, 90, 0), length, cell.MyWallThickness, cell.MyWallHeight);
                cell.WallLeftObject = wall;
                chunkManager?.RegisterObject(wall, wallPos, cullable: false);
            }

            count++;
            if (count >= wallPerFrame)
            {
                count = 0;
                yield return null;
            }
        }
    }

    // ✅ ATENÇÃO: BuildWall agora retorna o GameObject (antes era void)
    // Essa é a ÚNICA mudança na assinatura do método
    GameObject BuildWall(Vector3 pos, Vector3 rot, float length, float thickness, float height)
    {
        GameObject wall = Instantiate(WallPrefab, pos, Quaternion.Euler(rot), transform);
        wall.transform.localScale = new Vector3(length, height, thickness);
        wall.transform.position += new Vector3(0, -1, 0);
        return wall; // ✅ NOVO: retorna o objeto para podermos registrar no chunk
    }

    IEnumerator SpawnPlayerAndTransition()
    {
        Vector3 startPos = new Vector3(0, 2f, 0);
        GameObject player = Instantiate(PlayerPrefab, startPos, Quaternion.identity);
        player.GetComponent<FirstPersonController>().mapaUIPlayer = mapUI;

        // Só registra o player no ChunkManager. O culling ainda NÃO começa: o labirinto
        // inteiro continua visível durante toda a transição da câmera aérea.
        if (chunkManager != null)
        {
            chunkManager.SetPlayer(player.transform);
        }

        Transform cameraTarget = player.transform.GetComponentInChildren<Camera>()?.transform;
        if (cameraTarget == null) cameraTarget = player.transform;

        Vector3 startCamPos = mainCamera.transform.position;
        Quaternion startCamRot = mainCamera.transform.rotation;
        float elapsed = 0;

        while (elapsed < cameraFlyingTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / cameraFlyingTime;
            t = t * t * (3f - 2f * t);

            mainCamera.transform.position = Vector3.Lerp(startCamPos, cameraTarget.position, t);
            mainCamera.transform.rotation = Quaternion.Slerp(startCamRot, cameraTarget.rotation, t);

            yield return null;
        }

        mainCamera.gameObject.SetActive(false);
        map.gameObject.SetActive(true);
        run.gameObject.SetActive(true);

        // Player em jogo: agora sim ativa o culling por chunks.
        if (chunkManager != null) chunkManager.BeginCulling();
    }

    List<MazeCell> GetUnvisitedNeighbors(MazeCell cell)
    {
        List<MazeCell> neighbors = new List<MazeCell>();
        if (cell.X + 1 < Width && !grid[cell.X + 1, cell.Z].IsVisited) neighbors.Add(grid[cell.X + 1, cell.Z]);
        if (cell.X - 1 >= 0 && !grid[cell.X - 1, cell.Z].IsVisited) neighbors.Add(grid[cell.X - 1, cell.Z]);
        if (cell.Z + 1 < Height && !grid[cell.X, cell.Z + 1].IsVisited) neighbors.Add(grid[cell.X, cell.Z + 1]);
        if (cell.Z - 1 >= 0 && !grid[cell.X, cell.Z - 1].IsVisited) neighbors.Add(grid[cell.X, cell.Z - 1]);
        return neighbors;
    }

    void RemoveWalls(MazeCell a, MazeCell b)
    {
        if (a.X < b.X) { a.WallRight = false; b.WallLeft = false; }
        else if (a.X > b.X) { a.WallLeft = false; b.WallRight = false; }
        else if (a.Z < b.Z) { a.WallTop = false; b.WallBottom = false; }
        else if (a.Z > b.Z) { a.WallBottom = false; b.WallTop = false; }
    }

    public int BraidingRate = 10;
    void RemoveDeadEnds()
    {
        System.Random rng = new System.Random(Seed);
        for (int x = 0; x < Width; x++)
        {
            for (int z = 0; z < Height; z++)
            {
                MazeCell cell = grid[x, z];
                int wallCount = 0;
                if (cell.WallTop) wallCount++; if (cell.WallBottom) wallCount++;
                if (cell.WallRight) wallCount++; if (cell.WallLeft) wallCount++;
                if (wallCount == 3 && rng.Next(0, 100) < BraidingRate)
                {
                    if (cell.WallTop && z + 1 < Height) { cell.WallTop = false; grid[x, z + 1].WallBottom = false; }
                    else if (cell.WallRight && x + 1 < Width) { cell.WallRight = false; grid[x + 1, z].WallLeft = false; }
                }
            }
        }
    }

    void SpawnRandomObjects()
    {
        if (ObjectsToSpawn.Count == 0 || ObjectsToSpawn == null) return;

        System.Random rng = new System.Random(Seed);
        List<string> usedPositions = new List<string>();

        for (int i = 0; i < ObjectsToSpawn.Count; i++)
        {
            GameObject objectToSpawn = ObjectsToSpawn[i].ObjectToSpawn;
            int numberOfObjects = ObjectsToSpawn[i].Quantity;
            float objectHeight = ObjectsToSpawn[i].ObjectHight;
            int spawnedCount = 0;

            while (spawnedCount < numberOfObjects)
            {
                int rX = rng.Next(0, Width);
                int rZ = rng.Next(0, Height);

                if (rX == 0 && rZ == 0) continue;

                string posKey = $"{rX},{rZ}";
                if (usedPositions.Contains(posKey)) continue;

                Vector3 worldPosition = new Vector3(rX * spacing, objectHeight, rZ * spacing);
                GameObject spawned = Instantiate(objectToSpawn, worldPosition, Quaternion.identity, transform);

                // ✅ NOVO: Registra objetos spawnados (grama, itens, etc.) no chunk também
                chunkManager?.RegisterObject(spawned, worldPosition);

                usedPositions.Add(posKey);
                spawnedCount++;
            }
        }
    }

    // Instancia a torre de vitória numa célula longe do spawn. O labirinto é conexo
    // (perfeito + braiding), então qualquer célula é acessível — não precisa checar caminho.
    void SpawnVictoryTower()
    {
        if (victoryTowerPrefab == null)
        {
            UnityEngine.Debug.LogWarning("[MazeGenerator] victoryTowerPrefab não atribuído — sem condição de vitória.");
            return;
        }

        System.Random rng = new System.Random(Seed + 977); // determinístico por seed
        int maxManhattan = (Width - 1) + (Height - 1);
        int minDist = Mathf.RoundToInt(maxManhattan * victoryMinDistancePercent);

        int tx = Width - 1, tz = Height - 1; // fallback: canto oposto (sempre longe e acessível)
        for (int attempt = 0; attempt < 50; attempt++)
        {
            int cx = rng.Next(Width);
            int cz = rng.Next(Height);
            if (cx + cz >= minDist) { tx = cx; tz = cz; break; }
        }

        Vector3 pos = new Vector3(tx * spacing, victoryTowerHeight, tz * spacing);
        GameObject tower = Instantiate(victoryTowerPrefab, pos, Quaternion.identity, transform);
        EnsureVictoryTrigger(tower);

        // Não registramos no ChunkManager: a torre fica sempre ativa (farol do objetivo)
        // e o gatilho funciona mesmo enquanto o player ainda está chegando.
        UnityEngine.Debug.Log($"[MazeGenerator] Torre de vitória posicionada na célula ({tx},{tz}).");
    }

    // Garante que a torre tenha um gatilho de vitória mesmo se o prefab não estiver configurado.
    void EnsureVictoryTrigger(GameObject tower)
    {
        if (tower.GetComponentInChildren<VictoryTrigger>() != null) return; // já configurada no prefab

        SphereCollider trigger = tower.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = victoryTouchRadius;
        tower.AddComponent<VictoryTrigger>();
    }

    // ─────────────────────────────────────────────
    //  API DE MUTAÇÃO EM RUNTIME (usada pelo MazeMutator)
    // ─────────────────────────────────────────────

    public bool InBounds(int x, int z) => x >= 0 && x < Width && z >= 0 && z < Height;

    /// <summary>Converte uma posição de mundo para o índice de célula mais próximo.</summary>
    public Vector2Int WorldToCell(Vector3 world)
        => new Vector2Int(Mathf.RoundToInt(world.x / spacing), Mathf.RoundToInt(world.z / spacing));

    /// <summary>Há passagem (sem parede) entre (x,z) e o vizinho na direção dir?</summary>
    public bool IsPassageOpen(int x, int z, MazeDir dir)
        => InBounds(x, z) && !grid[x, z].HasWall(dir);

    /// <summary>
    /// Tenta abrir (open=true) ou fechar (open=false) a passagem entre a célula (x,z) e o
    /// vizinho na direção dir. Retorna true se algo mudou.
    ///
    /// Garantias:
    ///  • Só mexe em paredes INTERNAS (vizinho válido) — nunca abre o perímetro.
    ///  • ABRIR é sempre seguro (só aumenta a conectividade).
    ///  • FECHAR só é aplicado se as duas células continuarem ligadas por OUTRO caminho
    ///    (busca limitada a maxSearchNodes), garantindo que o labirinto nunca isole a célula.
    /// </summary>
    public bool TrySetPassage(int x, int z, MazeDir dir, bool open, int maxSearchNodes)
    {
        if (!InBounds(x, z)) return false;

        int nx = x + dir.DX();
        int nz = z + dir.DZ();
        if (!InBounds(nx, nz)) return false; // parede de perímetro: não mexe

        bool currentlyOpen = !grid[x, z].HasWall(dir);
        if (open == currentlyOpen) return false; // nada a fazer

        // Fechar: só se sobrar um caminho alternativo entre as duas células.
        if (!open && !PathExistsAvoidingEdge(x, z, nx, nz, maxSearchNodes))
            return false;

        ApplyPassage(x, z, dir, open);
        return true;
    }

    /// <summary>Recalcula o NavMesh imediatamente com a geometria completa, sem deixar
    /// o culling esconder paredes do bake. Síncrono (pode causar um pequeno hitch).</summary>
    public void RebuildNavMeshImmediate()
    {
        if (enemyNavmeshSurface == null) return;

        // Ativa tudo para o bake enxergar todas as paredes/pisos; recula no mesmo frame,
        // então a tela não chega a renderizar o labirinto inteiro (sem "pop", só hitch).
        chunkManager?.ShowAll();
        enemyNavmeshSurface.BuildNavMesh();
        chunkManager?.BeginCulling();
    }

    // --- Internos da mutação ---

    // Aplica a mudança nas DUAS células e gerencia o GameObject compartilhado da parede.
    void ApplyPassage(int x, int z, MazeDir dir, bool open)
    {
        int nx = x + dir.DX();
        int nz = z + dir.DZ();

        grid[x, z].SetWall(dir, !open);
        grid[nx, nz].SetWall(dir.Opposite(), !open);

        // A parede entre duas células é UM só objeto, desenhado na célula "de baixo/esquerda"
        // como Top ou Right. Canonicaliza para achar o dono.
        GetCanonicalWall(x, z, dir, out MazeCell owner, out MazeDir ownerDir);

        if (open)
        {
            GameObject go = owner.GetWallObject(ownerDir);
            if (go != null)
            {
                chunkManager?.UnregisterObject(go, go.transform.position);
                Destroy(go);
                owner.SetWallObject(ownerDir, null);
            }
        }
        else if (owner.GetWallObject(ownerDir) == null)
        {
            GameObject go = BuildEdgeWall(owner, ownerDir);
            owner.SetWallObject(ownerDir, go);
            // Não-cullável, como na geração: mantém o carving da nova parede sempre ativo.
            chunkManager?.RegisterObject(go, go.transform.position, cullable: false);
        }
    }

    // Mapeia (x,z,dir) para a célula/lado que "possui" o GameObject da parede compartilhada.
    void GetCanonicalWall(int x, int z, MazeDir dir, out MazeCell owner, out MazeDir ownerDir)
    {
        switch (dir)
        {
            case MazeDir.Top: owner = grid[x, z]; ownerDir = MazeDir.Top; break;
            case MazeDir.Right: owner = grid[x, z]; ownerDir = MazeDir.Right; break;
            case MazeDir.Bottom: owner = grid[x, z - 1]; ownerDir = MazeDir.Top; break;
            default: owner = grid[x - 1, z]; ownerDir = MazeDir.Right; break; // Left
        }
    }

    // Instancia a parede de uma aresta canônica (Top ou Right do dono), igual à geração.
    GameObject BuildEdgeWall(MazeCell owner, MazeDir ownerDir)
    {
        Vector3 basePos = new Vector3(owner.X * spacing, 0, owner.Z * spacing);
        float offset = spacing / 2f;
        float thickness = spacing * wallThicknessRatio;
        float height = spacing * wallHeightRatio;

        if (ownerDir == MazeDir.Top)
            return BuildWall(basePos + new Vector3(0, 0, offset), Vector3.zero, spacing, thickness, height);

        return BuildWall(basePos + new Vector3(offset, 0, 0), new Vector3(0, 90, 0), spacing, thickness, height);
    }

    // BFS de (ax,az) até (bx,bz) IGNORANDO a aresta direta entre elas, limitado a maxNodes
    // expansões. Usa um "stamp" incremental para não realocar/limpar o array a cada chamada.
    private int[,] bfsStamp;
    private int bfsCounter;
    private readonly Queue<Vector2Int> bfsQueue = new Queue<Vector2Int>();
    private static readonly MazeDir[] AllDirs = { MazeDir.Top, MazeDir.Right, MazeDir.Bottom, MazeDir.Left };

    bool PathExistsAvoidingEdge(int ax, int az, int bx, int bz, int maxNodes)
    {
        if (bfsStamp == null) bfsStamp = new int[Width, Height];
        bfsCounter++;
        bfsQueue.Clear();

        bfsQueue.Enqueue(new Vector2Int(ax, az));
        bfsStamp[ax, az] = bfsCounter;

        int expanded = 0;
        while (bfsQueue.Count > 0 && expanded < maxNodes)
        {
            Vector2Int c = bfsQueue.Dequeue();
            expanded++;

            if (c.x == bx && c.y == bz) return true;

            foreach (MazeDir d in AllDirs)
            {
                if (grid[c.x, c.y].HasWall(d)) continue; // parede fechada: não passa

                int nx = c.x + d.DX();
                int nz = c.y + d.DZ();
                if (!InBounds(nx, nz)) continue;

                // Ignora a aresta direta sob teste (nos dois sentidos).
                if ((c.x == ax && c.y == az && nx == bx && nz == bz) ||
                    (c.x == bx && c.y == bz && nx == ax && nz == az)) continue;

                if (bfsStamp[nx, nz] == bfsCounter) continue;
                bfsStamp[nx, nz] = bfsCounter;
                bfsQueue.Enqueue(new Vector2Int(nx, nz));
            }
        }

        return false;
    }

    private int CountDeadEnds()
    {
        int deadEndCount = 0;
        for (int x = 0; x < Width; x++)
        {
            for (int z = 0; z < Height; z++)
            {
                MazeCell cell = grid[x, z];
                int wallCount = 0;
                if (cell.WallTop) wallCount++;
                if (cell.WallBottom) wallCount++;
                if (cell.WallRight) wallCount++;
                if (cell.WallLeft) wallCount++;

                if (wallCount == 3) deadEndCount++;
            }
        }
        return deadEndCount;
    }
}