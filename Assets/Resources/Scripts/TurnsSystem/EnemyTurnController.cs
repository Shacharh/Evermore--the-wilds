using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy-side TurnController.
/// On its turn, loops through all unacted enemy monsters in order,
/// decides an action for each (attack if adjacent, otherwise move toward
/// nearest player monster), spends shared AP, and ends the turn when done.
///
/// Override DecideAndAct() to plug in your own AI logic.
/// </summary>
public class EnemyTurnController : TurnController
{
    // -- References ------------------------------------------------------------

    [Header("References")]
    [SerializeField] private GridManager gridManager;

    // -- Timing ----------------------------------------------------------------

    [Header("AI Timing")]
    [Tooltip("Pause between each monster's action so the player can follow.")]
    [SerializeField] private float actionDelay = 0.9f;

    [Tooltip("Speed at which enemy monsters slide to their new tile.")]
    [SerializeField] private float moveSpeed = 5f;

    // -- Turn Lifecycle --------------------------------------------------------

    private void Awake()
    {
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();
    }

    protected override void OnTurnStarted()
    {
        StartCoroutine(RunAITurn());
    }

    // -- AI Loop ---------------------------------------------------------------

    private IEnumerator RunAITurn()
    {
        Debug.Log("[EnemyAI] Starting AI turn...");

        List<Monster> queue = GetUnactedMonsters();

        foreach (Monster monster in queue)
        {
            if (CurrentAP <= 0) break;

            yield return new WaitForSeconds(actionDelay);
            yield return StartCoroutine(DecideAndAct(monster));
        }

        Debug.Log("[EnemyAI] All actions complete -- ending turn.");
        ForceEndTurn();
    }

    // -- Decision Logic --------------------------------------------------------

    /// <summary>
    /// Override to replace with BehaviourTree, GOAP, or any other AI.
    /// Default: attack adjacent player monster -> move toward nearest -> pass.
    /// </summary>
    protected virtual IEnumerator DecideAndAct(Monster monster)
    {
        Tile currentTile = gridManager.GetTileAtWorldPosition(monster.transform.position);
        if (currentTile == null)
        {
            Debug.LogWarning($"[EnemyAI] {monster.name} not on a valid tile.");
            monster.MarkActed();
            yield break;
        }


        // -- Try to attack an adjacent player monster ---------------------------
        // AP cost comes from the attack's ConsumeActionPoints, not a fixed field.
        var attacks = monster.GetAttacks();
        if (attacks != null && attacks.Count > 0)
        {
            AttackData bestAttack = PickBestAffordableAttack(attacks);
            if (bestAttack != null)
            {
                Tile attackTarget = FindAdjacentPlayerTile(currentTile);
                if (attackTarget != null)
                {
                    SpendAP(bestAttack.ConsumeActionPoints);
                    monster.MarkActed();
                    Monster target = attackTarget.GetMonster();
                    int attackIndex = GetAttackIndex(attacks, bestAttack);
                    monster.ExecuteAttack(target, attackIndex, true);
                    Debug.Log($"[EnemyAI] {monster.name} used '{bestAttack.DisplayName}' " +
                              $"on {target.name}! (cost {bestAttack.ConsumeActionPoints} AP)");
                    yield break;
                }
            }
        }

        // -- Try to move toward nearest player monster -------------------------
        if (CanAfford(monster.MoveCost))
        {
            Tile stepTile = FindStepTowardNearestPlayer(currentTile);
            if (stepTile != null)
            {
                SpendAP(monster.MoveCost);
                monster.MarkActed();
                yield return StartCoroutine(SlideTo(monster, currentTile, stepTile));
                yield break;
            }
        }

        // -- Nothing affordable ------------------------------------------------
        Debug.Log($"[EnemyAI] {monster.name} has no valid action -- passing.");
        monster.MarkActed();
    }


    // -- Movement Coroutine ----------------------------------------------------

    private IEnumerator SlideTo(Monster monster, Tile fromTile, Tile toTile)
    {
        fromTile.ClearOccupation();
        toTile.SetOccupation(Tile.OccupationType.Monster, monster.gameObject);

        Vector3 start = monster.transform.position;
        Vector3 end = toTile.transform.position;
        float dist = Vector3.Distance(start, end);
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * moveSpeed / Mathf.Max(dist, 0.01f);
            monster.transform.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        monster.transform.position = end;
        Debug.Log($"[EnemyAI] {monster.name} moved to {toTile.GridPosition}.");
    }

    // -- Targeting Helpers -----------------------------------------------------

    /// <summary>Return the first cardinal-adjacent tile that has a player monster.</summary>
    private Tile FindAdjacentPlayerTile(Tile origin)
    {
        foreach (Tile neighbour in gridManager.GetNeighbors(origin, includeDiagonals: false))
        {
            Monster m = neighbour.GetMonster();
            if (m != null && !m.IsEnemy)
                return neighbour;
        }
        return null;
    }

    /// <summary>
    /// Return the walkable adjacent tile that minimises Manhattan distance
    /// to the nearest player monster.
    /// </summary>
    private Tile FindStepTowardNearestPlayer(Tile origin)
    {
        // Find the closest player monster's tile
        Tile targetTile = FindNearestPlayerTile(origin);
        if (targetTile == null) return null;

        // Pick the walkable neighbour that is closest to that target
        Tile best = null;
        int bestDist = int.MaxValue;

        foreach (Tile neighbour in gridManager.GetNeighbors(origin, includeDiagonals: false))
        {
            if (!neighbour.IsWalkable()) continue;

            int d = gridManager.GetDistanceBetweenTiles(neighbour, targetTile);
            if (d < bestDist)
            {
                bestDist = d;
                best = neighbour;
            }
        }

        return best;
    }

    private Tile FindNearestPlayerTile(Tile origin)
    {
        Tile nearest = null;
        int bestDist = int.MaxValue;

        // Player monsters are tracked in PlayerTurnController
        PlayerTurnController playerCtrl =
            TurnManager.Instance?.ActiveController as PlayerTurnController
            ?? FindFirstObjectByType<PlayerTurnController>();

        if (playerCtrl == null) return null;

        foreach (Monster m in playerCtrl.Monsters)
        {
            if (m == null) continue;
            Tile t = gridManager.GetTileAtWorldPosition(m.transform.position);
            if (t == null) continue;

            int d = gridManager.GetDistanceBetweenTiles(origin, t);
            if (d < bestDist) { bestDist = d; nearest = t; }
        }

        return nearest;
    }

    // -- Attack Selection Helpers ----------------------------------------------

    /// <summary>
    /// Returns the highest-priority affordable attack.
    /// Priority: highest ConsumeActionPoints the pool can still afford
    /// (i.e. prefer spending more AP per attack when possible).
    /// Override this in a subclass for smarter selection.
    /// </summary>
    private AttackData PickBestAffordableAttack(
        System.Collections.Generic.IReadOnlyList<MonsterAttack> attacks)
    {
        AttackData best = null;
        int bestCost = -1;

        foreach (MonsterAttack ma in attacks)
        {
            if (ma?.data == null) continue;
            int cost = ma.data.ConsumeActionPoints;
            if (CanAfford(cost) && cost > bestCost)
            {
                bestCost = cost;
                best = ma.data;
            }
        }

        return best;
    }

    private int GetAttackIndex(
        System.Collections.Generic.IReadOnlyList<MonsterAttack> attacks,
        AttackData target)
    {
        for (int i = 0; i < attacks.Count; i++)
            if (attacks[i]?.data == target) return i;
        return 0;
    }
}