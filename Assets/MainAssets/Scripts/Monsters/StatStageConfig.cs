using UnityEngine;

/// <summary>
/// ScriptableObject that defines the stat-stage multiplier table used by the
/// battle system.  Stages range from -6 (heavy debuff) to +6 (heavy buff).
///
/// HOW TO CREATE THE ASSET:
///   In the Unity Project window right-click →
///   Create → Evermore → StatStageConfig
///   then assign it to every Monster prefab's "Stage Config" field.
///
/// STAGE → INDEX mapping:
///   Stage -6 = index 0,  Stage 0 = index 6,  Stage +6 = index 12
/// </summary>
[CreateAssetMenu(fileName = "StatStageConfig", menuName = "Evermore/StatStageConfig")]
public class StatStageConfig : ScriptableObject
{
    [Tooltip("Exactly 13 multipliers for stages -6 through +6.\n" +
             "Index 0 = stage -6  (20% of base stat)\n" +
             "Index 6 = stage  0  (100% — no change)\n" +
             "Index 12= stage +6  (250% of base stat)")]
    public float[] stageMultipliers = new float[13]
    {
        0.20f,  // stage -6
        0.35f,  // stage -5
        0.50f,  // stage -4
        0.65f,  // stage -3
        0.75f,  // stage -2
        0.90f,  // stage -1
        1.00f,  // stage  0  ← neutral
        1.25f,  // stage +1
        1.50f,  // stage +2
        1.75f,  // stage +3
        2.00f,  // stage +4
        2.25f,  // stage +5
        2.50f,  // stage +6
    };

    /// <summary>
    /// Returns the multiplier for the given stage, clamped to [-6, +6].
    /// Falls back to 1.0 if the array is misconfigured (not 13 entries).
    /// </summary>
    public float GetMultiplier(int stage)
    {
        if (stageMultipliers == null || stageMultipliers.Length != 13)
        {
            Debug.LogWarning("[StatStageConfig] stageMultipliers must have exactly 13 entries. " +
                             "Using neutral multiplier (1.0).");
            return 1f;
        }

        int index = Mathf.Clamp(stage + 6, 0, 12);
        return stageMultipliers[index];
    }
}
