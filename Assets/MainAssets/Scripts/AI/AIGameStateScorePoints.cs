using UnityEngine;

/// <summary>
/// Global AI scoring baseline shared by every enemy monster.
///
/// These values define how the AI scores actions based on the current game state.
/// Every monster starts with these base scores. MonsterPersonality then adds bonus
/// points on top for monsters that have one assigned.
///
/// Final score formula:
///   finalScore = AIGameStateScorePoints (base) + MonsterPersonality (bonus) + Random(0, randomJitter)
///
/// Create via: Assets → Create → Evermore → AI → AI Game State Score Points
/// Assign to EnemyTurnController → AI Configuration → Game State Score Points.
/// </summary>
[CreateAssetMenu(menuName = "Evermore/AI/AI Game State Score Points")]
public class AIGameStateScorePoints : ScriptableObject
{
    // ── Thresholds & Estimation Constants ────────────────────────────────────
    // These are conditions and calculation settings. They live here only —
    // MonsterPersonality does not duplicate them.

    [Header("Thresholds")]
    [Tooltip("Target HP fraction (0–1) below which the focus-fire bonus activates.\n" +
             "Default 0.3 = bonus triggers when target is below 30% HP.")]
    [Range(0f, 1f)]
    public float focusFireHPThreshold = 0.30f;

    [Tooltip("Self HP fraction (0–1) below which the low-HP caution penalty activates.\n" +
             "Default 0.25 = penalty triggers when self is below 25% HP.")]
    [Range(0f, 1f)]
    public float lowHPCautionThreshold = 0.25f;

    [Tooltip("Self HP fraction (0–1) below which survival instinct can activate.\n" +
             "Survival instinct also requires a player monster to have a one-shot attack in range.\n" +
             "Default 0.3 = activates when self is below 30% HP.")]
    [Range(0f, 1f)]
    public float dangerHPThreshold = 0.30f;

    [Header("Estimation Constants")]
    [Tooltip("Score penalty subtracted from MoveAndAttackAction candidates.\n" +
             "Ensures attacking from the current tile is preferred when scores are otherwise equal.")]
    public float repositionPenalty = 3f;

    [Tooltip("Score added for a status effect type not explicitly handled " +
             "(anything other than Freeze / Burn / Poison / Shock).")]
    public float unknownStatusScore = 5f;

    [Tooltip("Damage multiplier used in estimation when a ranged attack path is obstructed.\n" +
             "Default 0.5 = obstructed attacks are estimated at half damage.")]
    [Range(0f, 1f)]
    public float obstructedDamageMultiplier = 0.50f;

    [Tooltip("Midpoint of the 0.85–1.0 random damage roll used in expected-value estimation.\n" +
             "0.925 = exact midpoint (unbiased). Raise to make the AI more aggressive, lower to be conservative.")]
    [Range(0.85f, 1f)]
    public float damageRollMidpoint = 0.925f;

    // ── Random Variation ──────────────────────────────────────────────────────

    [Header("Random Variation")]
    [Tooltip("Maximum random score bonus added to each non-pass action.\n" +
             "Adds unpredictability — monsters occasionally pick a slightly sub-optimal action.\n" +
             "0 = fully deterministic AI.")]
    public float randomJitter = 10f;

    // ── First-Actor Fatigue ───────────────────────────────────────────────────

    [Header("First-Actor Fatigue")]
    [Tooltip("After the same monster has acted first for this many consecutive enemy turns,\n" +
             "a fatigue penalty is subtracted from all its actions.\n" +
             "Set to 0 to disable.")]
    [Min(0)]
    public int firstActorFatigueLimit = 2;

    [Tooltip("Score subtracted from every action of the fatigued monster.\n" +
             "Stacks: each additional consecutive turn as first adds one more penalty on top.\n" +
             "Should be high enough to reliably push the monster out of the top slot.")]
    public float firstActorFatiguePenalty = 80f;

    // ── Repetition Penalty ────────────────────────────────────────────────────

    [Header("Repetition Penalty")]
    [Tooltip("Score subtracted from a monster's 2nd, 3rd... actions in the same turn queue.\n" +
             "Applied as: penalty × repetition index (so 2nd action loses 1×, 3rd loses 2×, etc.).\n\n" +
             "This prevents one strong monster from owning every slot in the queue.\n" +
             "A value around 25–50 is a good starting point:\n" +
             "  • Too low  → dominant monster still monopolises the turn.\n" +
             "  • Too high → every monster always acts in strict round-robin order.")]
    public float repetitionPenalty = 30f;

    // ── Base Score Values ─────────────────────────────────────────────────────
    // These are the base points given to every monster from game state.
    // MonsterPersonality adds bonus points on top of these.

    [Header("Attack — Base Score Values")]
    [Tooltip("Base points added when an action would kill the target.")]
    public float killShotBonus = 100f;

    [Tooltip("Base score multiplier applied to expected-damage-per-AP efficiency.")]
    public float damageEfficiencyMultiplier = 10f;

    [Tooltip("Base points added when targeting a monster already below the focus-fire HP threshold.")]
    public float focusFireBonus = 30f;

    [Header("Status Effects — Base Score Values")]
    [Tooltip("Base points for applying Freeze to a target.")]
    public float freezeBonus = 40f;

    [Tooltip("Base points for applying Burn or Poison (damage-over-time).")]
    public float dotBonus = 20f;

    [Tooltip("Base points for applying Shock (AP drain).")]
    public float shockBonus = 15f;

    [Header("Positioning — Base Score Values")]
    [Tooltip("Base points per tile closed toward the nearest player monster when evaluating a move.")]
    public float aggressionBonus = 5f;

    [Tooltip("Base points per additional player monster that can be reached from a destination tile.")]
    public float multiTargetBonus = 10f;

    [Header("Caution — Base Score Values")]
    [Tooltip("Base score penalty per obstruction tile on the path to a ranged attack target.")]
    public float obstructionPenalty = 15f;

    [Tooltip("Base score penalty applied to all attack actions when self HP is below the low-HP threshold.")]
    public float lowHPCautionPenalty = 20f;

    [Header("Survival — Base Score Values")]
    [Tooltip("Base points per tile moved away from each threatening player monster " +
             "when survival instinct is active.")]
    public float survivalInstinctBonus = 25f;

    [Header("Enemy Close Bonus — Base Score Values")]
    [Tooltip("Maximum tile distance at which the close-range attack bonus applies.\n" +
             "Manhattan distance from attacker to target. 1 = adjacent only.")]
    [Min(1)]
    public int enemyCloseThreshold = 2;

    [Tooltip("Base points added to an attack action when the target is within " +
             "enemyCloseThreshold tiles. Rewards aggressive close-range play.")]
    public float enemyCloseBonus = 20f;

    [Header("Frozen Target — Base Score Values")]
    [Tooltip("Base points added to any attack action when the target is currently frozen.\n" +
             "Frozen monsters cannot dodge, so attacks are more reliable.\n" +
             "This bonus expires after freezeBonusTurnDuration turns since the freeze was applied.")]
    public float attackFrozenBonus = 35f;

    [Tooltip("Number of turns after a freeze is applied during which attackFrozenBonus applies.\n" +
             "After this many turns, the AI stops prioritising the frozen target and moves on.\n" +
             "Example: 2 = bonus applies for 2 turns, then disappears.")]
    [Min(1)]
    public int freezeBonusTurnDuration = 2;

    [Header("Ally Healing — Base Score Values")]
    [Tooltip("Ally HP fraction (0–1) below which the healing bonus activates.\n" +
             "Default 0.5 = bonus triggers when an ally (or self) is below 50% HP.")]
    [Range(0f, 1f)]
    public float allyLowHPThreshold = 0.5f;

    [Tooltip("Base points added per ally (or self) below allyLowHPThreshold when\n" +
             "evaluating a healing action. Stacks — more injured allies = higher score.")]
    public float allyHealBonus = 40f;
}
