using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Place this component on any scene object (rock, tree, wall, etc.) that should
/// act as a blocking obstacle on the grid.
///
/// The level designer sets <see cref="tileOffsets"/> in the Inspector to specify
/// which grid tiles the obstacle covers, relative to the tile beneath the
/// object's root position. (0,0) = just the tile under the pivot.
///
/// Effects on the game:
///   • Covered tiles become OccupationType.Obstruction so IsWalkable() is false
///     — ground monsters cannot enter or pass through them.
///   • Flying monsters (IsFlying = true on Monster) CAN fly over them and are
///     not blocked by obstruction tiles.
///   • Ranged attacks whose Bresenham line crosses an obstruction tile receive
///     accuracy and damage penalties defined in ObstructionConfig.
///
/// The component self-registers with GridManager on Start (one frame deferred
/// so GridManager.Start() always runs first), and unregisters on destroy.
/// </summary>
public class Obstruction : MonoBehaviour
{
    [Tooltip("Grid offsets relative to the tile directly beneath this object's root.\n" +
             "(0,0) = the centre tile only. Add more entries for multi-tile obstacles.\n" +
             "E.g. (0,0) + (1,0) makes a 2-tile-wide wall going right.")]
    [SerializeField] private Vector2Int[] tileOffsets = { Vector2Int.zero };

    private GridManager    gridManager;
    private List<Tile>     coveredTiles = new List<Tile>();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Coroutine: yields one frame so GridManager.Start() finishes its grid
    /// generation before we try to register tiles on it.
    /// </summary>
    private IEnumerator Start()
    {
        yield return null;   // wait one frame

        gridManager = FindFirstObjectByType<GridManager>();
        if (gridManager == null)
        {
            Debug.LogError($"[Obstruction] '{name}': No GridManager found in the scene. " +
                           "Make sure GridManager is present before placing obstructions.");
            yield break;
        }

        RegisterTiles();
    }

    private void OnDestroy()
    {
        UnregisterTiles();
    }

    // ── Registration ──────────────────────────────────────────────────────────

    private void RegisterTiles()
    {
        Tile root = gridManager.GetTileAtWorldPosition(transform.position);
        if (root == null)
        {
            Debug.LogWarning($"[Obstruction] '{name}' is not positioned above any grid tile. " +
                             "Make sure the object sits on the grid plane.");
            return;
        }

        foreach (Vector2Int offset in tileOffsets)
        {
            int tx = root.GridPosition.x + offset.x;
            int ty = root.GridPosition.y + offset.y;
            Tile t = gridManager.GetTile(tx, ty);
            if (t == null)
            {
                Debug.LogWarning($"[Obstruction] '{name}': offset ({offset.x},{offset.y}) " +
                                 $"maps to ({tx},{ty}) which is outside the grid — skipped.");
                continue;
            }

            if (t.SetOccupation(Tile.OccupationType.Obstruction, gameObject))
            {
                coveredTiles.Add(t);
                gridManager.RegisterObstructionTile(t.GridPosition);
                Debug.Log($"[Obstruction] '{name}' registered on tile {t.GridPosition}.");
            }
            else
            {
                Debug.LogWarning($"[Obstruction] '{name}': tile ({tx},{ty}) is already occupied " +
                                 "by something else — skipped.");
            }
        }
    }

    private void UnregisterTiles()
    {
        if (gridManager == null) return;

        foreach (Tile t in coveredTiles)
        {
            if (t == null) continue;
            gridManager.UnregisterObstructionTile(t.GridPosition);
            t.ClearOccupation();
        }
        coveredTiles.Clear();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>All tiles currently marked as blocked by this obstruction.</summary>
    public IReadOnlyList<Tile> CoveredTiles => coveredTiles;
}
