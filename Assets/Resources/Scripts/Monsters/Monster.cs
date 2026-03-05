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
    #endregion

    // -- TURN / AP INTEGRATION ------------------------------------------------
    #region Turn State


    /// <summary>
    /// True if this monster belongs to the enemy side.
    /// Driven by the existing enemyMonster serialized field.
    /// </summary>
    public bool IsEnemy => enemyMonster;

    /// <summary>AP cost to move this monster one tile.</summary>
    /// <summary>AP cost to move this monster one tile. Defined on MonsterData ScriptableObject.</summary>
    public int MoveCost => data.moveCost;

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

    #region Public Properties - Calculated Stats
    public int MaxHP => CalculateStat(data.baseHP, ivHP, true);
    public int Attack => CalculateStat(data.baseAttack, ivAttack);
    public int Defense => CalculateStat(data.baseDefense, ivDefense);
    public int Speed => CalculateStat(data.baseSpeed, ivSpeed);
    public int CritRate => CalculateStat(data.baseCritRate, ivCritRate);
    public int CritMod => CalculateStat(data.baseCritMod, ivCritMod);
    public int Dodge => CalculateStat(data.baseDodge, ivDodge);
    public MonsterData Data => data;
    #endregion

    #region Unity Lifecycle
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
        currentHP = MaxHP;
        level = 40;
        CalculateExp();
        customeName = "custom name";

        ivHP = 10;
        ivAttack = 12;
        ivDefense = 8;
        ivSpeed = 14;
        ivCritRate = 5;
        ivCritMod = 3;
        ivDodge = 7;
    }

    private void LoadEnemyMonster()
    {
        currentHP = MaxHP;
        customeName = data.displayName;
        CalculateExp();

        ivHP = Random.Range(0, MaxIV);
        ivAttack = Random.Range(0, MaxIV);
        ivDefense = Random.Range(0, MaxIV);
        ivSpeed = Random.Range(0, MaxIV);
        ivCritRate = Random.Range(0, MaxIV);
        ivCritMod = Random.Range(0, MaxIV);
        ivDodge = Random.Range(0, MaxIV);
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

        AttackCommandManager.Instance.SetupAttack(this, target, attackIndex, isDirect);

        // Find the matching AttackEntry in the move pool to get the animation name
        AttackData attackData = learnedAttacks[attackIndex].data;
        AttackEntry entry = System.Array.Find(data.movePool, e => e.attack == attackData);

        if (entry != null && !string.IsNullOrEmpty(entry.AnimationTrigger) && anim != null)
        {
            anim.SetTrigger(entry.AnimationTrigger);
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] No animation name set for {attackData.DisplayName}");
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
        effectTarget.currentHP -= damage;
    }

    private void ApplyHeal(Monster attacker, Monster target, Monster effectTarget,
        AttackData attackData, bool isDirect, AttackEffect effect)
    {
        int healAmount = attacker.CalculateDamage(target, attackData, isDirect, effect);
        effectTarget.currentHP -= healAmount;
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

    private int CalculateStat(int baseStat, int iv, bool isHP = false)
    {
        int stat = Mathf.FloorToInt(((baseStat + iv) * level) / 50f) + (isHP ? 10 : 5);

        foreach (var effect in activeEffects)
        {
            if (effect.stat == AttackEnum.AttackBuffType.HP && isHP)
            {
                stat += effect.value;
            }
            else if (!isHP)
            {
                switch (effect.stat)
                {
                    case AttackEnum.AttackBuffType.Attack: stat += effect.value; break;
                    case AttackEnum.AttackBuffType.Defense: stat += effect.value; break;
                    case AttackEnum.AttackBuffType.Speed: stat += effect.value; break;
                    case AttackEnum.AttackBuffType.CritRate: stat += effect.value; break;
                    case AttackEnum.AttackBuffType.CritMod: stat += effect.value; break;
                    case AttackEnum.AttackBuffType.Dodge: stat += effect.value; break;
                }
            }
        }

        return Mathf.Max(0, stat);
    }

    private int CalculateDamage(Monster target, AttackData attack, bool isDirect, AttackEffect attackEffect)
    {
        if (target == null) throw new System.ArgumentNullException(nameof(target));
        if (attack == null) throw new System.ArgumentNullException(nameof(attack));

        if (attackEffect.category == AttackEnum.AttackCategory.heal)
            return -Mathf.FloorToInt(((level * attackEffect.value) / 50f) + 2f);

        float hitChance = Mathf.Clamp(attack.Accuracy - target.Dodge, 5f, 100f);

        if (!attack.GuaranteedHit && Random.value * 100f > hitChance)
        {
            Debug.Log($"{target.customeName} avoided the attack!");
            return 0;
        }

        float levelFactor = (2f * level) / 5f + 2f;
        float attackDefenseRatio = (float)Attack / Mathf.Max(1, target.Defense);
        float baseDamage = ((levelFactor * attackEffect.value * attackDefenseRatio) / 50f) + 2f;

        if (Random.value < (CritRate / 100f)) baseDamage *= CritMod;
        if (!attack.IsDirect && !isDirect) baseDamage *= attack.InDirectHitPercent;

        return Mathf.Max(1, Mathf.FloorToInt(baseDamage * Random.Range(0.85f, 1f)));
    }
    #endregion

    #region Effect Application
    private void CalculateBuff(Monster target, AttackEffect effect)
    {
        if (Random.Range(0f, 100f) > effect.chance)
        {
            Debug.Log($"{target.customeName} avoided the effect!");
            return;
        }

        int appliedValue = effect.isDebuff ? -effect.value : effect.value;
        target.activeEffects.Add(new ActiveEffect(effect.buffType, appliedValue, effect.duration));
        Debug.Log($"{target.customeName} received {(appliedValue >= 0 ? "buff" : "debuff")} " +
                  $"{effect.buffType} for {effect.duration} turns!");
    }

    private void CalculateStatus(Monster target, AttackEffect effect)
    {
        if (effect.statusEffect == null) return;

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
            activeStatuses[i].remainingTurns--;
            if (activeStatuses[i].remainingTurns <= 0)
            {
                Debug.Log($"{customeName} is no longer affected by {activeStatuses[i].data.name}.");
                activeStatuses.RemoveAt(i);
            }
        }
    }
    #endregion
}