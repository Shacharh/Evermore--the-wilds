using UnityEngine;

/// <summary>
/// Per-monster AI personality — bonus score points added on top of AIGameStateScorePoints.
///
/// Final score formula:
///   finalScore = AIGameStateScorePoints (base) + MonsterPersonality (bonus) + Random(0, randomJitter)
///
/// All fields default to 0 — a monster with no personality assigned gets no bonus
/// and simply uses the global base scores from AIGameStateScorePoints.
///
/// Assign one to MonsterData → AI → Personality in the Inspector.
/// Create via: Assets → Create → Evermore → AI → Monster Personality
///
/// Example personalities:
///   Berserker   — high killShotBonus, negative lowHPCautionPenalty (ignores danger), negative survivalInstinctBonus
///   Tactician   — high freezeBonus, high multiTargetBonus, negative aggressionBonus (waits for openings)
///   Coward      — high survivalInstinctBonus, negative aggressionBonus (retreats when threatened)
///   Assassin    — high focusFireBonus, high killShotBonus (always finishes wounded targets)
/// </summary>
[CreateAssetMenu(menuName = "Evermore/AI/Monster Personality")]
public class MonsterPersonality : ScriptableObject
{
    [Header("Attack — Bonus Score Values")]
    [Tooltip("Extra points added on top of the global kill-shot base score.\n" +
             "Positive = monster prioritises finishing enemies. Negative = monster ignores kill shots.")]
    public float killShotBonus = 0f;

    [Tooltip("Extra multiplier added to damage-efficiency scoring.\n" +
             "Stacks with the global base multiplier.")]
    public float damageEfficiencyMultiplier = 0f;

    [Tooltip("Extra points added on top of the global focus-fire base score.\n" +
             "Positive = monster loves targeting already-wounded enemies.")]
    public float focusFireBonus = 0f;

    [Header("Status Effects — Bonus Score Values")]
    [Tooltip("Extra points for applying Freeze, added on top of the global base.")]
    public float freezeBonus = 0f;

    [Tooltip("Extra points for applying Burn or Poison, added on top of the global base.")]
    public float dotBonus = 0f;

    [Tooltip("Extra points for applying Shock, added on top of the global base.")]
    public float shockBonus = 0f;

    [Header("Positioning — Bonus Score Values")]
    [Tooltip("Extra points per tile closed toward the nearest player, added on top of the global base.\n" +
             "Negative values make the monster reluctant to advance.")]
    public float aggressionBonus = 0f;

    [Tooltip("Extra points per extra player in AoE range, added on top of the global base.")]
    public float multiTargetBonus = 0f;

    [Header("Caution — Bonus Score Values")]
    [Tooltip("Extra penalty per obstruction on a ranged path, added on top of the global base.")]
    public float obstructionPenalty = 0f;

    [Tooltip("Extra penalty applied to attack actions when self HP is low, added on top of the global base.\n" +
             "Negative values make the monster ignore self-preservation (berserker).")]
    public float lowHPCautionPenalty = 0f;

    [Header("Survival — Bonus Score Values")]
    [Tooltip("Extra points per tile moved away from each threat, added on top of the global base.\n" +
             "Negative values make the monster stand its ground even when in danger.")]
    public float survivalInstinctBonus = 0f;

    [Header("Enemy Close — Bonus Score Values")]
    [Tooltip("Extra points for attacking a close-range target, added on top of the global base.")]
    public float enemyCloseBonus = 0f;

    [Header("Frozen Target — Bonus Score Values")]
    [Tooltip("Extra points for attacking a frozen target, added on top of the global base.")]
    public float attackFrozenBonus = 0f;

    [Header("Ally Healing — Bonus Score Values")]
    [Tooltip("Extra points per injured ally when evaluating a healing action, " +
             "added on top of the global base.")]
    public float allyHealBonus = 0f;

    [Header("Guaranteed Window Appearance")]
    [Tooltip("When enabled, this monster is guaranteed to appear at least once in every block\n" +
             "of 'guaranteedEveryXActions' slots in the turn queue.\n" +
             "Example: X=3 → must appear in slots 1-3, then 4-6, then 7-9, etc.")]
    public bool guaranteedAppearanceEnabled = false;

    [Tooltip("Window size for the guaranteed appearance. The monster must appear at least once\n" +
             "in every consecutive group of this many queue slots.")]
    [Min(1)]
    public int guaranteedEveryXActions = 3;

    [Header("Guaranteed Exact Slot")]
    [Tooltip("When enabled, this monster's best-scoring action is forced to appear at exactly\n" +
             "the position set in 'guaranteedExactSlot', regardless of its score.\n" +
             "Applied after all score-based sorting. Use for a support monster that should\n" +
             "always act 2nd, a finisher that always acts last, etc.")]
    public bool guaranteedExactSlotEnabled = false;

    [Tooltip("1-based slot position this monster's best action is forced to.\n" +
             "1 = first, 2 = second, etc. Clamped to the actual queue length.")]
    [Min(1)]
    public int guaranteedExactSlot = 2;
}
