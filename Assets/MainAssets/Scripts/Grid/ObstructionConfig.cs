using UnityEngine;

/// <summary>
/// ScriptableObject holding all tunable parameters for the obstruction system.
///
/// Create one via: Assets → Create → Evermore → ObstructionConfig
/// Assign it to each Monster prefab's "Obstruction Config" Inspector field.
///
/// Values control:
///   • How much ranged damage is reduced when shooting through an obstruction.
///   • An extra penalty when the attacker is very close to the obstruction.
///   • How much accuracy is lost per obstruction tile in the line of fire.
///
/// Direct (melee) attacks are NEVER penalised — only ranged shots that cross
/// one or more tiles with OccupationType.Obstruction.
/// </summary>
[CreateAssetMenu(fileName = "ObstructionConfig", menuName = "Evermore/ObstructionConfig")]
public class ObstructionConfig : ScriptableObject
{
    [Header("Ranged Damage Penalties")]

    [Tooltip("Damage multiplier applied when a ranged attack passes through one or more " +
             "obstruction tiles. 0.5 = 50 % of normal damage.\n" +
             "Applied once regardless of how many obstructions are in the way.")]
    [Range(0f, 1f)]
    public float obstructedDamageMultiplier = 0.50f;

    [Tooltip("Extra damage reduction when the attacker is within nearDistanceThreshold " +
             "tiles of an obstruction. Applied on top of obstructedDamageMultiplier.\n" +
             "0.15 → final mult becomes obstructedDamageMultiplier × (1 − 0.15).")]
    [Range(0f, 1f)]
    public float nearObstructionExtraPenalty = 0.15f;

    [Tooltip("Manhattan distance from the attacker to the nearest obstruction tile at " +
             "which the near-obstruction extra penalty activates.")]
    [Range(1, 5)]
    public int nearDistanceThreshold = 2;

    [Header("Accuracy Penalty")]

    [Tooltip("Accuracy reduction per obstruction tile on the straight line between " +
             "attacker and target. Stacks with multiple obstructions.\n" +
             "E.g. 10 → −10 % accuracy for one obstruction, −20 % for two, etc.")]
    [Range(0f, 50f)]
    public float accuracyPenaltyPerObstruction = 10f;
}
