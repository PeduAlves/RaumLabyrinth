using UnityEngine;

[System.Serializable]
public class MazeCell
{
    public int X, Z;
    public bool IsVisited = false;

    public bool WallTop = true;
    public bool WallRight = true;
    public bool WallBottom = true;
    public bool WallLeft = true;

    public GameObject WallTopObject;
    public GameObject WallBottomObject;
    public GameObject WallRightObject;
    public GameObject WallLeftObject;

    public float MyWallThickness; 
    public float MyWallHeight;

    public MazeCell(int x, int z)
    {
        X = x;
        Z = z;
    }
}