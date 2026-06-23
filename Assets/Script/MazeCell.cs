using UnityEngine;

/// <summary>Direção de uma parede / vizinho de célula.</summary>
public enum MazeDir { Top, Right, Bottom, Left }

public static class MazeDirExtensions
{
    public static MazeDir Opposite(this MazeDir d) => d switch
    {
        MazeDir.Top => MazeDir.Bottom,
        MazeDir.Bottom => MazeDir.Top,
        MazeDir.Right => MazeDir.Left,
        _ => MazeDir.Right
    };

    /// <summary>Deslocamento em X do vizinho nessa direção.</summary>
    public static int DX(this MazeDir d) => d == MazeDir.Right ? 1 : d == MazeDir.Left ? -1 : 0;

    /// <summary>Deslocamento em Z do vizinho nessa direção.</summary>
    public static int DZ(this MazeDir d) => d == MazeDir.Top ? 1 : d == MazeDir.Bottom ? -1 : 0;
}

[System.Serializable]
public class MazeCell
{
    public int X, Z;
    public bool IsVisited = false;

    // true = parede presente naquela direção.
    public bool WallTop = true;
    public bool WallRight = true;
    public bool WallBottom = true;
    public bool WallLeft = true;

    public GameObject WallTopObject;
    public GameObject WallRightObject;
    public GameObject WallBottomObject;
    public GameObject WallLeftObject;

    public float MyWallThickness;
    public float MyWallHeight;

    public MazeCell(int x, int z)
    {
        X = x;
        Z = z;
    }

    // --- Acesso por direção (evita repetir os 4 casos pelo código) ---

    public bool HasWall(MazeDir d) => d switch
    {
        MazeDir.Top => WallTop,
        MazeDir.Right => WallRight,
        MazeDir.Bottom => WallBottom,
        _ => WallLeft
    };

    public void SetWall(MazeDir d, bool present)
    {
        switch (d)
        {
            case MazeDir.Top: WallTop = present; break;
            case MazeDir.Right: WallRight = present; break;
            case MazeDir.Bottom: WallBottom = present; break;
            case MazeDir.Left: WallLeft = present; break;
        }
    }

    public GameObject GetWallObject(MazeDir d) => d switch
    {
        MazeDir.Top => WallTopObject,
        MazeDir.Right => WallRightObject,
        MazeDir.Bottom => WallBottomObject,
        _ => WallLeftObject
    };

    public void SetWallObject(MazeDir d, GameObject go)
    {
        switch (d)
        {
            case MazeDir.Top: WallTopObject = go; break;
            case MazeDir.Right: WallRightObject = go; break;
            case MazeDir.Bottom: WallBottomObject = go; break;
            case MazeDir.Left: WallLeftObject = go; break;
        }
    }
}
