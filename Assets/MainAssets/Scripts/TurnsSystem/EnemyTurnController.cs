using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy-side TurnController with scoring-based AI.
///
/// On its turn:
///   1. Scores every possible action for every unacted enemy monster via MonsterAIBrain.
///   2. Sorts monsters by their best available action score — highest opportunity acts first.
///   3. Re-evaluates each monster just before execution (AP may have changed since the sort).
///   4. Executes: AttackAction, MoveAction, MoveAndAttackAction, or PassAction.
///   5. Ends the turn when all monsters have acted or AP is depleted.
///
/// Scoring formula (per action):
///   finalScore = AIGameStateScorePoints (global baseline)
///              + MonsterPersonality     (per-monster bonus, 0 if not assigned)
///              + Random(0, randomJitter) (unpredictability)
///
/// Setup in the Inspector:
///   • Assign an AIGameStateScorePoints asset to "Game State Score Points"
///     (create via Assets → Create → Evermore → AI → AI Game State Score Points).
///   • Assign a MonsterPersonality to each MonsterData
///     (create via Assets → Create → Evermore → AI → Monster Personality).
/// </summary>
public class EnemyTurnController : TurnController
{
    // -- References ------------------------------------------------------------

    [Header("References")]
    [SerializeField] private GridManager gridManager;

    [Header("AI Configuration")]
    [Tooltip("Global score thresholds and base values shared by all enemy monsters.\n" +
             "Leave empty to use built-in defaults.\n" +
             "Create via: Assets → Create → Evermore → AI → AI Game State Score Points")]
    [SerializeField] private AIGameStateScorePoints gameStateScorePoints;

    // -- Testing / Debug -------------------------------------------------------

    [Header("Testing / Development")]
    [Tooltip("When enabled the enemy skips its entire turn immediately. Disable when the AI is ready.")]
    [SerializeField] private bool skipTurnForTesting = false;

    [Tooltip("When enabled, shows the AI turn queue debug panel for the entire session.\n" +
             "The panel refreshes its content at the start of each enemy turn.")]
    [SerializeField] private bool showTurnDebugPanel = false;

    [Tooltip("Height of the debug panel in UI pixels (reference resolution 1920×1080).\n" +
             "Adjust until all queue entries are comfortably visible.")]
    [SerializeField] private float debugPanelHeight = 800f;

    // -- Timing ----------------------------------------------------------------

    [Header("AI Timing")]
    [Tooltip("Pause between each monster's action so the player can follow what is happening.")]
    [SerializeField] private float actionDelay = 0.9f;

    [Tooltip("Speed at which enemy monsters slide to their new tile (world units per second).")]
    [SerializeField] private float moveSpeed = 5f;

    // -- Camera ----------------------------------------------------------------

    private CameraController _cameraController;

    // -- Brain -----------------------------------------------------------------

    private MonsterAIBrain _brain;

    // -- First-Actor Fatigue State ---------------------------------------------

    private Monster _lastFirstActor      = null;
    private int     _consecutiveFirstCount = 0;

    // -- Turn Lifecycle --------------------------------------------------------

    private void Awake()
    {
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        _cameraController = FindFirstObjectByType<CameraController>();
        _brain = new MonsterAIBrain(gameStateScorePoints);
        AITurnQueueDebug.SetEnabled(showTurnDebugPanel, debugPanelHeight);
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
        List<Monster> allyMonsters  = CollectAllyMonsters();

        // Late-bind CurrentTile for any monster that lost it between turns.
        foreach (var m in monsters)
        {
            if (m != null && m.CurrentTile == null)
            {
                Tile found = gridManager.FindTileOccupiedBy(m.gameObject);
                if (found != null)
                {
                    m.CurrentTile = found;
                    Debug.Log($"[EnemyAI] {m.name} CurrentTile late-bound to {found.GridPosition}.");
                }
            }
        }

        // ── Score everything once at turn start ───────────────────────────────
        // Collect every possible action from every living monster into one flat
        // list, then sort it highest-to-lowest. This order is fixed for the
        // entire turn — no re-scoring happens after this point.
        var turnQueue = new List<(Monster monster, AIAction action)>();

        foreach (var monster in monsters)
        {
            if (monster == null || !monster.IsAlive) continue;
            if (monster.CurrentTile == null) continue;
            if (monster.IsFrozen)
            {
                string name = monster.Data?.displayName ?? monster.gameObject.name;
                BattleMessage.Show($"{name} is frozen and cannot act!", 2f);
                Debug.Log($"[EnemyAI] {monster.name} is frozen — excluded from turn queue.");
                continue;
            }

            var ctx     = AIContext.Build(monster, CurrentAP, gridManager, playerTargets, allyMonsters);
            var actions = _brain.GetAllScoredActions(ctx);

            foreach (var action in actions)
                turnQueue.Add((monster, action));
        }

        float repPenalty = gameStateScorePoints != null
            ? gameStateScorePoints.repetitionPenalty
            : 30f;

        // ── Step 1: initial sort by raw score ─────────────────────────────────
        turnQueue.Sort((a, b) => b.action.Score.CompareTo(a.action.Score));

        // ── Step 2: consecutive repetition penalty ────────────────────────────
        // Walk the sorted list. Each time the same monster appears again
        // immediately after itself, the streak counter grows and the penalty
        // increases. A different monster in between resets the streak to 0.
        //
        //   streak | penalty applied to that action
        //     0    | 0          (first or fresh appearance)
        //     1    | 1 × repPenalty   (2nd in a row)
        //     2    | 2 × repPenalty   (3rd in a row)
        //     ...
        if (repPenalty > 0f)
        {
            Monster lastMonster   = null;
            int     streak        = 0;

            foreach (var (monster, action) in turnQueue)
            {
                if (monster == lastMonster)
                {
                    streak++;
                    action.Score -= repPenalty * streak;
                }
                else
                {
                    streak      = 0;
                    lastMonster = monster;
                }
            }
        }

        // ── Step 3: first-actor fatigue penalty ───────────────────────────────
        // If the same monster has acted first for >= firstActorFatigueLimit
        // consecutive turns, subtract an increasing penalty from all its actions
        // so a different monster is more likely to reach the top slot.
        int   fatigueLimit   = gameStateScorePoints != null ? gameStateScorePoints.firstActorFatigueLimit   : 2;
        float fatiguePenalty = gameStateScorePoints != null ? gameStateScorePoints.firstActorFatiguePenalty : 80f;

        if (fatigueLimit > 0
            && _lastFirstActor != null
            && _consecutiveFirstCount >= fatigueLimit)
        {
            int   extraTurns    = _consecutiveFirstCount - fatigueLimit + 1;
            float totalPenalty  = fatiguePenalty * extraTurns;
            foreach (var (monster, action) in turnQueue)
                if (monster == _lastFirstActor)
                    action.Score -= totalPenalty;

            Debug.Log($"[EnemyAI] First-actor fatigue: {_lastFirstActor.name} " +
                      $"penalised -{totalPenalty} pts ({_consecutiveFirstCount} consecutive turns first).");
        }

        // ── Step 4: final sort ────────────────────────────────────────────────
        turnQueue.Sort((a, b) => b.action.Score.CompareTo(a.action.Score));

        // ── Step 5: guaranteed exact slot (per MonsterPersonality) ───────────
        // Applied AFTER scoring sort. Forces a monster's best action to a
        // specific 1-based position, shifting others around it.
        ApplyGuaranteedExactSlots(turnQueue);

        // ── Step 6: guaranteed window appearance (per MonsterPersonality) ─────
        // Applied last. Ensures a monster appears at least once in every
        // consecutive block of X slots.
        ApplyGuaranteedWindowAppearances(turnQueue);

        // ── Step 7: trim to actions the AI can actually afford ────────────────
        // Simulates AP spending in queue order (tracking each monster's tile as
        // moves commit) and removes every entry whose cost exceeds the AP that
        // will remain when its slot is reached.  The result is exactly the set
        // of actions that will execute this turn — nothing more.
        TrimQueueByAP(turnQueue, CurrentAP);

        // ── Record first actor for fatigue tracking ───────────────────────────
        if (turnQueue.Count > 0)
        {
            Monster first = turnQueue[0].monster;
            if (first == _lastFirstActor)
                _consecutiveFirstCount++;
            else
            {
                _lastFirstActor       = first;
                _consecutiveFirstCount = 1;
            }
        }

        Debug.Log($"[EnemyAI] Turn queue ready — {turnQueue.Count} affordable actions across all monsters.");

        // Show the full queue in the debug overlay.
        AITurnQueueDebug.ShowQueue(turnQueue);

        // ── Execute in sorted order ───────────────────────────────────────────
        for (int i = 0; i < turnQueue.Count; i++)
        {
            if (CurrentAP <= 0)
            {
                Debug.Log("[EnemyAI] AP depleted — stopping execution.");
                break;
            }

            var (monster, action) = turnQueue[i];
            if (monster == null || !monster.IsAlive) continue;

            AITurnQueueDebug.SetActiveIndex(i);

            // Focus the camera on this monster BEFORE the delay so it has time to pan there.
            _cameraController?.FocusOnMonster(monster.transform.position);

            yield return new WaitForSeconds(actionDelay);

            // Re-check: monster may have died during the delay (e.g. from a delayed animation event).
            if (monster == null || !monster.IsAlive) continue;

            yield return StartCoroutine(ExecuteAction(monster, action));
        }

        // Mark all monsters as acted so the turn system is satisfied.
        foreach (var m in monsters)
            if (m != null) m.MarkActed();

        AITurnQueueDebug.ClearActiveIndex();
        _cameraController?.ReleaseFocus();
        Debug.Log("[EnemyAI] All actions complete — waiting for animations.");
        yield return WaitForPendingAnimations();
        Debug.Log("[EnemyAI] Animations done — ending turn.");
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
                // Only attack if the target survived the move.
                if (moveAtk.Target != null && moveAtk.Target.IsAlive)
                    yield return StartCoroutine(PerformAttack(monster, moveAtk.Target, moveAtk.AttackIndex));
                break;

            default: // PassAction
                Debug.Log($"[EnemyAI] {monster.name} passes — no beneficial action found.");
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

        // Face the target before striking.
        Vector3 dir = target.transform.position - monster.transform.position;
        dir.y = 0f;
        if (dir != Vector3.zero)
            monster.transform.root.rotation = Quaternion.LookRotation(dir);

        monster.ExecuteAttack(new List<Monster> { target }, attackIndex, attackData.IsDirect);

        // Briefly pan to the target so the player can see the damage result.
        if (target.IsAlive)
            _cameraController?.UpdateFocusTarget(target.transform.position);

        Debug.Log($"[EnemyAI] {monster.name} used '{attackData.DisplayName}' " +
                  $"on {target.name}! (cost {apCost} AP)");
    }

    private IEnumerator PerformMove(Monster monster, Tile destination)
    {
        if (destination == null || monster.CurrentTile == null) yield break;

        // Safety check: destination might have been taken by another monster acting earlier.
        if (!destination.IsWalkable())
        {
            Debug.Log($"[EnemyAI] {monster.name} destination {destination.GridPosition} " +
                      "is no longer walkable — skipping move.");
            yield break;
        }

        // Find the actual BFS path so the monster walks around walls correctly.
        List<Tile> path = gridManager.FindPath(monster.CurrentTile, destination, monster.IsFlying);
        if (path == null || path.Count == 0)
        {
            Debug.Log($"[EnemyAI] {monster.name} no path to {destination.GridPosition} — skipping.");
            yield break;
        }

        // AP cost mirrors the player movement formula: ceiling(tiles / tilesPerAP) + shock.
        int moveCost = Mathf.CeilToInt((float)path.Count / monster.TilesPerAP)
                     + monster.ShockAPCostIncrease;
        moveCost = Mathf.Max(1, moveCost);

        if (!SpendAP(moveCost)) yield break;

        yield return StartCoroutine(SlideAlongPath(monster, monster.CurrentTile, path));
    }

    // -- Movement Coroutine ────────────────────────────────────────────────────

    /// <summary>
    /// Slides a monster along a multi-tile BFS path, one tile at a time.
    /// Faces the correct direction at each step. Starts/ends the walk animation.
    /// </summary>
    private IEnumerator SlideAlongPath(Monster monster, Tile fromTile, List<Tile> path)
    {
        if (path == null || path.Count == 0) yield break;

        monster.TriggerMovementAnimationStart();

        Tile previousTile = fromTile;
        foreach (Tile nextTile in path)
        {
            if (monster == null) yield break;

            // Guard against flying monsters pathing through an occupied tile.
            // FindPath (isFlying=true) allows intermediate tiles to be non-walkable,
            // but SlideAlongPath must not physically enter an occupied tile.
            if (!nextTile.IsWalkable())
            {
                Debug.LogWarning($"[EnemyAI] {monster.name} path blocked at " +
                                 $"{nextTile.GridPosition} (occupied by " +
                                 $"{nextTile.OccupyingObject?.name ?? "unknown"}) — stopping mid-path.");
                break;
            }

            previousTile.ClearOccupation();
            nextTile.SetOccupation(Tile.OccupationType.Monster, monster.gameObject);

            Vector3 start = monster.transform.position;
            Vector3 end   = nextTile.transform.position;

            Vector3 moveDir = new Vector3(end.x - start.x, 0f, end.z - start.z);
            if (moveDir != Vector3.zero)
                monster.transform.root.rotation = Quaternion.LookRotation(moveDir);

            float dist = Vector3.Distance(start, end);
            float t    = 0f;
            while (t < 1f)
            {
                if (monster == null) yield break;
                t += Time.deltaTime * moveSpeed / Mathf.Max(dist, 0.01f);
                monster.transform.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t));
                _cameraController?.UpdateFocusTarget(monster.transform.position);
                yield return null;
            }

            if (monster == null) yield break;
            monster.transform.position = end;
            monster.CurrentTile        = nextTile;
            previousTile               = nextTile;
        }

        if (monster == null) yield break;
        monster.TriggerMovementAnimationEnd();
        Debug.Log($"[EnemyAI] {monster.name} moved to {path[path.Count - 1].GridPosition}.");
    }

    // -- Queue Post-Processing ─────────────────────────────────────────────────

    /// <summary>
    /// Feature 3 — Guaranteed exact slot.
    /// For every monster whose personality has guaranteedExactSlotEnabled,
    /// finds its best action in the queue and moves it to the requested 1-based slot.
    /// Processed in queue order so lower-slot constraints win ties.
    /// </summary>
    private void ApplyGuaranteedExactSlots(List<(Monster monster, AIAction action)> queue)
    {
        // Collect all monsters that want an exact slot, sorted by requested slot ascending
        // so earlier slots are placed first and don't displace later ones.
        var requests = new List<(Monster monster, int slot)>();
        foreach (var m in monsters)
        {
            if (m == null || m.Data?.personality == null) continue;
            var p = m.Data.personality;
            if (p.guaranteedExactSlotEnabled)
                requests.Add((m, p.guaranteedExactSlot));
        }
        requests.Sort((a, b) => a.slot.CompareTo(b.slot));

        foreach (var (monster, slot) in requests)
        {
            int targetIdx = Mathf.Clamp(slot - 1, 0, queue.Count - 1);

            // Find the monster's first (best-scored) action currently in the queue.
            int sourceIdx = -1;
            for (int i = 0; i < queue.Count; i++)
                if (queue[i].monster == monster) { sourceIdx = i; break; }

            if (sourceIdx < 0 || sourceIdx == targetIdx) continue;

            var entry = queue[sourceIdx];
            queue.RemoveAt(sourceIdx);
            queue.Insert(targetIdx, entry);
        }
    }

    /// <summary>
    /// Feature 2 — Guaranteed window appearance.
    /// For every monster whose personality has guaranteedAppearanceEnabled,
    /// scans the queue in windows of guaranteedEveryXActions. If the monster
    /// is absent from a window it has actions remaining for, moves its next
    /// action into the last slot of that window.
    /// </summary>
    private void ApplyGuaranteedWindowAppearances(List<(Monster monster, AIAction action)> queue)
    {
        foreach (var m in monsters)
        {
            if (m == null || m.Data?.personality == null) continue;
            var p = m.Data.personality;
            if (!p.guaranteedAppearanceEnabled || p.guaranteedEveryXActions < 1) continue;

            int windowSize = p.guaranteedEveryXActions;
            int windowStart = 0;

            while (windowStart < queue.Count)
            {
                int windowEnd = Mathf.Min(windowStart + windowSize, queue.Count);

                // Check if monster appears in [windowStart, windowEnd).
                bool found = false;
                for (int i = windowStart; i < windowEnd; i++)
                    if (queue[i].monster == m) { found = true; break; }

                if (!found)
                {
                    // Find the monster's next action anywhere after this window.
                    int sourceIdx = -1;
                    for (int i = windowEnd; i < queue.Count; i++)
                        if (queue[i].monster == m) { sourceIdx = i; break; }

                    if (sourceIdx >= 0)
                    {
                        // Pull it into the last slot of this window.
                        var entry = queue[sourceIdx];
                        queue.RemoveAt(sourceIdx);
                        queue.Insert(windowEnd - 1, entry);
                    }
                    // No more actions for this monster — nothing to guarantee.
                }

                windowStart += windowSize;
            }
        }
    }

    // -- Turn Queue AP Trimming ────────────────────────────────────────────────

    /// <summary>
    /// Removes queue entries that cannot be executed with the AP that will remain
    /// when their slot is reached. Walks the list in order, subtracting each kept
    /// entry's cost from a running AP total; entries whose cost exceeds the
    /// remaining total are removed outright.
    ///
    /// Monster tile positions are tracked across moves so that the cost of a
    /// later move by the same monster is computed from its post-move location.
    /// </summary>
    private void TrimQueueByAP(List<(Monster monster, AIAction action)> queue, int availableAP)
    {
        var simulatedTile = new Dictionary<Monster, Tile>();
        foreach (var m in monsters)
            if (m != null && m.CurrentTile != null)
                simulatedTile[m] = m.CurrentTile;

        int simAP = availableAP;
        int i     = 0;
        while (i < queue.Count)
        {
            var (monster, action) = queue[i];

            if (monster == null || !monster.IsAlive)
            {
                queue.RemoveAt(i);
                continue;
            }

            simulatedTile.TryGetValue(monster, out Tile fromTile);
            int cost = ComputeActionAPCost(monster, action, fromTile ?? monster.CurrentTile);

            if (cost > simAP)
            {
                queue.RemoveAt(i);
                continue;
            }

            simAP -= cost;

            if (action is MoveAction mv && mv.Destination != null)
                simulatedTile[monster] = mv.Destination;
            else if (action is MoveAndAttackAction mva && mva.MoveTo != null)
                simulatedTile[monster] = mva.MoveTo;

            i++;
        }
    }

    /// <summary>
    /// Returns the AP cost of <paramref name="action"/> without executing it.
    /// <paramref name="fromTile"/> is the monster's simulated current tile, which
    /// may differ from <c>monster.CurrentTile</c> when earlier moves have already
    /// been committed during the same trim pass.
    /// </summary>
    private int ComputeActionAPCost(Monster monster, AIAction action, Tile fromTile)
    {
        int shock      = monster.ShockAPCostIncrease;
        int tilesPerAP = Mathf.Max(1, monster.TilesPerAP);

        switch (action)
        {
            case AttackAction atk:
            {
                var attacks = monster.GetAttacks();
                if (atk.AttackIndex < 0 || atk.AttackIndex >= attacks.Count) return int.MaxValue;
                var data = attacks[atk.AttackIndex]?.data;
                if (data == null) return int.MaxValue;
                return data.ConsumeActionPoints + shock;
            }

            case MoveAction mv:
            {
                if (fromTile == null || mv.Destination == null) return int.MaxValue;
                int dist = gridManager.GetDistanceBetweenTiles(fromTile, mv.Destination);
                return Mathf.Max(1, Mathf.CeilToInt((float)dist / tilesPerAP) + shock);
            }

            case MoveAndAttackAction mva:
            {
                if (fromTile == null || mva.MoveTo == null) return int.MaxValue;
                int dist     = gridManager.GetDistanceBetweenTiles(fromTile, mva.MoveTo);
                int moveCost = Mathf.Max(1, Mathf.CeilToInt((float)dist / tilesPerAP) + shock);
                var attacks  = monster.GetAttacks();
                if (mva.AttackIndex < 0 || mva.AttackIndex >= attacks.Count) return int.MaxValue;
                var data     = attacks[mva.AttackIndex]?.data;
                if (data == null) return int.MaxValue;
                return moveCost + data.ConsumeActionPoints + shock;
            }

            case PassAction _:
                return 0;

            default:
                return int.MaxValue;
        }
    }

    // -- Helpers ───────────────────────────────────────────────────────────────

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

    /// <summary>All living enemy-side monsters (allies for heal scoring, excludes Self per context).</summary>
    private List<Monster> CollectAllyMonsters()
    {
        var allies = new List<Monster>();
        foreach (var m in monsters)
            if (m != null && m.IsAlive)
                allies.Add(m);
        return allies;
    }
}
