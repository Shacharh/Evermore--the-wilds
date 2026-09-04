using UnityEngine;

/// <summary>
/// Orchestrates the full taming flow:
///   1. Acceptance roll (accept or refuse)
///   2. Open DialogueUI for the question sequence
///   3. Resolve outcome (capture / partial / fail / penalty)
/// </summary>
public class TamingSystem : MonoBehaviour
{
    public static TamingSystem Instance { get; private set; }

    private Monster _pendingTarget;

    /// <summary>Accumulated bonus from AcceptanceRateEnhancing items. Consumed on next roll.</summary>
    public float PendingAcceptanceBonus { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        new GameObject("TamingSystem").AddComponent<TamingSystem>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    public void AddAcceptanceBonus(float bonus) => PendingAcceptanceBonus += bonus;

    /// <summary>Called when the player selects "Dialogue" from the radial menu on an enemy.</summary>
    public void AttemptDialogue(Monster target)
    {
        if (target == null) return;

        string monName = target.Data?.displayName ?? target.name;

        if (target.CommunicationLocked)
        {
            BattleMessage.Show($"{monName} refuses to speak with you.", 2.5f);
            return;
        }

        var personality = target.Data?.tamingPersonality;
        if (personality == null)
        {
            Debug.LogWarning($"[TamingSystem] {monName} has no TamingPersonality assigned — cannot tame.");
            BattleMessage.Show($"{monName} stares blankly and ignores you.", 2f);
            return;
        }

        if (target.Data?.dialogueGraph == null)
        {
            Debug.LogWarning($"[TamingSystem] {monName} has no DialogueGraph — auto-failing taming.");
            BattleMessage.Show($"{monName} stares blankly and ignores you.", 2f);
            return;
        }

        var ptc = FindFirstObjectByType<PlayerTurnController>();
        if (ptc == null) return;

        if (!ptc.TrySpendAPForDialogue(personality.dialogueInitiationCost)) return;

        float acceptance = ComputeAcceptance(target, personality);
        acceptance += PendingAcceptanceBonus;
        PendingAcceptanceBonus = 0f;
        acceptance = Mathf.Clamp01(acceptance);

        Debug.Log($"[TamingSystem] Acceptance for {target.name}: {acceptance:P0}");

        if (Random.value > acceptance)
        {
            target.IncrementFailedAttempts();
            BattleMessage.Show($"{monName} refuses to talk!", 2f);
            return;
        }

        _pendingTarget = target;
        DialogueUI.Instance?.OpenTaming(target, target.Data.dialogueGraph);
    }

    /// <summary>Called by DialogueUI when the sequence ends.</summary>
    /// <param name="outcome">Dialogue performance result (used for penalty detection).</param>
    /// <param name="dialogueScore">Fraction of questions answered correctly (0–1).</param>
    public void OnDialogueComplete(DialogueEnum.DialogueOutcome outcome, float dialogueScore)
    {
        var target = _pendingTarget;
        _pendingTarget = null;
        if (target == null) return;

        var personality = target.Data?.tamingPersonality;
        string name = target.Data?.displayName ?? target.name;

        if (outcome == DialogueEnum.DialogueOutcome.CapturePenalty)
        {
            int debt = personality?.apDebtPerReallyBad ?? 4;
            FindFirstObjectByType<PlayerTurnController>()?.AddAPDebt(debt);
            target.LockCommunication();
            int healAmt = Mathf.RoundToInt(target.MaxHP * 0.15f);
            target.HealHP(healAmt);
            BattleMessage.Show($"{name} is furious! It healed {healAmt} HP. You're in AP debt!", 3.5f);
            return;
        }

        RollForCapture(target, dialogueScore, personality);
    }

    private void RollForCapture(Monster target, float dialogueScore, TamingPersonality personality)
    {
        string name = target.Data?.displayName ?? target.name;

        if ((personality?.debugGuaranteeTame ?? false) && Mathf.Approximately(dialogueScore, 1f))
        {
            TameMonster(target);
            BattleMessage.Show($"{name} joined your team! (debug)", 3f);
            return;
        }

        float dialogueBonus = dialogueScore * (personality?.dialogueMaxBonus ?? 0.40f);
        float finalChance   = Mathf.Clamp01(ComputeAcceptance(target, personality) + dialogueBonus);

        Debug.Log($"[TamingSystem] Final tame roll for {target.name}: " +
                  $"chance={finalChance:P0} (dialogue bonus {dialogueBonus:P0})");

        if (Random.value <= finalChance)
        {
            TameMonster(target);
            BattleMessage.Show($"{name} joined your team!", 3f);
        }
        else if (dialogueScore > 0f)
        {
            int gold = personality?.partialSuccessGoldReward ?? 50;
            BattleMessage.Show($"{name} felt something… but wasn't ready. You received {gold} gold!", 3f);
        }
        else
        {
            BattleMessage.Show($"{name} wasn't swayed.", 2f);
        }
    }

    // ── Acceptance Formula ─────────────────────────────────────────────────────

    private static float ComputeAcceptance(Monster target, TamingPersonality p)
    {
        if (target.Data == null || p == null) return 0f;

        float a = p.baseAcceptance;

        float hpMissing = 1f - (float)target.CurrentHP / Mathf.Max(1, target.MaxHP);
        float bstScale  = 1f / (1f + (float)target.Data.BST / Mathf.Max(1f, p.bstNormDivisor));
        a += hpMissing * p.hpBonusMax * bstScale;

        if (target.WasLastHitCrit)           a += p.critBonus;
        if (target.WasLastHitSuperEffective) a += p.superEffectiveBonus;

        a += target.ActiveStatuses.Count * p.statusEffectBonus;
        a -= target.FailedDialogueAttempts * p.failedAttemptPenalty;

        return Mathf.Clamp01(a);
    }

    // ── Capture ────────────────────────────────────────────────────────────────

    private static void TameMonster(Monster target)
    {
        FindFirstObjectByType<EnemyTurnController>()?.RemoveMonster(target);
        target.TameMonster();
        FindFirstObjectByType<PlayerTurnController>()?.AddMonster(target);
        HUDController.RefreshRosters();
    }
}
