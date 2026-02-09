using System.Collections.Generic;
using UnityEngine;

public class Monster : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField] private MonsterData data;
    [SerializeField] private bool enemyMonster;
    [SerializeField, ShowIf("enemyMonster"), Range(1, 100)] private int level;

    [SerializeField] // Remove in production - for debugging only
    private List<MonsterAttack> learnedAttacks = new List<MonsterAttack>();
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
        // TODO: Load this data from save file
        currentHP = MaxHP;
        level = 40;
        CalculateExp();
        customeName = "custome name";

        // TODO: Load IVs from save file
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

        // Generate random IVs for enemy
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
    /// <summary>
    /// Teaches this monster a new attack
    /// </summary>
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

    /// <summary>
    /// Removes an attack from this monster's learned attacks
    /// </summary>
    public void ForgetAttack(AttackData attack)
    {
        if (attack == null)
            throw new System.ArgumentException("Attack cannot be null");

        learnedAttacks.RemoveAll(ma => ma.data == attack);
    }

    /// <summary>
    /// Returns a read-only list of this monster's learned attacks
    /// </summary>
    public IReadOnlyList<MonsterAttack> GetAttacks()
    {
        return learnedAttacks;
    }
    #endregion

    #region Attack Execution
    /// <summary>
    /// Initiates an attack - sets up the context and triggers the animation.
    /// The animation events will then call TriggerAttackEffect(effectIndex).
    /// </summary>
    public void ExecuteAttack(Monster target, int attackIndex, bool isDirect)
    {
        if (attackIndex < 0 || attackIndex >= learnedAttacks.Count)
            throw new System.ArgumentException("Invalid attack index");

        // Store the attack context in the manager
        AttackCommandManager.Instance.SetupAttack(this, target, attackIndex, isDirect);

        Debug.Log($"{customeName} uses {learnedAttacks[attackIndex].data.DisplayName}");

        // TODO: Trigger your animation here
        // Example: GetComponent<Animator>().SetTrigger("Attack");
        // or: PlayAttackAnimation(learnedAttacks[attackIndex].data.animationName);
    }

    /// <summary>
    /// Bridge method for animation events - forwards to AttackCommandManager.
    /// Animation events can only call methods on the GameObject with the Animator.
    /// Called from animation events with the effect index (0, 1, 2, etc.)
    /// </summary>
    public void TriggerAttackEffect(int effectIndex)
    {
        AttackCommandManager.Instance.TriggerAttackEffect(effectIndex);
    }

    /// <summary>
    /// Applies a single effect from an attack. Called by AttackCommandManager.
    /// </summary>
    public void UseAttackEffect(
        Monster attacker,
        Monster target,
        int attackIndex,
        int effectIndex,
        bool isDirect)
    {
        // Validate parameters
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

        // PP is consumed once (usually on first effect)
        if (effectIndex == 0)
            monsterAttack.UsePP();

        // Determine the actual target (might be self-inflicted)
        Monster effectTarget = effect.selfInflicted ? attacker : target;

        // Apply the effect based on category
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
        effectTarget.currentHP -= healAmount; // heal is negative damage
    }
    #endregion

    #region Stat Calculations
    /// <summary>
    /// Calculates the current level based on total experience
    /// </summary>
    public void CalculateLevel()
    {
        level = (int)Mathf.Floor((XpMultiplier + Mathf.Sqrt(Mathf.Pow(XpMultiplier, 2) + 4 * XpMultiplier * exp)) / (2 * XpMultiplier));
    }

    /// <summary>
    /// Calculates total experience for current level
    /// </summary>
    public void CalculateExp()
    {
        long l = level;
        long multiplier = (long)XpMultiplier;
        long temp = (2 * l - 1);

        exp = multiplier * (temp * temp - 1) / 4;
        UpdateExpByLevel();
    }

    /// <summary>
    /// Updates experience required for the current level
    /// </summary>
    public void UpdateExpByLevel()
    {
        long l = level;
        long multiplier = (long)XpMultiplier;
        long temp = (2 * l - 1);

        expByLevel = multiplier * (temp * temp - 1) / 4;
    }

    /// <summary>
    /// Calculates a stat value including level scaling, IVs, and active buffs/debuffs
    /// </summary>
    private int CalculateStat(int baseStat, int iv, bool isHP = false)
    {
        // Base calculation with level and IV
        int stat = Mathf.FloorToInt(((baseStat + iv) * level) / 50f) + (isHP ? 10 : 5);

        // Apply active buffs/debuffs
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
                    case AttackEnum.AttackBuffType.Attack:
                        stat += effect.value;
                        break;
                    case AttackEnum.AttackBuffType.Defense:
                        stat += effect.value;
                        break;
                    case AttackEnum.AttackBuffType.Speed:
                        stat += effect.value;
                        break;
                    case AttackEnum.AttackBuffType.CritRate:
                        stat += effect.value;
                        break;
                    case AttackEnum.AttackBuffType.CritMod:
                        stat += effect.value;
                        break;
                    case AttackEnum.AttackBuffType.Dodge:
                        stat += effect.value;
                        break;
                }
            }
        }

        return Mathf.Max(0, stat); // Stat cannot go below 0
    }

    /// <summary>
    /// Calculates damage/healing value for an attack effect
    /// </summary>
    private int CalculateDamage(Monster target, AttackData attack, bool isDirect, AttackEffect attackEffect)
    {
        if (target == null)
            throw new System.ArgumentNullException(nameof(target));
        if (attack == null)
            throw new System.ArgumentNullException(nameof(attack));

        // Handle healing
        if (attackEffect.category == AttackEnum.AttackCategory.heal)
        {
            return -Mathf.FloorToInt(((level * attackEffect.value) / 50f) + 2f);
        }

        // Hit chance calculation
        float hitChance = attack.Accuracy - target.Dodge;
        hitChance = Mathf.Clamp(hitChance, 5f, 100f); // Minimum 5% hit chance

        if (!attack.GuaranteedHit && Random.value * 100f > hitChance)
        {
            Debug.Log($"{target.customeName} avoided the attack!");
            return 0; // Attack missed
        }

        // Damage formula
        float levelFactor = (2f * level) / 5f + 2f;
        float attackDefenseRatio = (float)Attack / Mathf.Max(1, target.Defense);
        float baseDamage = ((levelFactor * attackEffect.value * attackDefenseRatio) / 50f) + 2f;

        // Critical hit
        bool isCrit = Random.value < (CritRate / 100f);
        if (isCrit)
        {
            baseDamage *= CritMod;
        }

        // Indirect hit modifier
        if (!attack.IsDirect && !isDirect)
        {
            baseDamage *= attack.InDirectHitPercent;
        }

        // Random variance
        float randomModifier = Random.Range(0.85f, 1f);
        int finalDamage = Mathf.FloorToInt(baseDamage * randomModifier);

        return Mathf.Max(1, finalDamage); // Minimum 1 damage
    }
    #endregion

    #region Effect Application
    /// <summary>
    /// Applies a buff or debuff effect to the target
    /// </summary>
    private void CalculateBuff(Monster target, AttackEffect effect)
    {
        // Chance check
        if (Random.Range(0f, 100f) > effect.chance)
        {
            Debug.Log($"{target.customeName} avoided the effect!");
            return;
        }

        int appliedValue = effect.value;
        if (effect.isDebuff)
            appliedValue = -appliedValue;

        target.activeEffects.Add(new ActiveEffect(effect.buffType, appliedValue, effect.duration));

        Debug.Log($"{target.customeName} received {(appliedValue >= 0 ? "buff" : "debuff")} " +
                  $"{effect.buffType} for {effect.duration} turns!");
    }

    /// <summary>
    /// Applies a status effect to the target
    /// </summary>
    private void CalculateStatus(Monster target, AttackEffect effect)
    {
        if (effect.statusEffect == null)
            return;

        // Chance check
        if (Random.Range(0f, 100f) > effect.chance)
        {
            Debug.Log($"{target.customeName} resisted the status!");
            return;
        }

        // Check for existing status
        for (int i = 0; i < target.activeStatuses.Count; i++)
        {
            if (target.activeStatuses[i].data == effect.statusEffect)
            {
                if (!effect.statusEffect.Stacks)
                {
                    // Refresh duration
                    target.activeStatuses[i].remainingTurns = effect.duration;
                    Debug.Log($"{target.customeName}'s {effect.statusEffect.name} duration " +
                              $"refreshed to {effect.duration} turns!");
                }
                else
                {
                    // Stack duration
                    target.activeStatuses[i].remainingTurns += effect.duration;
                    Debug.Log($"{target.customeName}'s {effect.statusEffect.name} duration " +
                              $"extended by {effect.duration} turns!");
                }
                return;
            }
        }

        // Add new status
        target.activeStatuses.Add(new ActiveStatus(effect.statusEffect, effect.duration));
        Debug.Log($"{target.customeName} is affected by {effect.statusEffect.name}!");
    }
    #endregion

    #region Experience & Leveling
    /// <summary>
    /// Adds experience points to this monster
    /// </summary>
    public void AddExp(long exp)
    {
        if (exp < 0)
            throw new System.ArgumentException("Experience points must be positive");

        this.exp += exp;
    }

    /// <summary>
    /// Levels up the monster and restores HP proportionally
    /// </summary>
    private void LevelUp(long exp)
    {
        AddExp(exp);
        CalculateLevel();

        int oldMaxHP = MaxHP;
        currentHP += (MaxHP - oldMaxHP);
    }
    #endregion

    #region Turn Management
    /// <summary>
    /// Called at the start of this monster's turn
    /// </summary>
    public void OnStartTurn()
    {
        TickEffects();
        TickStatuses();
    }

    /// <summary>
    /// Called at the end of this monster's turn
    /// </summary>
    public void OnEndTurn()
    {
        // Add end-of-turn logic here if needed
    }

    /// <summary>
    /// Decrements duration of all active buffs/debuffs and removes expired ones
    /// </summary>
    private void TickEffects()
    {
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            activeEffects[i].remainingTurns--;

            if (activeEffects[i].remainingTurns <= 0)
                activeEffects.RemoveAt(i);
        }
    }

    /// <summary>
    /// Decrements duration of all active status effects and removes expired ones
    /// </summary>
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