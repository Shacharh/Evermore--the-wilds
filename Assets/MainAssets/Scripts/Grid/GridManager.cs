using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private int gridWidth = 10;
    [SerializeField] private int gridHeight = 10;
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] private float tileSpacing = 1.1f;
    [Tooltip("Visual side length of each tile in world units.\n" +
             "Set this here on the GridManager — the Tile prefab's own scale is\n" +
             "ignored at runtime and has no effect.\n\n" +
             "Typical values:\n" +
             "  = tileSpacing  → seamless (tiles touch edge-to-edge)\n" +
             "  < tileSpacing  → gap between tiles\n" +
             "  > tileSpacing  → overlap (clamped automatically to tileSpacing)")]
    [SerializeField] private float tileSideLength = 1.0f;
    
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

    /// <summary>
    /// Called by the Unity Editor whenever any Inspector field is changed.
    /// Re-applies the tile scale live so the level designer sees the result
    /// immediately without restarting Play Mode.
    /// Grid size / spacing changes still require a Play Mode restart.
    /// </summary>
    void OnValidate()
    {
        // Clamp tileSideLength so it can never exceed tileSpacing (no accidental overlap).
        tileSideLength = Mathf.Clamp(tileSideLength, 0.05f, tileSpacing);

        // grid is null before Start() (Edit Mode / before Play Mode).
        // GenerateGrid() will apply the correct scale when Play Mode starts.
        if (!Application.isPlaying || grid == null) return;

        ApplyTileScales();
    }

    /// <summary>
    /// Computes and applies the correct local scale to every tile in the grid.
    /// Unity Plane mesh = 10×10 local units, so: localScale = tileSideLength / 10.
    /// Called once at the end of GenerateGrid() and live from OnValidate().
    /// </summary>
    private void ApplyTileScales()
    {
        float tileScale = tileSideLength / 10f;
        for (int x = 0; x < gridWidth; x++)
        for (int y = 0; y < gridHeight; y++)
        {
            if (grid[x, y] != null)
                grid[x, y].transform.localScale = new Vector3(tileScale, tileScale, tileScale);
        }
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

        // Apply the correct tile scale now that all tiles exist.
        // Unity Plane mesh = 10×10 local units, so scale = (spacing × fill) / 10.
        ApplyTileScales();

        Debug.Log($"Grid generated: {gridWidth}x{gridHeight}");
    }
    
    void SyncCameraWithGrid()
    {
        CameraController camController = FindFirstObjectByType<CameraController>();
        if (camController != null)
        {
            camController.SetGridBounds(gridWidth, gridHeight, tileSpacing);
        }
        else
        {
            Debug.LogWarning("[GridManager] CameraController not found in scene — camera not centred.");
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

    // Find the tile that is currently occupied by a specific GameObject.
    // Compares root GameObjects so it works whether the Monster component sits
    // on the prefab root or on a child object.
    public Tile FindTileOccupiedBy(GameObject occupant)
    {
        if (occupant == null) return null;
        GameObject occupantRoot = occupant.transform.root.gameObject;
        for (int x = 0; x < gridWidth; x++)
            for (int y = 0; y < gridHeight; y++)
            {
                GameObject occ = grid[x, y].OccupyingObject;
                if (occ != null && occ.transform.root.gameObject == occupantRoot)
                    return grid[x, y];
            }
        return null;
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
    
    // ── BFS Pathfinding ────────────────────────────────────────────────────────

    /// <summary>
    /// BFS shortest path from <paramref name="from"/> to <paramref name="to"/>.
    /// For non-flying monsters, obstructed tiles block movement.
    /// For flying monsters, all tiles are traversable except the destination if occupied.
    /// Returns the ordered list of tiles from the step AFTER <paramref name="from"/> up to
    /// and including <paramref name="to"/>, or null if no path exists.
    /// </summary>
    public List<Tile> FindPath(Tile from, Tile to, bool isFlying = false)
    {
        if (from == null || to == null) return null;
        if (from == to) return new List<Tile>();

        var visited = new Dictionary<Tile, Tile>(); // tile → parent
        var queue   = new Queue<Tile>();

        visited[from] = null;
        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            Tile current = queue.Dequeue();
            if (current == to) break;

            foreach (Tile nb in GetNeighbors(current, includeDiagonals: false))
            {
                if (visited.ContainsKey(nb)) continue;

                // Flying: can pass over terrain obstructions but not through creatures.
                // Grounded: must enter only fully-walkable tiles at every step.
                bool canEnter = isFlying
                    ? (nb == to
                        ? nb.IsWalkable()                                     // destination: must be empty/trap
                        : nb.Occupation != Tile.OccupationType.Obstruction)   // intermediate: blocked by monsters, not by terrain
                    : nb.IsWalkable();

                if (!canEnter) continue;

                visited[nb] = current;
                queue.Enqueue(nb);
            }
        }

        if (!visited.ContainsKey(to)) return null; // no path

        // Reconstruct path (from step after origin to destination)
        var path = new List<Tile>();
        Tile step = to;
        while (step != from)
        {
            path.Add(step);
            step = visited[step];
        }
        path.Reverse();
        return path;
    }

    /// <summary>
    /// Returns the actual number of steps in the walkable path from
    /// <paramref name="from"/> to <paramref name="to"/>, respecting obstructions.
    /// Returns <see cref="int.MaxValue"/> if the destination is unreachable.
    /// </summary>
    public int GetPathLength(Tile from, Tile to, bool isFlying = false)
    {
        if (from == null || to == null) return int.MaxValue;
        if (from == to) return 0;
        var path = FindPath(from, to, isFlying);
        return path != null ? path.Count : int.MaxValue;
    }

    /// <summary>
    /// Returns all tiles reachable within <paramref name="range"/> actual BFS steps
    /// from <paramref name="origin"/>, respecting walls and obstructions.
    /// Replaces the old Manhattan-distance flood which ignored walls.
    /// </summary>
    public List<Tile> GetTilesInRange(Tile origin, int range,
                                       bool walkableOnly = true, bool isFlying = false)
    {
        var result  = new List<Tile>();
        var visited = new Dictionary<Tile, int>(); // tile → steps from origin
        var queue   = new Queue<Tile>();

        visited[origin] = 0;
        queue.Enqueue(origin);

        while (queue.Count > 0)
        {
            Tile current = queue.Dequeue();
            int  steps   = visited[current];

            if (steps >= range) continue;

            foreach (Tile nb in GetNeighbors(current, includeDiagonals: false))
            {
                if (visited.ContainsKey(nb)) continue;

                bool canEnter = isFlying ? nb.IsWalkable() : nb.IsWalkable();
                // Flying would allow passing through obstructions mid-path;
                // for simplicity flying monsters use the same IsWalkable check
                // (obstructions block landing, not flight — the distinction matters
                //  for pathfinding only: we still find the shortest aerial route).
                if (!isFlying && IsObstructed(nb)) canEnter = false;

                if (!canEnter) continue;

                visited[nb] = steps + 1;
                queue.Enqueue(nb);
                if (!walkableOnly || nb.IsWalkable())
                    result.Add(nb);
            }
        }

        return result;
    }
    
    /// <summary>
    /// Returns all tiles that fall within an attack's shape and range, excluding
    /// the origin tile itself.
    ///
    /// Non-directional shapes:
    ///   sphere  — Manhattan diamond  (|dx|+|dy| ≤ range)
    ///   cube    — Chebyshev square   (max(|dx|,|dy|) ≤ range)
    ///   cross   — Cardinal plus (+)  (same row OR same column, ≤ range tiles away)
    ///
    /// Directional shapes (require <paramref name="directionTile"/>; fall back to
    /// sphere when none is supplied, e.g. for AI calls that have no cursor aim):
    ///   line    — Directional ray    — range tiles along the aim direction
    ///   column  — Perpendicular line — runs ⊥ to aim, centred on the adjacent tile
    ///   cone    — Bowling-pin cone   — expands 1 tile wider per step of depth
    /// </summary>
    public List<Tile> GetTilesInAttackShape(Tile origin, int range,
                                             AttackEnum.AttackTargetShape shape,
                                             Tile directionTile = null)
    {
        var result = new List<Tile>();
        if (origin == null || range <= 0) return result;

        int cx = origin.GridPosition.x;
        int cy = origin.GridPosition.y;

        bool isDirectional = shape == AttackEnum.AttackTargetShape.line   ||
                             shape == AttackEnum.AttackTargetShape.column ||
                             shape == AttackEnum.AttackTargetShape.cone;

        // ── Non-directional shapes (or directional without an aim → sphere fallback) ──
        if (!isDirectional || directionTile == null)
        {
            for (int x = cx - range; x <= cx + range; x++)
            for (int y = cy - range; y <= cy + range; y++)
            {
                if (!IsValidPosition(x, y)) continue;
                if (x == cx && y == cy)    continue;   // exclude origin

                int dx = Mathf.Abs(x - cx);
                int dy = Mathf.Abs(y - cy);

                bool include = shape switch
                {
                    AttackEnum.AttackTargetShape.sphere => dx + dy <= range,
                    AttackEnum.AttackTargetShape.cube   => true,            // loop bounds already enforce ≤ range in both axes
                    AttackEnum.AttackTargetShape.cross  => dx == 0 || dy == 0,
                    _                                   => dx + dy <= range // directional fallback → sphere
                };

                if (include) result.Add(grid[x, y]);
            }
            return result;
        }

        // ── Directional shapes — compute aim vector ────────────────────────────
        int ddx = (int)Mathf.Sign(directionTile.GridPosition.x - cx);
        int ddy = (int)Mathf.Sign(directionTile.GridPosition.y - cy);

        // Perpendicular direction (90° CCW rotation of the aim vector)
        int pdx = -ddy;
        int pdy =  ddx;

        switch (shape)
        {
            case AttackEnum.AttackTargetShape.line:
            {
                // Ray of tiles at distance 1…range along the aim direction.
                // Stops early if the ray leaves the grid.
                for (int r = 1; r <= range; r++)
                {
                    int tx = cx + r * ddx;
                    int ty = cy + r * ddy;
                    if (!IsValidPosition(tx, ty)) break;
                    result.Add(grid[tx, ty]);
                }
                break;
            }

            case AttackEnum.AttackTargetShape.column:
            {
                // Perpendicular line centred on the tile one step in the aim direction.
                // Extends ±range tiles along the perpendicular axis (total = 2*range+1 wide).
                int fcx = cx + ddx;
                int fcy = cy + ddy;
                for (int s = -range; s <= range; s++)
                {
                    int tx = fcx + s * pdx;
                    int ty = fcy + s * pdy;
                    if (!IsValidPosition(tx, ty)) continue;
                    result.Add(grid[tx, ty]);
                }
                break;
            }

            case AttackEnum.AttackTargetShape.cone:
            {
                // Bowling-pin cone starting one tile ahead of the caster.
                // Row 0 (adjacent tile) is 1 tile wide; each deeper row adds 1 on each side.
                // Total depth = range rows.
                int startX = cx + ddx;
                int startY = cy + ddy;
                for (int r = 0; r < range; r++)
                for (int s = -r; s <= r; s++)
                {
                    int tx = startX + r * ddx + s * pdx;
                    int ty = startY + r * ddy + s * pdy;
                    if (!IsValidPosition(tx, ty)) continue;
                    result.Add(grid[tx, ty]);
                }
                break;
            }
        }

        return result;
    }

    /// <summary>
    /// Returns all tiles along the eight straight lines (4 cardinal + 4 diagonal)
    /// radiating from <paramref name="origin"/>, up to <paramref name="maxRange"/>
    /// steps in each direction.
    /// Used by the UI to shade valid aiming directions for directional attack shapes.
    /// </summary>
    public List<Tile> GetStraightTilesInRange(Tile origin, int maxRange)
    {
        var result = new List<Tile>();
        if (origin == null || maxRange <= 0) return result;

        int cx = origin.GridPosition.x;
        int cy = origin.GridPosition.y;

        int[] steps = { -1, 0, 1 };
        foreach (int dx in steps)
        foreach (int dy in steps)
        {
            if (dx == 0 && dy == 0) continue;   // skip the "no movement" direction
            for (int r = 1; r <= maxRange; r++)
            {
                int tx = cx + r * dx;
                int ty = cy + r * dy;
                if (!IsValidPosition(tx, ty)) break;
                result.Add(grid[tx, ty]);
            }
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