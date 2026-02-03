using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private int gridWidth = 10;
    [SerializeField] private int gridHeight = 10;
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] private float tileSpacing = 1.1f;
    
    private Tile[,] grid;
    
    // Public accessors
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
                Vector3 worldPosition = new Vector3(x * tileSpacing, 0, y * tileSpacing);
                
                GameObject tileObject = Instantiate(tilePrefab, worldPosition, Quaternion.identity);
                tileObject.name = $"Tile ({x}, {y})";
                tileObject.transform.parent = transform;
                
                // Set layer for raycasting
                tileObject.layer = LayerMask.NameToLayer("Tile");
                
                Tile tile = tileObject.GetComponent<Tile>();
                tile.Initialize(x, y, gridHeight);
                
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
        }
    }
    
    // Get tile at grid coordinates
    public Tile GetTile(int x, int y)
    {
        if (IsValidPosition(x, y))
            return grid[x, y];
        return null;
    }
    
    // Get tile at world position
    public Tile GetTileAtWorldPosition(Vector3 worldPosition)
    {
        int x = Mathf.RoundToInt(worldPosition.x / tileSpacing);
        int y = Mathf.RoundToInt(worldPosition.z / tileSpacing);
        return GetTile(x, y);
    }
    
    // Check if coordinates are valid
    public bool IsValidPosition(int x, int y)
    {
        return x >= 0 && x < gridWidth && y >= 0 && y < gridHeight;
    }
    
    // Calculate Manhattan distance
    public int GetDistanceBetweenTiles(Tile tileA, Tile tileB)
    {
        return Mathf.Abs(tileA.GridPosition.x - tileB.GridPosition.x) +
               Mathf.Abs(tileA.GridPosition.y - tileB.GridPosition.y);
    }
    
    // Get neighbors of a tile
    public List<Tile> GetNeighbors(Tile tile, bool includeDiagonals = false)
    {
        List<Tile> neighbors = new List<Tile>();
        int x = tile.GridPosition.x;
        int y = tile.GridPosition.y;
        
        // Cardinal directions
        AddNeighborIfValid(neighbors, x + 1, y);
        AddNeighborIfValid(neighbors, x - 1, y);
        AddNeighborIfValid(neighbors, x, y + 1);
        AddNeighborIfValid(neighbors, x, y - 1);
        
        // Diagonals
        if (includeDiagonals)
        {
            AddNeighborIfValid(neighbors, x + 1, y + 1);
            AddNeighborIfValid(neighbors, x + 1, y - 1);
            AddNeighborIfValid(neighbors, x - 1, y + 1);
            AddNeighborIfValid(neighbors, x - 1, y - 1);
        }
        
        return neighbors;
    }
    
    private void AddNeighborIfValid(List<Tile> list, int x, int y)
    {
        if (IsValidPosition(x, y))
        {
            list.Add(grid[x, y]);
        }
    }
    
    // Get tiles within range (for movement/attack range)
    public List<Tile> GetTilesInRange(Tile centerTile, int range, bool walkableOnly = true)
    {
        List<Tile> tilesInRange = new List<Tile>();
        int centerX = centerTile.GridPosition.x;
        int centerY = centerTile.GridPosition.y;
        
        for (int x = centerX - range; x <= centerX + range; x++)
        {
            for (int y = centerY - range; y <= centerY + range; y++)
            {
                if (!IsValidPosition(x, y)) continue;
                
                Tile tile = grid[x, y];
                int distance = GetDistanceBetweenTiles(centerTile, tile);
                
                if (distance <= range && distance > 0)
                {
                    if (!walkableOnly || tile.IsWalkable())
                    {
                        tilesInRange.Add(tile);
                    }
                }
            }
        }
        
        return tilesInRange;
    }
    
    // Highlight tiles (useful for showing movement range, attack range, etc.)
    public void HighlightTiles(List<Tile> tiles, Color highlightColor, float heightOffset = 0.1f)
    {
        foreach (Tile tile in tiles)
        {
            tile.Highlight(highlightColor, heightOffset);
        }
    }
    
    // Clear all highlights
    public void ClearAllHighlights()
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                grid[x, y].ResetVisuals();
            }
        }
    }
}