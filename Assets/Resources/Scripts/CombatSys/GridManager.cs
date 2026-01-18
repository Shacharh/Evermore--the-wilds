using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private int gridWidth = 10;
    [SerializeField] private int gridHeight = 10;
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] private float tileSpacing = 1.1f;

    private Tile[,] grid;

    // Public accessors for other systems
    public int GridWidth => gridWidth;
    public int GridHeight => gridHeight;
    public float TileSpacing => tileSpacing;

    void Start()
    {
        GenerateGrid();
        SyncCameraWithGrid();
    }

    void GenerateGrid()
    {
        grid = new Tile[gridWidth, gridHeight];

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                // Calculate world position
                Vector3 worldPosition = new Vector3(x * tileSpacing, 0, y * tileSpacing);

                // Instantiate tile
                GameObject tileObject = Instantiate(tilePrefab, worldPosition, Quaternion.identity);
                tileObject.name = $"Tile ({x}, {y})";
                tileObject.transform.parent = transform;

                // Initialize tile with grid height for side coloring
                Tile tile = tileObject.GetComponent<Tile>();
                tile.Initialize(x, y, gridHeight);

                // Store in array
                grid[x, y] = tile;
            }
        }

        Debug.Log($"Grid generated: {gridWidth}x{gridHeight}");
    }

    void SyncCameraWithGrid()
    {
        CameraController camController = Camera.main.GetComponent<CameraController>();
        if (camController != null)
        {
            camController.SetGridBounds(gridWidth, gridHeight, tileSpacing);
            Debug.Log("Camera synced with grid bounds");
        }
        else
        {
            Debug.LogWarning("CameraController not found on Main Camera!");
        }
    }

    // Utility method to get a tile at grid coordinates
    public Tile GetTile(int x, int y)
    {
        if (IsValidPosition(x, y))
        {
            return grid[x, y];
        }
        return null;
    }

    // Utility method to check if coordinates are valid
    public bool IsValidPosition(int x, int y)
    {
        return x >= 0 && x < gridWidth && y >= 0 && y < gridHeight;
    }

    // Get tile at world position
    public Tile GetTileAtWorldPosition(Vector3 worldPosition)
    {
        int x = Mathf.RoundToInt(worldPosition.x / tileSpacing);
        int y = Mathf.RoundToInt(worldPosition.z / tileSpacing);
        return GetTile(x, y);
    }

    // Calculate distance between two tiles (Manhattan distance for grid movement)
    public int GetDistanceBetweenTiles(Tile tileA, Tile tileB)
    {
        return Mathf.Abs(tileA.GridPosition.x - tileB.GridPosition.x) +
               Mathf.Abs(tileA.GridPosition.y - tileB.GridPosition.y);
    }
}