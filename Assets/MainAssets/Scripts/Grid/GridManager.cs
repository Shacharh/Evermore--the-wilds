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
    
    /// <summary>
    /// Returns all tiles that fall within an attack's shape and range, excluding
    /// the origin tile itself.
    ///
    /// Shapes:
    ///   sphere  — Manhattan diamond  (|dx|+|dy| ≤ range)
    ///   cube    — Chebyshev square   (max(|dx|,|dy|) ≤ range)
    ///   line    — Orthogonal cross   (|dx|=0 OR |dy|=0)
    ///   column  — Diagonal cross     (|dx|=|dy|)
    /// </summary>
    public List<Tile> GetTilesInAttackShape(Tile origin, int range,
                                             AttackEnum.AttackTargetShape shape)
    {
        var result = new List<Tile>();
        if (origin == null || range <= 0) return result;

        int cx = origin.GridPosition.x;
        int cy = origin.GridPosition.y;

        for (int x = cx - range; x <= cx + range; x++)
        for (int y = cy - range; y <= cy + range; y++)
        {
            if (!IsValidPosition(x, y)) continue;
            if (x == cx && y == cy)    continue;   // exclude origin

            int dx = Mathf.Abs(x - cx);
            int dy = Mathf.Abs(y - cy);

            bool include = shape switch
            {
                AttackEnum.AttackTargetShape.sphere => dx + dy <= range,          // diamond
                AttackEnum.AttackTargetShape.cube   => dx <= range && dy <= range, // square
                AttackEnum.AttackTargetShape.line   => dx == 0 || dy == 0,        // plus / cross
                AttackEnum.AttackTargetShape.column => dx == dy,                  // diagonal cross
                _                                   => dx + dy <= range
            };

            if (include)
                result.Add(grid[x, y]);
        }

        return result;
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

    // ── Obstruction Registry ───────────────────────────────────────────────────
    //
    // Obstruction components call RegisterObstructionTile/UnregisterObstructionTile
    // when they start/destroy.  All queries (IsObstructed, ObstructionsBetween, etc.)
    // look up this set in O(1) rather than scanning the tile grid every frame.

    private HashSet<Vector2Int> _obstructionTiles = new HashSet<Vector2Int>();

    /// <summary>Marks a grid position as containing an obstruction.</summary>
    public void RegisterObstructionTile(Vector2Int pos)   => _obstructionTiles.Add(pos);

    /// <summary>Removes the obstruction flag from a grid position.</summary>
    public void UnregisterObstructionTile(Vector2Int pos) => _obstructionTiles.Remove(pos);

    /// <summary>Returns true if the given grid coordinate holds an obstruction.</summary>
    public bool IsObstructedPosition(Vector2Int pos) => _obstructionTiles.Contains(pos);

    /// <summary>Returns true if <paramref name="t"/> is an obstruction tile.</summary>
    public bool IsObstructed(Tile t) => t != null && _obstructionTiles.Contains(t.GridPosition);

    // ── Line-of-fire queries ───────────────────────────────────────────────────

    /// <summary>
    /// Counts the number of obstruction tiles on the straight (Bresenham) line
    /// between <paramref name="from"/> and <paramref name="to"/>, excluding the
    /// endpoints themselves (attacker and target tiles are not counted).
    /// Returns 0 when there are no obstructions or the inputs are null/equal.
    /// </summary>
    public int ObstructionsBetween(Tile from, Tile to)
    {
        if (from == null || to == null) return 0;
        int count = 0;
        foreach (Vector2Int pos in BresenhamInterior(from.GridPosition, to.GridPosition))
            if (_obstructionTiles.Contains(pos))
                count++;
        return count;
    }

    /// <summary>
    /// Returns the Manhattan distance from <paramref name="referencePoint"/> to the
    /// nearest obstruction tile on the straight line between <paramref name="from"/>
    /// and <paramref name="to"/>.
    /// Returns <see cref="int.MaxValue"/> when there are no obstructions on the line.
    /// </summary>
    public int DistanceToNearestObstruction(Tile from, Tile to, Tile referencePoint)
    {
        if (from == null || to == null || referencePoint == null) return int.MaxValue;
        int nearest = int.MaxValue;
        foreach (Vector2Int pos in BresenhamInterior(from.GridPosition, to.GridPosition))
        {
            if (!_obstructionTiles.Contains(pos)) continue;
            int dist = Mathf.Abs(pos.x - referencePoint.GridPosition.x)
                     + Mathf.Abs(pos.y - referencePoint.GridPosition.y);
            if (dist < nearest) nearest = dist;
        }
        return nearest;
    }

    /// <summary>
    /// Iterates the interior grid positions on the Bresenham line from
    /// <paramref name="a"/> to <paramref name="b"/>, EXCLUDING the endpoints.
    /// Uses integer arithmetic only (no floating-point rounding artifacts).
    /// </summary>
    private IEnumerable<Vector2Int> BresenhamInterior(Vector2Int a, Vector2Int b)
    {
        int x0 = a.x, y0 = a.y;
        int x1 = b.x, y1 = b.y;

        int dx  = Mathf.Abs(x1 - x0), dy = Mathf.Abs(y1 - y0);
        int sx  = x0 < x1 ? 1 : -1,   sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            bool isStart = (x0 == a.x && y0 == a.y);
            bool isEnd   = (x0 == b.x && y0 == b.y);

            // Only yield interior points
            if (!isStart && !isEnd)
                yield return new Vector2Int(x0, y0);

            if (isEnd) yield break;

            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 <  dx) { err += dx; y0 += sy; }
        }
    }
}