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

    // ✅ NOVO: Referência ao ChunkManager
    [Header("Otimização")]
    [Tooltip("Arraste aqui o GameObject com o script MazeChunkManager")]
    public MazeChunkManager chunkManager;

    // Dados internos
    private MazeCell[,] grid;
    private float cellHeight;
    private float cellThickness;
    private List<MazeCell> generationOrder;

    public class MazeCell
    {
        public bool IsVisited = false;
        public bool WallTop = true, WallRight = true, WallBottom = true, WallLeft = true;
        public GameObject WallTopObject, WallRightObject, WallBottomObject, WallLeftObject;
        public float MyWallThickness, MyWallHeight;
        public int X, Z;
        public MazeCell(int x, int z) { X = x; Z = z; }
    }

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

        // Ativa todos os objetos para o NavMesh bake (são todos inativos após o registro)
        chunkManager?.ShowAll();
        enemyNavmeshSurface.BuildNavMesh();
        // O SetPlayer (chamado em SpawnPlayerAndTransition) vai restaurar a visibilidade por chunk

        if (mainCamera != null) SpawnRandomObjects();
        if (mainCamera != null) yield return StartCoroutine(SpawnPlayerAndTransition());
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

                // ✅ NOVO: Registra o piso no chunk correspondente
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

            if (cell.WallTop)
            {
                Vector3 wallPos = position + new Vector3(0, 0, offset);
                // ✅ NOVO: BuildWall agora retorna o objeto e registramos no chunk
                GameObject wall = BuildWall(wallPos, Vector3.zero, length, cell.MyWallThickness, cell.MyWallHeight);
                chunkManager?.RegisterObject(wall, wallPos);
            }

            if (cell.WallRight)
            {
                Vector3 wallPos = position + new Vector3(offset, 0, 0);
                GameObject wall = BuildWall(wallPos, new Vector3(0, 90, 0), length, cell.MyWallThickness, cell.MyWallHeight);
                chunkManager?.RegisterObject(wall, wallPos);
            }

            if (cell.Z == 0 && cell.WallBottom)
            {
                Vector3 wallPos = position + new Vector3(0, 0, -offset);
                GameObject wall = BuildWall(wallPos, Vector3.zero, length, cell.MyWallThickness, cell.MyWallHeight);
                chunkManager?.RegisterObject(wall, wallPos);
            }

            if (cell.X == 0 && cell.WallLeft)
            {
                Vector3 wallPos = position + new Vector3(-offset, 0, 0);
                GameObject wall = BuildWall(wallPos, new Vector3(0, 90, 0), length, cell.MyWallThickness, cell.MyWallHeight);
                chunkManager?.RegisterObject(wall, wallPos);
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

        // ✅ NOVO: Informa o ChunkManager sobre o Transform do player
        // A partir disso, o Update() do ChunkManager começa a controlar a visibilidade
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