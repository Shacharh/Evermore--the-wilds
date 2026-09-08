using UnityEngine;

/// <summary>
/// Per-species taming profile. Assign to MonsterData → Taming → Taming Personality.
/// Create via: Assets → Create → Evermore → Taming → Taming Personality
/// </summary>
[CreateAssetMenu(menuName = "Evermore/Taming/Taming Personality")]
public class TamingPersonality : ScriptableObject
{
    [Header("Base Acceptance")]
    [Tooltip("Baseline probability (0–1) that this species agrees to talk.")]
    [Range(0f, 1f)] public float baseAcceptance = 0.40f;

    [Header("Acceptance Formula Weights")]
    [Tooltip("Maximum bonus added when the monster is at 0 HP.")]
    [Range(0f, 0.5f)]  public float hpBonusMax           = 0.30f;
    [Tooltip("Higher = BST matters less. BST-300 monster gets half the HP bonus.")]
    [Min(1f)]          public float bstNormDivisor        = 300f;
    [Range(0f, 0.2f)]  public float critBonus             = 0.10f;
    [Range(0f, 0.15f)] public float statusEffectBonus     = 0.05f;
    [Range(0f, 0.25f)] public float superEffectiveBonus   = 0.15f;
    [Range(0f, 0.2f)]  public float failedAttemptPenalty  = 0.10f;

    [Header("Dialogue")]
    [Tooltip("Maximum acceptance boost a perfect Q&A session can add.")]
    [Range(0f, 1f)] public float dialogueMaxBonus      = 0.40f;
    [Tooltip("AP spent by the player just to open the dialogue.")]
    [Min(0)]        public int   dialogueInitiationCost = 1;
    [Tooltip("AP debt applied when the player picks a ReallyBad answer.")]
    [Min(1)]        public int   apDebtPerReallyBad     = 4;

    [Header("Rewards")]
    [Tooltip("Gold given when the final roll fails but the player answered at least one question correctly.")]
    [Min(0)] public int partialSuccessGoldReward = 50;

    [Header("Debug")]
    [Tooltip("When ticked, answering every question correctly guarantees a tame — skips the final roll.")]
    public bool debugGuaranteeTame;
}
