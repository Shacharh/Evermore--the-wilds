using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy-side TurnController.
///
/// On its turn:
///   1. Scores every possible action for every unacted enemy monster.
///   2. Sorts monsters by their best available action score (globally highest acts first).
///   3. Re-evaluates each monster just before execution (AP may have changed).
///   4. Executes: AttackAction, MoveAction, MoveAndAttackAction, or PassAction.
///   5. Ends the turn when all monsters have acted or AP is depleted.
///
/// Personality is driven by MonsterPersonality ScriptableObjects assigned on MonsterData.
/// </summary>
public class EnemyTurnController : TurnController
{
    // -- References ------------------------------------------------------------

    [Header("References")]
    [SerializeField] private GridManager gridManager;

    [Header("AI Configuration")]
    [Tooltip("Global score thresholds and estimation constants shared by all enemy monsters.\n" +
             "Leave empty to use built-in defaults.\n" +
             "Create via: Assets → Create → Evermore → AI → AI Game State Score Points")]
    [SerializeField] private AIGameStateScorePoints gameStateScorePoints;

    // -- Testing ---------------------------------------------------------------

    [Header("Testing / Development")]
    [Tooltip("When enabled the enemy skips its entire turn immediately. Disable when AI is ready.")]
    [SerializeField] private bool skipTurnForTesting = false;

    // -- Timing ----------------------------------------------------------------

    [Header("AI Timing")]
    [Tooltip("Pause between each monster's action so the player can follow.")]
    [SerializeField] private float actionDelay = 0.9f;

    [Tooltip("Speed at which enemy monsters slide to their new tile (world units per second).")]
    [SerializeField] private float moveSpeed = 5f;

    // -- Brain -----------------------------------------------------------------

    private MonsterAIBrain _brain;

    // -- Turn Lifecycle --------------------------------------------------------

    private void Awake()
    {
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        _brain = new MonsterAIBrain(gameStateScorePoints);
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

    public override void AutoDiscoverMonsters()
    {
        monsters.Clear();
        DiscoverMonsters();
    }

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

        List<Monster> playerTargets = CollectPlayerTargets();

        // Late-bind CurrentTile for any monster that lost it between turns
        foreach (var m in monsters)
        {
            if (m != null && m.CurrentTile == null)
            {
                Tile found = gridManager.GetTileAtWorldPosition(m.transform.root.position);
                if (found != null)
                {
                    m.CurrentTile = found;
                    Debug.Log($"[EnemyAI] {m.name} CurrentTile late-bound to {found.GridPosition}.");
                }
            }
        }

        List<Monster> unacted = GetUnactedMonsters();

        // ── Score and sort monsters by best available action ──────────────────
        // Frozen monsters get PassAction (score 0) and naturally sort to the end.
        var sorted = new List<(Monster monster, float bestScore)>();

        foreach (var monster in unacted)
        {
            if (monster.CurrentTile == null)
            {
                Debug.LogWarning($"[EnemyAI] {monster.name} has no valid tile — skipping.");
                monster.MarkActed();
                continue;
            }

            if (monster.IsFrozen)
            {
                sorted.Add((monster, 0f));
                continue;
            }

            var ctx    = AIContext.Build(monster, CurrentAP, gridManager, playerTargets);
            var action = _brain.PickBestAction(ctx);
            sorted.Add((monster, action.Score));
        }

        // Descending by score — highest-opportunity monster acts first
        sorted.Sort((a, b) => b.bestScore.CompareTo(a.bestScore));

        // ── Execute in sorted order ───────────────────────────────────────────
        foreach (var (monster, _) in sorted)
        {
            yield return new WaitForSeconds(actionDelay);

            // Handle frozen monsters
            if (monster.IsFrozen)
            {
                string frozenName = monster.Data?.displayName ?? monster.gameObject.name;
                BattleMessage.Show($"{frozenName} is frozen and cannot act!", 2f);
                Debug.Log($"[EnemyAI] {monster.name} is frozen — skipping.");
                monster.MarkActed();
                continue;
            }

            if (CurrentAP <= 0)
            {
                Debug.Log($"[EnemyAI] Out of AP — {monster.name} cannot act.");
                monster.MarkActed();
                continue;
            }

            // Re-score with current AP (may have changed since initial sort)
            var ctx    = AIContext.Build(monster, CurrentAP, gridManager, playerTargets);
            var action = _brain.PickBestAction(ctx);

            yield return StartCoroutine(ExecuteAction(monster, action));
            monster.MarkActed();
        }

        Debug.Log("[EnemyAI] All actions complete — ending turn.");
        ForceEndTurn();
    }

    // -- Action Execution ──────────────────────────────────────────────────────

    private IEnumerator ExecuteAction(Monster monster, AIAction action)
    {
        switch (action)
        {
            case AttackAction atk:
                yield return StartCoroutine(PerformAttack(monster, atk.Target, atk.AttackIndex));
                break;

            case MoveAction move:
                yield return StartCoroutine(PerformMove(monster, move.Destination));
                break;

            case MoveAndAttackAction moveAtk:
                yield return StartCoroutine(PerformMove(monster, moveAtk.MoveTo));
                // Only attack if the target survived the repositioning delay
                if (moveAtk.Target != null && moveAtk.Target.IsAlive)
                    yield return StartCoroutine(PerformAttack(monster, moveAtk.Target, moveAtk.AttackIndex));
                break;

            default: // PassAction
                Debug.Log($"[EnemyAI] {monster.name} passes — no beneficial action available.");
                break;
        }
    }

    private IEnumerator PerformAttack(Monster monster, Monster target, int attackIndex)
    {
        if (target == null || !target.IsAlive) yield break;

        var attacks = monster.GetAttacks();
        if (attackIndex < 0 || attackIndex >= attacks.Count) yield break;

        AttackData attackData = attacks[attackIndex].data;
        if (attackData == null) yield break;

        int apCost = attackData.ConsumeActionPoints + monster.ShockAPCostIncrease;
        if (!SpendAP(apCost)) yield break;

        // Face the target before striking
        Vector3 dir = target.transform.position - monster.transform.position;
        dir.y = 0f;
        if (dir != Vector3.zero)
            monster.transform.root.rotation = Quaternion.LookRotation(dir);

        monster.ExecuteAttack(target, attackIndex, attackData.IsDirect);

        Debug.Log($"[EnemyAI] {monster.name} used '{attackData.DisplayName}' " +
                  $"on {target.name}! (cost {apCost} AP)");
    }

    private IEnumerator PerformMove(Monster monster, Tile destination)
    {
        if (destination == null || monster.CurrentTile == null) yield break;

        // Safety check: destination might have been taken by another monster acting earlier
        if (!destination.IsWalkable())
        {
            Debug.Log($"[EnemyAI] {monster.name} move destination {destination.GridPosition} " +
                      "is no longer walkable — skipping move.");
            yield break;
        }

        int distance = gridManager.GetDistanceBetweenTiles(monster.CurrentTile, destination);
        int moveCost = distance * monster.MoveCost + monster.ShockAPCostIncrease;

        if (!SpendAP(moveCost)) yield break;

        yield return StartCoroutine(SlideTo(monster, monster.CurrentTile, destination));
    }

    // -- Movement Coroutine ────────────────────────────────────────────────────

    private IEnumerator SlideTo(Monster monster, Tile fromTile, Tile toTile)
    {
        fromTile.ClearOccupation();
        toTile.SetOccupation(Tile.OccupationType.Monster, monster.gameObject);

        Vector3 start   = monster.transform.position;
        Vector3 end     = toTile.transform.position;

        Vector3 moveDir = new Vector3(end.x - start.x, 0f, end.z - start.z);
        if (moveDir != Vector3.zero)
            monster.transform.root.rotation = Quaternion.LookRotation(moveDir);

        float dist = Vector3.Distance(start, end);
        float t    = 0f;

        monster.TriggerMovementAnimationStart();
        while (t < 1f)
        {
            t += Time.deltaTime * moveSpeed / Mathf.Max(dist, 0.01f);
            monster.transform.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        monster.transform.position = end;
        monster.CurrentTile        = toTile;
        monster.TriggerMovementAnimationEnd();

        Debug.Log($"[EnemyAI] {monster.name} moved to {toTile.GridPosition}.");
    }

    // -- Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Collects all living player-side monsters for use as AI targets.</summary>
    private List<Monster> CollectPlayerTargets()
    {
        PlayerTurnController playerCtrl =
            TurnManager.Instance?.ActiveController as PlayerTurnController
            ?? FindFirstObjectByType<PlayerTurnController>();

        var targets = new List<Monster>();
        if (playerCtrl == null) return targets;

        foreach (var m in playerCtrl.Monsters)
            if (m != null && m.IsAlive)
                targets.Add(m);

        return targets;
    }
}
