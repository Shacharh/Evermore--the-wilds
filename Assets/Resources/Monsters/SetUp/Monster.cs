using System.Collections.Generic;
using UnityEngine;

public class Monster : MonoBehaviour
{
    [SerializeField] private MonsterData data;
    [SerializeField] private bool enemyMonster;
    [SerializeField] private int maxHP;
    private string customeName;
    
    private int currentHP;
    private long exp;
    private float xpMultiplier = 25f;

    [SerializeField, ShowIf("enemyMonster")]
    [Range(1, 100)] private int level;

    // Runtime learned attacks
    [SerializeField] // for debug add [SerializeField], for prodaction remove it
    private List<MonsterAttack> learnedAttacks = new List<MonsterAttack>();
    private const int MaxAttacks = 2;

    private void Start()
    {
        if (!enemyMonster) loadPlayerMonster();
        else loadEnemyMonster();
        
        Debug.Log($"EXP: {this.exp}, Level: {this.level}");
    }

    private void loadPlayerMonster()
    {
        //this data blobk will be loaded from the save file
        currentHP = maxHP;
        this.level = 40;
        CaculateExp();
        customeName = "custome name";
    }

    private void loadEnemyMonster() 
    {
        currentHP = maxHP;
        customeName = data.displayName;
        CaculateExp();
    }

    public void LearnAttack(AttackData attack)
    {
        MonsterAttack monsterAttack = new MonsterAttack(attack);
        if (attack == null || learnedAttacks.Contains(monsterAttack)) return;

        if (learnedAttacks.Count >= MaxAttacks)
        {
            Debug.Log($"{gameObject.name} cannot learn more than {MaxAttacks} attacks!");
            return;
        }

        learnedAttacks.Add(monsterAttack);

        Debug.Log($"{gameObject.name} learned attack: {attack.displayName}");
    }

    public void ForgetAttack(AttackData attack)
    {
        learnedAttacks.Remove(new MonsterAttack(attack));
    }

    public IReadOnlyList<MonsterAttack> GetAttacks()
    {
        return learnedAttacks;
    }

    public void UseAttack(int index, int usePP)
    {
        if (index < 0 || index >= learnedAttacks.Count)
        {
            Debug.LogWarning("Invalid attack index!");
            return;
        }

        if (usePP <= 0)
        {
            Debug.LogWarning("usePP must be greater than 0!");
            return;
        }

        var monsterAttack = learnedAttacks[index];
        Debug.Log($"{data.displayName} uses {monsterAttack.data.displayName}");
        monsterAttack.usePP();
        this.currentHP -= monsterAttack.data.power;
    }

    public void addExp(long exp)
    {
        if (exp < 0)
        {
            throw new System.ArgumentException("Experience points must be positive.");
        }
        this.exp += exp;
    }

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
    }

}
