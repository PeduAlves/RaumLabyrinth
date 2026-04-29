using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;
using Unity.AI.Navigation;

public class MazeGenerator : MonoBehaviour
{
    [Header("Configurações do Labirinto")]
    public int Width = 30;
    public int Height = 30;
    public int Seed = 0;
    public float spacing = 4f;

    [Header("Game Objects")]
    public List<ObjectSpawnData> ObjectsToSpawn; // Arraste seu item/inimigo aqui
    public NavMeshSurface enemyNavmeshSurface; // Referência ao NavMeshSurface para os inimigos navegarem

    public GameObject run;
    public GameObject map;

    [Header("Camera & Animations")]
    public Camera mainCamera; // Main player camera object
    public Camera mapCamera; // Map camera object
    public float cameraFlyingTime = 2.0f; // 1st person transitioning time
    public int wallPerFrame = 5; // The amount of wall appear per frame (higher number accelerate the process)

    [Header("Prefabs")]
    public GameObject WallPrefab;
    public GameObject FloorPrefab;
    public GameObject PlayerPrefab;
    public GameObject mapUI;

    // Dados internos
    private MazeCell[,] grid;
    private float cellHeight;
    private float cellThickness;
    private List<MazeCell> generationOrder;

    // --- CLASSE MAZE CELL (Mantida igual) ---
    public class MazeCell
    {
        public bool IsVisited = false;
        public bool WallTop = true, WallRight = true, WallBottom = true, WallLeft = true;
        public GameObject WallTopObject, WallRightObject, WallBottomObject, WallLeftObject;
        public float MyWallThickness, MyWallHeight;
        // Adicione isso junto com as outras variáveis privadas
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
        // Iniciamos a sequência cinematográfica
        StartCoroutine(GenerationSequence());
    }

    // --- ORQUESTRADOR DA ANIMAÇÃO ---
 IEnumerator GenerationSequence()
    {
        if(mainCamera != null )PositionAerialCamera();

        // 1. INÍCIO DO CRONÔMETRO DE PERFORMANCE
        Stopwatch timer = new Stopwatch();
        timer.Start();

        GenerateMazeData(); // Gera a matemática
        DrawFloors();       // Desenha o piso instantaneamente

        timer.Stop();
        UnityEngine.Debug.Log($"[Performance] Tempo para gerar matriz ({Width}x{Height}) e instanciar pisos: {timer.ElapsedMilliseconds} ms");

        yield return new WaitForSeconds(0.5f); 

        yield return StartCoroutine(DrawWallsAnimated());

        enemyNavmeshSurface.BuildNavMesh();

        if (mainCamera != null)SpawnRandomObjects();
        if(mainCamera != null)yield return StartCoroutine(SpawnPlayerAndTransition());
    }

    void PositionAerialCamera()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        // Calcula o centro do labirinto
        float centerX = (Width * spacing) / 2f;
        float centerZ = (Height * spacing) / 2f;
        
        // Define uma altura baseada no tamanho do labirinto para caber tudo na tela
        float altura = Mathf.Max(Width, Height) * spacing * 0.8f;

        mainCamera.transform.position = new Vector3(centerX, altura, centerZ);
        mainCamera.transform.rotation = Quaternion.Euler(90, 0, 0); // Olha direto para baixo

        mapCamera.transform.position = new Vector3(centerX, altura + 10f, centerZ);
        mapCamera.transform.rotation = Quaternion.Euler(90, 0, 0); // Olha direto para baixo
    }

    // --- LÓGICA DE GERAÇÃO (Separada do desenho) ---
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
        
        current.MyWallThickness = 10f; 
        current.MyWallHeight = 2f;

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
            }
        }
    }

    // --- DESENHO DAS PAREDES (Animado) ---
    IEnumerator DrawWallsAnimated()
    {
        int count = 0;

        // AQUI MUDOU: Usamos o foreach na lista de histórico em vez dos loops x/z
        foreach (MazeCell cell in generationOrder)
        {
            Vector3 position = new Vector3(cell.X * spacing, 0, cell.Z * spacing);
            float offset = spacing / 2f;
            float length = spacing / 3f;

            // Instancia as paredes (Lógica mantida, mas agora segue a ordem do caminho)
            if (cell.WallTop) BuildWall(position + new Vector3(0, 0, offset), Vector3.zero, length, cell.MyWallThickness, cell.MyWallHeight);
            if (cell.WallRight) BuildWall(position + new Vector3(offset, 0, 0), new Vector3(0, 90, 0), length, cell.MyWallThickness, cell.MyWallHeight);
            if (cell.WallBottom) BuildWall(position + new Vector3(0, 0, -offset), Vector3.zero, length, cell.MyWallThickness, cell.MyWallHeight);
            if (cell.WallLeft) BuildWall(position + new Vector3(-offset, 0, 0), new Vector3(0, 90, 0), length, cell.MyWallThickness, cell.MyWallHeight);

            // Controle de fluxo da animação
            count++;
            if (count >= wallPerFrame)
            {
                count = 0;
                yield return null; 
            }
        }
    }

    GameObject BuildWall(Vector3 pos, Vector3 rot, float length, float thickness, float height)
    {
        GameObject wall = Instantiate(WallPrefab, pos, Quaternion.Euler(rot), transform);
        wall.transform.localScale = new Vector3(length, height, thickness);
        wall.transform.position += new Vector3(0, -1, 0); // Ajuste do pivô
        return wall;
    }

    // --- SPAWN E TRANSIÇÃO ---
    IEnumerator SpawnPlayerAndTransition()
    {
        // 1. Spawna o Player
        Vector3 startPos = new Vector3(0, 2f, 0);
        GameObject player = Instantiate(PlayerPrefab, startPos, Quaternion.identity);
        player.GetComponent<FirstPersonController>().mapaUIPlayer = mapUI;

        // 2. Tenta achar a câmera dentro do Player (caso seja um prefab FPS padrão)
        // Se não achar, usa o próprio transform do player
        Transform cameraTarget = player.transform.GetComponentInChildren<Camera>()?.transform;
        if (cameraTarget == null) cameraTarget = player.transform;

        // Desativa o controle do player durante a transição (opcional, depende do seu script de player)
        // player.GetComponent<PlayerController>().enabled = false; 

        // 3. Animação da Câmera
        Vector3 startCamPos = mainCamera.transform.position;
        Quaternion startCamRot = mainCamera.transform.rotation;
        float elapsed = 0;

        while (elapsed < cameraFlyingTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / cameraFlyingTime;
            
            // Suavização (SmoothStep)
            t = t * t * (3f - 2f * t); 

            mainCamera.transform.position = Vector3.Lerp(startCamPos, cameraTarget.position, t);
            mainCamera.transform.rotation = Quaternion.Slerp(startCamRot, cameraTarget.rotation, t);

            yield return null;
        }

        mainCamera.gameObject.SetActive(false);
        map.gameObject.SetActive(true);
        run.gameObject.SetActive(true);
    }

    // --- FUNÇÕES AUXILIARES MANTIDAS (Lógica do labirinto) ---
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
        if (ObjectsToSpawn.Count == 0|| ObjectsToSpawn == null) return;
        
        System.Random rng = new System.Random(Seed); // Usando a mesma Seed para consistência
        List<string> usedPositions = new List<string>(); // Para evitar 2 objetos no mesmo lugar
        GameObject objectToSpawn; int numberOfObjects;
        float objectHeight;

        for (int i = 0; i < ObjectsToSpawn.Count; i++)
        {
            objectToSpawn = ObjectsToSpawn[i].ObjectToSpawn;
            numberOfObjects = ObjectsToSpawn[i].Quantity;
            objectHeight = ObjectsToSpawn[i].ObjectHight;

            int spawnedCount = 0;


            while (spawnedCount < numberOfObjects)
            {
                // Escolhe uma célula aleatória
                int rX = rng.Next(0, Width);
                int rZ = rng.Next(0, Height);

                // Regra: Não spawnar na posição inicial do Player (0,0)
                if (rX == 0 && rZ == 0) continue;

                // Regra: Não spawnar onde já tem objeto
                string posKey = $"{rX},{rZ}";
                if (usedPositions.Contains(posKey)) continue;

                // Calcula a posição no mundo real
                // Multiplicamos pelo spacing para centralizar no corredor
                Vector3 worldPosition = new Vector3(rX * spacing, objectHeight, rZ * spacing);

                Instantiate(objectToSpawn, worldPosition, Quaternion.identity, transform);

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

                // Se uma célula tem 3 paredes, é um beco sem saída
                if (wallCount == 3)
                {
                    deadEndCount++;
                }
            }
        }
        return deadEndCount;
    }
}