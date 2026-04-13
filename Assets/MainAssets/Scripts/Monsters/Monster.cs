using System.Collections.Generic;
using UnityEngine;

public class Monster : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField] private MonsterData data;
    [SerializeField] private bool enemyMonster;
    [SerializeField, Range(1, 100)] private int level;

    [SerializeField]
    private List<MonsterAttack> learnedAttacks = new List<MonsterAttack>();

    [SerializeField] private Animator anim;
    [SerializeField] private GridManager gridManager; // used for tile lookup on death

    [Header("Stat Stage System")]
    [Tooltip("Defines the multiplier table for stat stages -6 to +6.\n" +
             "Create one via: Assets → Create → Evermore → StatStageConfig\n" +
             "If left empty the system falls back to 1.0 (no stage effect).")]
    [SerializeField] private StatStageConfig stageConfig;


    [Header("Evasion Tuning")]
    [Tooltip("Scales how much the Dodge stat reduces incoming hit chance.\n" +
             "1.0 = full effect (Dodge subtracts directly from Accuracy).\n" +
             "0.5 = half effect (recommended default — Dodge is less dominant).\n" +
             "0.0 = Dodge has no effect at all.")]
    [SerializeField] [Range(0f, 1f)] private float dodgeEffectiveness = 0.5f;

    [Header("Obstruction System")]
    [Tooltip("Tuning values for ranged-attack obstruction penalties.\n" +
             "Create one via: Assets → Create → Evermore → ObstructionConfig\n" +
             "If left empty the system falls back to hardcoded defaults (50 % damage, −10 % acc).")]
    [SerializeField] private ObstructionConfig obstructionConfig;
    #endregion

    #region Constants
    private const int MaxAttacks = 2;
    private const int MaxIV = 24;
    private const float XpMultiplier = 25f;
    #endregion

    #region Runtime Stats
    private string customeName;
    private int currentHP;
    private long exp;
    private long expByLevel;
    #endregion

    #region Individual Values (IVs)
    private int ivHP;
    private int ivAttack;
    private int ivDefense;
    private int ivSpeed;
    private int ivCritRate;
    private int ivCritMod;
    private int ivDodge;
    #endregion

    #region Active Effects & Statuses
    private List<ActiveEffect> activeEffects = new List<ActiveEffect>();
    private List<ActiveStatus> activeStatuses = new List<ActiveStatus>();

    /// <summary>True while a Freeze status is active — the monster cannot act.</summary>
    public bool IsFrozen => HasActiveStatus(AttackEnum.StatusEffect.Freeze);

    /// <summary>
    /// Extra AP cost added to every action while Shock is active.
    /// Uses ExtraAPCost from the StatusEffectData. Stacks if multiple Shock entries are active.
    /// </summary>
    public int ShockAPCostIncrease => GetActiveStatusValue(AttackEnum.StatusEffect.Shock);

    private bool HasActiveStatus(AttackEnum.StatusEffect id)
    {
        foreach (var s in activeStatuses)
            if (s.data.ID == id) return true;
        return false;
    }

    private int GetActiveStatusValue(AttackEnum.StatusEffect id)
    {
        int total = 0;
        foreach (var s in activeStatuses)
            if (s.data.ID == id) total += s.data.ExtraAPCost;
        return total;
    }
    #endregion

    // -- TURN / AP INTEGRATION ------------------------------------------------
    #region Turn State


    /// <summary>
    /// True if this monster belongs to the enemy side.
    /// Driven by the existing enemyMonster serialized field.
    /// </summary>
    public bool IsEnemy => enemyMonster;

    /// <summary>The grid tile this monster currently occupies. Set by the spawner and updated on every move.</summary>
    public Tile CurrentTile { get; set; }

    /// <summary>
    /// How many tiles this monster moves per 1 AP spent.
    /// Derived from Speed: every 10 Speed = 1 tile/AP (minimum 1).
    /// Used for movement cost display and AP calculations.
    /// </summary>
    public int TilesPerAP => Mathf.Max(1, Speed / 10);

    // NOTE: Attack AP cost is NOT stored here.
    // It lives on AttackData.ConsumeActionPoints so each attack can have its own cost.

    /// <summary>
    /// True once this monster has used its action this turn.
    /// Automatically reset each turn by ResetForNewTurn().
    /// </summary>
    public bool HasActed { get; private set; }

    /// <summary>
    /// Marks this monster as done for the current turn.
    /// Called by PlayerTurnController or EnemyTurnController after spending AP.
    /// </summary>
    public void MarkActed()
    {
        HasActed = true;
        Debug.Log($"[{gameObject.name}] has acted -- locked for this turn.");
    }

    /// <summary>
    /// Resets HasActed and ticks all effects/statuses.
    /// Called by TurnController at the start of this side's turn.
    /// </summary>
    public void ResetForNewTurn()
    {
        HasActed = false;
        OnStartTurn(); // ticks effects and statuses (existing logic below)
    }

    #endregion
    // ------------------------------------------------------------------------

    #region HP Events & State
    /// <summary>Current HP (read-only from outside Monster).</summary>
    public int CurrentHP => currentHP;

    /// <summary>True while the monster is alive.</summary>
    public bool IsAlive => currentHP > 0;

    /// <summary>Fired whenever HP changes. Parameters: (currentHP, maxHP).</summary>
    public event System.Action<int, int> OnHPChanged;

    /// <summary>Fired once when HP reaches 0 (before the death animation plays).</summary>
    public event System.Action<Monster> OnDied;
    #endregion

    #region Public Properties - Calculated Stats
    public int MaxHP   => CalculateStat(data.baseHP,       ivHP,       AttackEnum.AttackBuffType.HP,       isHP: true);
    public int Attack  => CalculateStat(data.baseAttack,   ivAttack,   AttackEnum.AttackBuffType.Attack);
    public int Defense => CalculateStat(data.baseDefense,  ivDefense,  AttackEnum.AttackBuffType.Defense);
    // Speed does NOT scale with level — it returns baseSpeed directly so that
    // TilesPerAP stays constant and the Info panel shows the real movement rate.
    // Stage buffs/debuffs (e.g. from attacks) still apply via the active effects.
    public int Speed   => Mathf.Max(0, ApplyStageMultiplier(data != null ? data.baseSpeed : 0,
                                                             AttackEnum.AttackBuffType.Speed));
    public int CritRate=> CalculateStat(data.baseCritRate, ivCritRate, AttackEnum.AttackBuffType.CritRate);
    public int CritMod => CalculateStat(data.baseCritMod,  ivCritMod,  AttackEnum.AttackBuffType.CritMod);
    public int Dodge   => CalculateStat(data.baseDodge,    ivDodge,    AttackEnum.AttackBuffType.Dodge);
    public MonsterData Data  => data;
    /// <summary>Monster's current level — shown in the info panel.</summary>
    public int         Level => level;
    /// <summary>True for flying monsters — they ignore ground obstructions.</summary>
    public bool        IsFlying => data != null && data.isFlying;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();
    }

    private void Start()
    {
        if (!enemyMonster)
            LoadPlayerMonster();
        else
            LoadEnemyMonster();

        Debug.Log($"EXP: {exp}, Level: {level}");
    }
    #endregion

    #region Initialization
    private void LoadPlayerMonster()
    {
        // Set level and IVs FIRST so MaxHP is calculated with the correct values.
        level = 40;

        ivHP       = 10;
        ivAttack   = 12;
        ivDefense  = 8;
        ivSpeed    = 14;
        ivCritRate = 5;
        ivCritMod  = 3;
        ivDodge    = 7;

        // MaxHP now reflects level 40 + IVs correctly.
        currentHP   = MaxHP;
        // Use the MonsterData display name so battle messages say the real name.
        customeName = data != null ? data.displayName
                                   : gameObject.name.Replace("(Clone)", "").Trim();
        CalculateExp();
    }

    private void LoadEnemyMonster()
    {
        // Set IVs FIRST so MaxHP is calculated with the correct values.
        // (level is kept from the Inspector's serialized field.)
        ivHP       = Random.Range(0, MaxIV);
        ivAttack   = Random.Range(0, MaxIV);
        ivDefense  = Random.Range(0, MaxIV);
        ivSpeed    = Random.Range(0, MaxIV);
        ivCritRate = Random.Range(0, MaxIV);
        ivCritMod  = Random.Range(0, MaxIV);
        ivDodge    = Random.Range(0, MaxIV);

        // MaxHP now reflects the randomised IVs correctly.
        currentHP   = MaxHP;
        customeName = data != null ? data.displayName
                                   : gameObject.name.Replace("(Clone)", "").Trim();
        CalculateExp();
    }
    #endregion

    #region Attack Management
    public void LearnAttack(AttackData attack)
    {
        if (attack == null)
            throw new System.ArgumentException("Attack cannot be null");

        MonsterAttack monsterAttack = new MonsterAttack(attack);

        if (learnedAttacks.Contains(monsterAttack))
            throw new System.ArgumentException("Cannot learn the same attack twice");

        if (learnedAttacks.Count >= MaxAttacks)
            throw new System.ArgumentException($"{gameObject.name} cannot learn more than {MaxAttacks} attacks");

        learnedAttacks.Add(monsterAttack);
        Debug.Log($"{gameObject.name} learned attack: {attack.DisplayName}");
    }

    public void ForgetAttack(AttackData attack)
    {
        if (attack == null)
            throw new System.ArgumentException("Attack cannot be null");

        learnedAttacks.RemoveAll(ma => ma.data == attack);
    }

    public IReadOnlyList<MonsterAttack> GetAttacks() => learnedAttacks;
    #endregion

    #region Attack Execution
    public void ExecuteAttack(Monster target, int attackIndex, bool isDirect)
    {
        if (attackIndex < 0 || attackIndex >= learnedAttacks.Count)
            throw new System.ArgumentException("Invalid attack index");

        AttackData attackData = learnedAttacks[attackIndex].data;

        // Guard: AttackData not assigned in the Inspector
        if (attackData == null)
        {
            Debug.LogError($"[{gameObject.name}] Attack slot {attackIndex} has no AttackData " +
                           "assigned! Drag an AttackData asset onto the monster's learnedAttacks " +
                           "list in the Inspector.");
            BattleMessage.Show("Attack not configured!", 2.5f);
            return;
        }

        AttackEntry entry = System.Array.Find(data.movePool, e => e.attack == attackData);

        // ── Always apply effects immediately ────────────────────────────────────
        // Damage/heal/buff/status is ALWAYS applied right here, unconditionally.
        // We do NOT rely on animation events calling TriggerAttackEffect() because:
        //   1. Animations may not be configured yet.
        //   2. Animation events may not be set up on the clip.
        //   3. This keeps damage deterministic and independent of the animator.

        if (attackData.Effects.Count == 0)
        {
            Debug.LogWarning($"[{gameObject.name}] '{attackData.DisplayName}' has no effects " +
                             "configured! Add at least one Effect entry in the AttackData asset.");
            BattleMessage.Show($"'{attackData.DisplayName}' has no effects!\nOpen the AttackData asset and add a Damage effect.", 3f);
        }
        else
        {
            for (int i = 0; i < attackData.Effects.Count; i++)
                UseAttackEffect(this, target, attackIndex, i, isDirect);
        }

        // ── Trigger animation for visual feedback (purely cosmetic) ─────────────
        // AttackCommandManager is NOT set up — damage is already done above.
        // If an animation event fires TriggerAttackEffect(), AttackCommandManager
        // will find null attacker and return harmlessly without double-applying.
        if (entry != null && !string.IsNullOrEmpty(entry.AnimationTrigger) && anim != null)
        {
            Debug.Log($"[{gameObject.name}] Playing attack animation '{entry.AnimationTrigger}'.");
            anim.SetTrigger(entry.AnimationTrigger);
        }
        else
        {
            Debug.Log($"[{gameObject.name}] No animation configured for '{attackData.DisplayName}'.");
        }
    }

    public void TriggerAttackEffect(int effectIndex)
    {
        AttackCommandManager.Instance.TriggerAttackEffect(effectIndex);
    }

    public void UseAttackEffect(Monster attacker, Monster target,
        int attackIndex, int effectIndex, bool isDirect)
    {
        if (attacker == null || target == null)
            throw new System.ArgumentNullException("Attacker or target is null");
        if (attackIndex < 0 || attackIndex >= attacker.learnedAttacks.Count)
            throw new System.ArgumentException("Invalid attack index");

        MonsterAttack monsterAttack = attacker.learnedAttacks[attackIndex];
        AttackData attackData = monsterAttack.data;

        if (effectIndex < 0 || effectIndex >= attackData.Effects.Count)
            throw new System.ArgumentException("Invalid effect index");

        AttackEffect effect = attackData.Effects[effectIndex];
        Debug.Log($"{attacker.customeName} triggers {attackData.DisplayName} effect {effect.category}");

        if (effectIndex == 0) monsterAttack.UsePP();

        Monster effectTarget = effect.selfInflicted ? attacker : target;

        switch (effect.category)
        {
            case AttackEnum.AttackCategory.damage:
                ApplyDamage(attacker, target, effectTarget, attackData, isDirect, effect);
                break;
            case AttackEnum.AttackCategory.heal:
                ApplyHeal(attacker, target, effectTarget, attackData, isDirect, effect);
                break;
            case AttackEnum.AttackCategory.buff:
                effectTarget.CalculateBuff(target, effect);
                break;
            case AttackEnum.AttackCategory.status:
                effectTarget.CalculateStatus(target, effect);
                break;
        }
    }

    private void ApplyDamage(Monster attacker, Monster target, Monster effectTarget,
                             AttackData attackData, bool isDirect, AttackEffect effect)
    {
        int damage = attacker.CalculateDamage(target, attackData, isDirect, effect);
        int newHP = Mathf.Max(0, effectTarget.currentHP - damage);

        // ── Decide animation BEFORE applying HP ────────────────────────────
        if (damage <= 0)
            effectTarget.TriggerDodgeAnim();
        else if (newHP <= 0)
            effectTarget.TriggerDeathAnim();
        else
            effectTarget.TriggerDamageAnim();
        // ───────────────────────────────────────────────────────────────────

        effectTarget.currentHP = newHP;
        effectTarget.OnHPChanged?.Invoke(effectTarget.currentHP, effectTarget.MaxHP);

        if (damage <= 0)
        {
            BattleMessage.Show($"{effectTarget.customeName} dodged the attack!", 2.5f);
            Debug.Log($"[{effectTarget.gameObject.name}] dodged — HP unchanged: " +
                      $"{effectTarget.currentHP}/{effectTarget.MaxHP}");
        }
        else
        {
            BattleMessage.Show($"{attacker.customeName} → {effectTarget.customeName}: -{damage} HP  " +
                               $"({effectTarget.currentHP}/{effectTarget.MaxHP})", 3.5f);
            Debug.Log($"[{effectTarget.gameObject.name}] took {damage} damage — " +
                      $"HP: {effectTarget.currentHP}/{effectTarget.MaxHP}");
        }

        if (effectTarget.currentHP <= 0)
            effectTarget.HandleDeath();
    }

    private void ApplyHeal(Monster attacker, Monster target, Monster effectTarget,
        AttackData attackData, bool isDirect, AttackEffect effect)
    {
        // CalculateDamage returns a negative value for heals, so subtracting it adds HP.
        int healAmount = attacker.CalculateDamage(target, attackData, isDirect, effect);
        effectTarget.currentHP = Mathf.Min(effectTarget.MaxHP, effectTarget.currentHP - healAmount);
        effectTarget.OnHPChanged?.Invoke(effectTarget.currentHP, effectTarget.MaxHP);

        Debug.Log($"[{effectTarget.gameObject.name}] healed — " +
                  $"HP: {effectTarget.currentHP}/{effectTarget.MaxHP}");
    }

    private void HandleDeath()
    {
        currentHP = 0;
        OnDied?.Invoke(this);
        StartCoroutine(DieCoroutine());
    }

    private System.Collections.IEnumerator DieCoroutine()
    {
        Debug.Log($"[{gameObject.name}] died — playing shrink animation.");

        // Capture tile before position changes
        Tile myTile = gridManager?.GetTileAtWorldPosition(transform.position);

        // Shrink over 0.6 seconds
        Vector3 originalScale = transform.localScale;
        float elapsed = 0f;
        const float duration = 0.6f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, elapsed / duration);
            yield return null;
        }

        // Clear tile occupation
        myTile?.ClearOccupation();

        // Remove from the owning TurnController's roster
        if (IsEnemy)
            FindFirstObjectByType<EnemyTurnController>()?.RemoveMonster(this);
        else
            FindFirstObjectByType<PlayerTurnController>()?.RemoveMonster(this);

        Destroy(gameObject);
    }
    #endregion

    #region Stat Calculations
    public void CalculateLevel()
    {
        level = (int)Mathf.Floor(
            (XpMultiplier + Mathf.Sqrt(Mathf.Pow(XpMultiplier, 2) + 4 * XpMultiplier * exp))
            / (2 * XpMultiplier));
    }

    public void CalculateExp()
    {
        long l = level, multiplier = (long)XpMultiplier, temp = (2 * l - 1);
        exp = multiplier * (temp * temp - 1) / 4;
        UpdateExpByLevel();
    }

    public void UpdateExpByLevel()
    {
        long l = level, multiplier = (long)XpMultiplier, temp = (2 * l - 1);
        expByLevel = multiplier * (temp * temp - 1) / 4;
    }

    /// <summary>
    /// Calculates a final stat value by:
    ///   1. Computing the base value from baseStat, iv, and level.
    ///   2. Summing all active stage changes for <paramref name="buffType"/>.
    ///   3. Clamping total stages to [-6, +6].
    ///   4. Multiplying the base value by the stage multiplier from <see cref="stageConfig"/>.
    ///
    /// Stage changes are stored as integers in <see cref="ActiveEffect.value"/>.
    /// E.g. value=+2 means "raise two stages", value=-1 means "lower one stage".
    /// </summary>
    private int CalculateStat(int baseStat, int iv,
                               AttackEnum.AttackBuffType buffType, bool isHP = false)
    {
        // ── 1. Base flat value (level + IV formula) ───────────────────────────
        int baseValue = Mathf.FloorToInt(((baseStat + iv) * level) / 50f) + (isHP ? 10 : 5);

        // ── 2. Sum active stage changes for this stat ─────────────────────────
        int totalStages = 0;
        foreach (var effect in activeEffects)
            if (effect.stat == buffType)
                totalStages += effect.value;

        // ── 3. Clamp to valid stage range [-6, +6] ────────────────────────────
        totalStages = Mathf.Clamp(totalStages, -6, 6);

        // ── 4. Apply multiplier from StatStageConfig (falls back to 1.0) ──────
        float multiplier = stageConfig != null ? stageConfig.GetMultiplier(totalStages) : 1f;
        int finalValue   = Mathf.RoundToInt(baseValue * multiplier);

        return Mathf.Max(0, finalValue);
    }

    /// <summary>
    /// Applies only the stage-multiplier portion of stat calculation to a raw base value,
    /// without any level or IV scaling. Used for Speed which intentionally skips level growth.
    /// </summary>
    private int ApplyStageMultiplier(int baseValue, AttackEnum.AttackBuffType buffType)
    {
        int totalStages = 0;
        foreach (var effect in activeEffects)
            if (effect.stat == buffType)
                totalStages += effect.value;
        totalStages = Mathf.Clamp(totalStages, -6, 6);
        float multiplier = stageConfig != null ? stageConfig.GetMultiplier(totalStages) : 1f;
        return Mathf.RoundToInt(baseValue * multiplier);
    }

    /// <summary>
    /// Returns the effective stage (-6 to +6) currently applied to <paramref name="buffType"/>.
    /// Useful for displaying stage info in the UI.
    /// </summary>
    public int GetCurrentStage(AttackEnum.AttackBuffType buffType)
    {
        int total = 0;
        foreach (var effect in activeEffects)
            if (effect.stat == buffType)
                total += effect.value;
        return Mathf.Clamp(total, -6, 6);
    }

    private int CalculateDamage(Monster target, AttackData attack, bool isDirect, AttackEffect attackEffect)
    {
        if (target == null) throw new System.ArgumentNullException(nameof(target));
        if (attack == null) throw new System.ArgumentNullException(nameof(attack));

        if (attackEffect.category == AttackEnum.AttackCategory.heal)
            return -Mathf.FloorToInt(((level * attackEffect.value) / 50f) + 2f);

        // ── Hit chance ────────────────────────────────────────────────────────
        // Dodge is scaled by the TARGET's dodgeEffectiveness so the level designer
        // can tune how dominant evasion is per monster type without rewriting code.
        float scaledDodge = target.Dodge * target.dodgeEffectiveness;
        float hitChance   = Mathf.Clamp(attack.Accuracy - scaledDodge, 5f, 100f);

        // ── Obstruction penalty (ranged attacks only) ─────────────────────────
        // Direct/melee attacks bypass obstructions entirely.
        // Ranged attacks lose accuracy and deal reduced damage when the straight
        // line between attacker and target crosses one or more obstruction tiles.
        bool  isRanged             = !attack.IsDirect && !isDirect;
        float obstructionDamageMult = 1f;

        if (isRanged && gridManager != null)
        {
            Tile attackerTile = gridManager.GetTileAtWorldPosition(transform.position);
            Tile targetTile   = gridManager.GetTileAtWorldPosition(target.transform.position);

            if (attackerTile != null && targetTile != null)
            {
                int obstructions = gridManager.ObstructionsBetween(attackerTile, targetTile);
                if (obstructions > 0)
                {
                    // Use configured values; fall back to safe defaults if config is null
                    float accPenalty  = obstructionConfig != null
                        ? obstructionConfig.accuracyPenaltyPerObstruction * obstructions
                        : 10f * obstructions;
                    float damageMult  = obstructionConfig != null
                        ? obstructionConfig.obstructedDamageMultiplier
                        : 0.50f;

                    hitChance = Mathf.Clamp(hitChance - accPenalty, 5f, 100f);

                    // Extra near-obstruction penalty when the attacker is close
                    if (obstructionConfig != null)
                    {
                        int nearDist = gridManager.DistanceToNearestObstruction(
                            attackerTile, targetTile, attackerTile);
                        if (nearDist <= obstructionConfig.nearDistanceThreshold)
                            damageMult *= (1f - obstructionConfig.nearObstructionExtraPenalty);
                    }

                    obstructionDamageMult = damageMult;
                    Debug.Log($"[{gameObject.name}] Ranged attack obstructed " +
                              $"({obstructions} tile(s)) — " +
                              $"accPenalty={accPenalty:F0}%, " +
                              $"damageMult={obstructionDamageMult:F2}");
                }
            }
        }

        // ── Dodge roll ────────────────────────────────────────────────────────
        // Direct attacks (IsDirect = true) always hit — skip the dodge roll entirely.
        bool alwaysHits = attack.IsDirect || isDirect || attack.GuaranteedHit;
        if (!alwaysHits && Random.value * 100f > hitChance)
        {
            Debug.Log($"{target.customeName} avoided the attack!");
            return 0;
        }

        // ── Damage calculation ────────────────────────────────────────────────
        float levelFactor        = (2f * level) / 5f + 2f;
        float attackDefenseRatio = (float)Attack / Mathf.Max(1, target.Defense);
        float baseDamage         = ((levelFactor * attackEffect.value * attackDefenseRatio) / 50f) + 2f;

        //if (Random.value < (CritRate / 100f)) baseDamage *= CritMod;
        if (Random.value < (CritRate / 100f)) baseDamage += baseDamage * (CritMod / 100f);
        if (isRanged)               baseDamage *= attack.InDirectHitPercent;
        baseDamage                             *= obstructionDamageMult;   // 1.0 if unobstructed

        return Mathf.Max(1, Mathf.FloorToInt(baseDamage * Random.Range(0.85f, 1f)));
    }
    #endregion

    #region Effect Application
    private void CalculateBuff(Monster target, AttackEffect effect)
    {
        if (Random.Range(0f, 100f) > effect.chance)
        {
            Debug.Log($"{target.customeName} avoided the buff/debuff!");
            return;
        }

        // effect.value is now a STAGE change (e.g. +2 = raise two stages, -1 = lower one).
        int stageChange = effect.isDebuff ? -effect.stageCount : effect.stageCount;
        target.activeEffects.Add(new ActiveEffect(effect.buffType, stageChange, effect.duration));

        int currentStage = target.GetCurrentStage(effect.buffType);
        string direction = stageChange >= 0 ? "rose" : "fell";
        Debug.Log($"{target.customeName}'s {effect.buffType} {direction} " +
                  $"(stage {stageChange:+0;-0}, now at stage {currentStage}) " +
                  $"for {effect.duration} turn(s)!");

        BattleMessage.Show(
            $"{target.customeName}'s {effect.buffType} {direction}! " +
            $"(stage {stageChange:+0;-0})", 1.5f);
    }

    private void CalculateStatus(Monster target, AttackEffect effect)
    {
        if (effect.statusEffect == null) return;

        // ── Dispel: remove instead of applying ───────────────────────────────
        if (effect.statusEffect.Dispel)
        {
            if (effect.statusEffect.DispelAll)
            {
                int count = target.activeStatuses.Count;
                target.activeStatuses.Clear();
                Debug.Log($"{target.customeName} had all {count} status effect(s) dispelled!");
                BattleMessage.Show($"{target.customeName}'s status effects were all cleared!", 2f);
            }
            else
            {
                int removed = target.activeStatuses.RemoveAll(
                    s => s.data.ID == effect.statusEffect.ID);

                if (removed > 0)
                {
                    Debug.Log($"{target.customeName}'s {effect.statusEffect.ID} was dispelled!");
                    BattleMessage.Show($"{target.customeName}'s {effect.statusEffect.ID} was dispelled!", 2f);
                }
                else
                {
                    Debug.Log($"{target.customeName} had no {effect.statusEffect.ID} to dispel.");
                    BattleMessage.Show($"No {effect.statusEffect.ID} on {target.customeName} to dispel.", 2f);
                }
            }
            return;
        }
        // ─────────────────────────────────────────────────────────────────────

        if (Random.Range(0f, 100f) > effect.chance)
        {
            Debug.Log($"{target.customeName} resisted the status!");
            return;
        }

        for (int i = 0; i < target.activeStatuses.Count; i++)
        {
            if (target.activeStatuses[i].data == effect.statusEffect)
            {
                target.activeStatuses[i].remainingTurns =
                    effect.statusEffect.Stacks
                        ? target.activeStatuses[i].remainingTurns + effect.duration
                        : effect.duration;
                return;
            }
        }

        target.activeStatuses.Add(new ActiveStatus(effect.statusEffect, effect.duration));
        Debug.Log($"{target.customeName} is affected by {effect.statusEffect.name}!");
    }
    #endregion

    #region Experience & Leveling
    public void AddExp(long xp)
    {
        if (xp < 0) throw new System.ArgumentException("Experience must be positive");
        exp += xp;
    }

    private void LevelUp(long xp)
    {
        AddExp(xp);
        CalculateLevel();
        int oldMaxHP = MaxHP;
        currentHP += (MaxHP - oldMaxHP);
    }
    #endregion

    #region Turn Management
    /// <summary>Called by ResetForNewTurn(). Ticks effects and statuses.</summary>
    public void OnStartTurn()
    {
        TickEffects();
        TickStatuses();
    }

    public void OnEndTurn() { }

    private void TickEffects()
    {
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            activeEffects[i].remainingTurns--;
            if (activeEffects[i].remainingTurns <= 0)
                activeEffects.RemoveAt(i);
        }
    }

    private void TickStatuses()
    {
        for (int i = activeStatuses.Count - 1; i >= 0; i--)
        {
            ActiveStatus status = activeStatuses[i];

            // ── Apply DoT damage if the status deals damage ─────────────────
            if (status.data.Damage > 0)
            {
                int newHP = Mathf.Max(0, currentHP - status.data.Damage);

                if (newHP <= 0)
                    TriggerDeathAnim();
                else
                    TriggerDamageAnim();

                currentHP = newHP;
                OnHPChanged?.Invoke(currentHP, MaxHP);

                BattleMessage.Show($"{customeName} took {status.data.Damage} damage " +
                                   $"from {status.data.name}! ({currentHP}/{MaxHP})", 1.5f);
                Debug.Log($"[{gameObject.name}] {status.data.name} DoT: -{status.data.Damage} " +
                          $"HP — {currentHP}/{MaxHP}");

                if (currentHP <= 0)
                {
                    HandleDeath();
                    return; // monster is dead, stop processing remaining statuses
                }
            }
            // ────────────────────────────────────────────────────────────────

            status.remainingTurns--;
            if (status.remainingTurns <= 0)
            {
                Debug.Log($"{customeName} is no longer affected by {status.data.name}.");
                activeStatuses.RemoveAt(i);
            }
        }
    }
    #endregion

    #region Animation Initialization
    #region utility methods for triggering animations from AttackEntry.AnimationTrigger
    private void TriggerAnimationFromTrigger(string trigger)
    {
        anim.SetTrigger(trigger);
    }

    private void TriggerAnimationFromBool(string trigger, bool value)
    {
        anim.SetBool(trigger, value);
    }
    #endregion

    #region helper methods for triggering specific animations based on MonsterData configuration
    private bool CheckAnimTriggerString(string triger)
    {
        return anim != null && !string.IsNullOrEmpty(triger);
    }
    private void TriggerMovementAnimation(bool IsMoving)
    {
        string triger = data.MovementAnimationBoolean;

        if (CheckAnimTriggerString(triger))
            TriggerAnimationFromBool(triger, IsMoving);
    }

    private void TrigerTrigerAnimation(string triger)
    {
        if (CheckAnimTriggerString(triger))
            TriggerAnimationFromTrigger(triger);
    }

    #endregion
    #region Public Animation Seters
    public void TriggerMovementAnimationStart()
    {
        TriggerMovementAnimation(true);
    }

    public void TriggerMovementAnimationEnd()
    {
        TriggerMovementAnimation(false);
    }

    #region Private Animation Seters
    public void TriggerDamageAnim()
    {
        TrigerTrigerAnimation(data.DoamageAnimationTrigger);
    }

    public void TriggerDodgeAnim()
    {
        TrigerTrigerAnimation(data.DogeAnimationTrigger);
    }

    public void TriggerDeathAnim()
    {
        TrigerTrigerAnimation(data.DeathAnimationTrigger);
    }
    #endregion
    #endregion
    #endregion
}