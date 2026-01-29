using System.Collections.Generic;
using UnityEngine;

public class Monster : MonoBehaviour
{
    #region properties
    [SerializeField] private MonsterData data;
    [SerializeField] private bool enemyMonster;
    [SerializeField, ShowIf("enemyMonster")]
    [Range(1, 100)] private int level;

    // Runtime learned attacks
    [SerializeField] // for debug add [SerializeField], for prodaction remove it
    private List<MonsterAttack> learnedAttacks = new List<MonsterAttack>();
    private const int MaxAttacks = 2;


    private string customeName;
    private int currentHP;
    private long exp;
    private long expByLevel;
    private float xpMultiplier = 25f;
    #endregion

    #region ivs
    private int ivHP;
    private int ivAttack;
    private int ivDefense;
    private int ivSpeed;
    private int ivCritRate;
    private int ivCritMod;
    private int ivDodge;
    #endregion

    #region Calculated Stats
    private const int MaxIV = 24;
    public int MaxHP => CalculateStat(data.baseHP, ivHP, true);
    public int Attack => CalculateStat(data.baseAttack, ivAttack);
    public int Defense => CalculateStat(data.baseDefense, ivDefense);
    public int Speed => CalculateStat(data.baseSpeed, ivSpeed);
    public int CritRate => CalculateStat(data.baseCritRate, ivCritRate);
    public int CritMod => CalculateStat(data.baseCritMod, ivCritMod);
    public int Dodge => CalculateStat(data.baseDodge, ivDodge);
    #endregion

    #region Unity Hookes
    private void Start()
    {
        if (!enemyMonster) loadPlayerMonster();
        else loadEnemyMonster();

        Debug.Log($"EXP: {this.exp}, Level: {this.level}");
    }
    #endregion

    #region loaders
    private void loadPlayerMonster()
    {
        //this data blobk will be loaded from the save file
        currentHP = MaxHP;
        this.level = 40;
        CaculateExp();
        customeName = "custome name";

        //load ivs from save file
        ivHP = 10;
        ivAttack = 12;
        ivDefense = 8;
        ivSpeed = 14;
        ivCritRate = 5;
        ivCritMod = 3;
        ivDodge = 7;
    }

    private void loadEnemyMonster() 
    {
        currentHP = MaxHP;
        customeName = data.displayName;
        CaculateExp();

        ivHP = Random.Range(0, MaxIV);
        ivAttack = Random.Range(0, MaxIV);
        ivDefense = Random.Range(0, MaxIV);
        ivSpeed = Random.Range(0, MaxIV);
        ivCritRate = Random.Range(0, MaxIV);
        ivCritMod = Random.Range(0, MaxIV);
        ivDodge = Random.Range(0, MaxIV);
    }
    #endregion

    #region Attack Manupiolation
    public void LearnAttack(AttackData attack)
    {
        if (attack == null) throw new System.ArgumentException("Attack canot be null at Monster.LearnAttack");
       
        MonsterAttack monsterAttack = new MonsterAttack(attack);
        if (learnedAttacks.Contains(monsterAttack)) throw new System.ArgumentException("Cannot Learn The Same Attack Twice at Monster.LearnAttack");

        if (learnedAttacks.Count >= MaxAttacks)
        {
            throw new System.ArgumentException($"{gameObject.name} cannot learn more than {MaxAttacks} attacks at Monster.LearnAttack");
        }

        learnedAttacks.Add(monsterAttack);

        Debug.Log($"{gameObject.name} learned attack: {attack.displayName}");
    }

    public void ForgetAttack(AttackData attack)
    {
        if (attack == null) throw new System.ArgumentException("Attack canot be null at Monster.ForgetAttack"); ;
        learnedAttacks.Remove(new MonsterAttack(attack));
    }

    public IReadOnlyList<MonsterAttack> GetAttacks()
    {
        return learnedAttacks;
    }

    public void UseAttack(int index, Monster target, bool isDirect)
    {
        if (index < 0 || index >= learnedAttacks.Count)
        {
            throw new System.ArgumentNullException("Invalid attack index!");
        }

        var attack = learnedAttacks[index];
        Debug.Log($"{data.displayName} uses {attack.data.displayName}");
        attack.usePP();

        if (
            (attack.data.category == AttackEnum.AttackCategory.buff) ||
            (attack.data.category == AttackEnum.AttackCategory.debuff)
        ) 
        {
            CalculateBuff(target, attack.data); ///need to add miss logic here
        }
        else target.currentHP -= CalculateDamage(target, attack.data, isDirect);
    }
    #endregion
    
    #region calculations
    public void CalculateLevel()
    {
        // Formula to calculate the level from total XP
        // Use Math.Floor for rounding down to the current level
        this.level = (int)Mathf.Floor((this.xpMultiplier + Mathf.Sqrt(Mathf.Pow(this.xpMultiplier, 2) + 4 * this.xpMultiplier * exp)) / (2 * this.xpMultiplier));
    }

    public void CaculateExp()
    {
        long l = this.level;
        long multiplier = (long)this.xpMultiplier;

        // Use the formula: exp = xpMultiplier * ((2*level - 1)^2 - 1) / 4
        long temp = (2 * l - 1);
        this.exp = multiplier * (temp * temp - 1) / 4;
        UpdateExpByLevel();
    }

    private int CalculateStat(int baseStat, int iv, bool isHP = false)
    {
        return Mathf.FloorToInt(((baseStat + iv) * level) / 50f) + ((isHP) ? 10 : 5);
    }

    private int CalculateDamage(Monster target, AttackData attack, bool isDirect)
    {
        if (target == null) throw new System.ArgumentNullException(nameof(target));
        if (attack == null) throw new System.ArgumentNullException(nameof(attack));


        //check for heal attack
        if (attack.category == AttackEnum.AttackCategory.heal)
        {
            return -Mathf.FloorToInt(((level * attack.power) / 50f) + 2f);
        }

        // --- Hit chance check: accuracy vs dodge ---
        float hitChance = attack.accuracy - target.Dodge; // simple formula
        hitChance = Mathf.Clamp(hitChance, 5f, 100f);   // ensure at least 5% chance to hit
        if ((!attack.guaranteedHit) && (Random.value * 100f > hitChance))
        {
            Debug.Log($"{target.customeName} avoided the attack!");
            return 0; // Attack missed
        }

        float levelFactor = (2f * level) / 5f + 2f;
        float attackDefenseRatio = (float)Attack / Mathf.Max(1, target.Defense);

        float baseDamage = ((levelFactor * attack.power * attackDefenseRatio) / 50f) + 2f;

        // Critical hit
        bool isCrit = Random.value < (CritRate / 100f);
        if (isCrit)
        {
            baseDamage *= CritMod;
        }

        // Indirect hit modifier
        if ((!attack.isDirect)&&(!isDirect))
        {
            baseDamage *= attack.inDirectHitPrecent;
        }

        // Random variance
        float randomModifier = Random.Range(0.85f, 1f);

        int finalDamage = Mathf.FloorToInt(baseDamage * randomModifier);

        return Mathf.Max(1, finalDamage);
    }

    private void CalculateBuff(Monster target, AttackData attack) 
    {
        ///need to add miss logic here
    }
    #endregion

    #region general
    public void addExp(long exp)
    {
        if (exp < 0)
        {
            throw new System.ArgumentException("Experience points must be positive.");
        }
        this.exp += exp;
    }

    private void LevelUp(long exp)
    {
        addExp(exp);
        CalculateLevel();
        int oldMaxHP = MaxHP;

        currentHP += (MaxHP - oldMaxHP);
    }

    public void UpdateExpByLevel()
    {
        long l = this.level;
        long multiplier = (long)this.xpMultiplier;

        // Formula: exp required to reach the current level
        long temp = (2 * l - 1);
        this.expByLevel = multiplier * (temp * temp - 1) / 4;
    }

    #endregion

}
