using UnityEngine;

/// <summary>
/// Global AI scoring baseline shared by every enemy monster.
///
/// These values define how the AI scores actions based on the current game state.
/// Every monster starts with these scores. MonsterPersonality then adds bonus points
/// on top for monsters that have one assigned.
///
/// Scoring formula:
///   finalScore = AIGameStateScorePoints (base) + MonsterPersonality (bonus)
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
             "Default 0.3 = bonus triggers when target is below 30 % HP.")]
    [Range(0f, 1f)]
    public float focusFireHPThreshold = 0.30f;

    [Tooltip("Self HP fraction (0–1) below which the low-HP caution penalty activates.\n" +
             "Default 0.25 = penalty triggers when self is below 25 % HP.")]
    [Range(0f, 1f)]
    public float lowHPCautionThreshold = 0.25f;

    [Tooltip("Self HP fraction (0–1) below which survival instinct can activate.\n" +
             "Survival instinct also requires a player to have a one-shot attack in range.\n" +
             "Default 0.3 = activates when self is below 30 % HP.")]
    [Range(0f, 1f)]
    public float dangerHPThreshold = 0.30f;

    [Header("Estimation Constants")]
    [Tooltip("Score penalty subtracted from MoveAndAttackAction candidates.\n" +
             "Ensures attacking from the current tile is preferred when scores are equal.")]
    public float repositionPenalty = 3f;

    [Tooltip("Score added for a status effect type not explicitly handled (not Freeze / Burn / Poison / Shock).")]
    public float unknownStatusScore = 5f;

    [Tooltip("Damage multiplier used in estimation when a ranged attack path is obstructed.\n" +
             "Default 0.5 = obstructed attacks estimated at half damage.")]
    [Range(0f, 1f)]
    public float obstructedDamageMultiplier = 0.50f;

    [Tooltip("Midpoint of the 0.85–1.0 random damage roll used in expected-value estimation.\n" +
             "0.925 = exact midpoint (unbiased). Raise to make the AI more aggressive.\n" +
             "Lower to make it more conservative.")]
    [Range(0.85f, 1f)]
    public float damageRollMidpoint = 0.925f;

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
    [Tooltip("Base points per tile closed toward the nearest enemy when evaluating a move.")]
    public float aggressionBonus = 5f;

    [Tooltip("Base points per additional player monster caught in AoE range from a tile.")]
    public float multiTargetBonus = 10f;

    [Header("Caution — Base Score Values")]
    [Tooltip("Base score penalty per obstruction tile on the path to a ranged attack target.")]
    public float obstructionPenalty = 15f;

    [Tooltip("Base score penalty applied to all attack actions when self HP is below the low-HP threshold.")]
    public float lowHPCautionPenalty = 20f;

    [Header("Survival — Base Score Values")]
    [Tooltip("Base points per tile moved away from each threatening player when survival instinct is active.")]
    public float survivalInstinctBonus = 25f;
}
