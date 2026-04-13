using UnityEngine;

/// <summary>
/// Per-monster AI personality — bonus score points added on top of AIGameStateScorePoints.
///
/// Scoring formula:
///   finalScore = AIGameStateScorePoints (base) + MonsterPersonality (bonus)
///
/// All fields default to 0 — a monster with no personality assigned gets no bonus,
/// and simply uses the global base scores from AIGameStateScorePoints.
///
/// Assign one to MonsterData → AI → Personality in the Inspector.
/// Create via: Assets → Create → Evermore → AI → Monster Personality
/// </summary>
[CreateAssetMenu(menuName = "Evermore/AI/Monster Personality")]
public class MonsterPersonality : ScriptableObject
{
    [Header("Attack — Bonus Score Values")]
    [Tooltip("Extra points added on top of the global kill-shot base score.")]
    public float killShotBonus = 0f;

    [Tooltip("Extra multiplier added to damage-efficiency scoring.\n" +
             "Stacks with the global base multiplier.")]
    public float damageEfficiencyMultiplier = 0f;

    [Tooltip("Extra points added on top of the global focus-fire base score.")]
    public float focusFireBonus = 0f;

    [Header("Status Effects — Bonus Score Values")]
    [Tooltip("Extra points for applying Freeze, added on top of the global base.")]
    public float freezeBonus = 0f;

    [Tooltip("Extra points for applying Burn or Poison, added on top of the global base.")]
    public float dotBonus = 0f;

    [Tooltip("Extra points for applying Shock, added on top of the global base.")]
    public float shockBonus = 0f;

    [Header("Positioning — Bonus Score Values")]
    [Tooltip("Extra points per tile closed toward the nearest enemy, added on top of the global base.")]
    public float aggressionBonus = 0f;

    [Tooltip("Extra points per extra player in AoE range, added on top of the global base.")]
    public float multiTargetBonus = 0f;

    [Header("Caution — Bonus Score Values")]
    [Tooltip("Extra penalty per obstruction on a ranged path, added on top of the global base.")]
    public float obstructionPenalty = 0f;

    [Tooltip("Extra penalty applied to attack actions when self HP is low, added on top of the global base.")]
    public float lowHPCautionPenalty = 0f;

    [Header("Survival — Bonus Score Values")]
    [Tooltip("Extra points per tile moved away from each threat, added on top of the global base.")]
    public float survivalInstinctBonus = 0f;
}
