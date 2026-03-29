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

    // -- Testing ---------------------------------------------------------------

    [Header("Testing / Development")]
    [Tooltip("When enabled the enemy skips its entire turn immediately " +
             "and returns control to the player. Disable when the AI is ready.")]
    [SerializeField] private bool skipTurnForTesting = false;

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
        if (skipTurnForTesting)
        {
            Debug.Log("[EnemyAI] Skip-turn is ON — ending enemy turn immediately.");
            ForceEndTurn();
            return;
        }

        StartCoroutine(RunAITurn());
    }

    // -- Auto-Discovery --------------------------------------------------------

    /// <summary>
    /// Always re-discovers enemy monsters from the scene, replacing any
    /// Inspector-assigned list. This ensures the AI uses the spawner's runtime
    /// instances (which have CurrentTile set) rather than pre-placed scene objects.
    /// </summary>
    public override void AutoDiscoverMonsters()
    {
        monsters.Clear();
        DiscoverMonsters();
    }

    /// <summary>Scans the scene for all enemy monsters and adds them to this roster.</summary>
    protected override void DiscoverMonsters()
    {
        var all = FindObjectsByType<Monster>(FindObjectsSortMode.None);
        foreach (var m in all)
            if (m.IsEnemy)
                monsters.Add(m);
        Debug.Log($"[EnemyTurnController] Auto-discovered {monsters.Count} enemy monster(s).");
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

        Debug.Log("[EnemyAI] All actions complete — ending turn.");
        ForceEndTurn();
    }

    // -- Decision Logic --------------------------------------------------------

    /// <summary>
    /// Override to replace with BehaviourTree, GOAP, or any other AI.
    /// Default: attack adjacent player monster → move toward nearest → pass.
    /// </summary>
    protected virtual IEnumerator DecideAndAct(Monster monster)
    {
        Tile currentTile = monster.CurrentTile
                        ?? gridManager.GetTileAtWorldPosition(monster.transform.root.position);
        if (currentTile != null && monster.CurrentTile == null)
        {
            monster.CurrentTile = currentTile; // late-bind so future lookups are fast
            Debug.Log($"[EnemyAI] {monster.name} CurrentTile late-bound to {currentTile.GridPosition}.");
        }
        if (currentTile == null)
        {
            Debug.LogWarning($"[EnemyAI] {monster.name} not on a valid tile " +
                             $"(root pos={monster.transform.root.position}).");
            monster.MarkActed();
            yield break;
        }

        // -- Try to attack an adjacent player monster ---------------------------
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

                    // Face the attack target before executing
                    Vector3 attackDir = new Vector3(
                        attackTarget.transform.position.x - monster.transform.position.x, 0f,
                        attackTarget.transform.position.z - monster.transform.position.z);
                    if (attackDir != Vector3.zero)
                        monster.transform.root.rotation = Quaternion.LookRotation(attackDir);

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
        Debug.Log($"[EnemyAI] {monster.name} has no valid action — passing.");
        monster.MarkActed();
    }

    // -- Movement Coroutine ----------------------------------------------------

    private IEnumerator SlideTo(Monster monster, Tile fromTile, Tile toTile)
    {
        fromTile.ClearOccupation();
        toTile.SetOccupation(Tile.OccupationType.Monster, monster.gameObject);

        Vector3 start = monster.transform.position;
        Vector3 end   = toTile.transform.position;

        // Face the movement direction before sliding
        Vector3 moveDir = new Vector3(end.x - start.x, 0f, end.z - start.z);
        if (moveDir != Vector3.zero)
            monster.transform.root.rotation = Quaternion.LookRotation(moveDir);

        float dist    = Vector3.Distance(start, end);
        float t       = 0f;

        monster.TriggerMovementAnimationStart();
        while (t < 1f)
        {
            t += Time.deltaTime * moveSpeed / Mathf.Max(dist, 0.01f);
            monster.transform.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        monster.transform.position = end;
        monster.CurrentTile = toTile;
        monster.TriggerMovementAnimationEnd();

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
        Tile targetTile = FindNearestPlayerTile(origin);
        if (targetTile == null) return null;

        Tile best     = null;
        int  bestDist = int.MaxValue;

        foreach (Tile neighbour in gridManager.GetNeighbors(origin, includeDiagonals: false))
        {
            if (!neighbour.IsWalkable()) continue;

            int d = gridManager.GetDistanceBetweenTiles(neighbour, targetTile);
            if (d < bestDist) { bestDist = d; best = neighbour; }
        }

        return best;
    }

    private Tile FindNearestPlayerTile(Tile origin)
    {
        Tile nearest  = null;
        int  bestDist = int.MaxValue;

        PlayerTurnController playerCtrl =
            TurnManager.Instance?.ActiveController as PlayerTurnController
            ?? FindFirstObjectByType<PlayerTurnController>();

        if (playerCtrl == null) return null;

        foreach (Monster m in playerCtrl.Monsters)
        {
            if (m == null) continue;
            Tile t = m.CurrentTile ?? gridManager.GetTileAtWorldPosition(m.transform.root.position);
            if (t == null) continue;

            int d = gridManager.GetDistanceBetweenTiles(origin, t);
            if (d < bestDist) { bestDist = d; nearest = t; }
        }

        return nearest;
    }

    // -- Attack Selection Helpers ----------------------------------------------

    private AttackData PickBestAffordableAttack(
        System.Collections.Generic.IReadOnlyList<MonsterAttack> attacks)
    {
        AttackData best     = null;
        int        bestCost = -1;

        foreach (MonsterAttack ma in attacks)
        {
            if (ma?.data == null) continue;
            int cost = ma.data.ConsumeActionPoints;
            if (CanAfford(cost) && cost > bestCost) { bestCost = cost; best = ma.data; }
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
