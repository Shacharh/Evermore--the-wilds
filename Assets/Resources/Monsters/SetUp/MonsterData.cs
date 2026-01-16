using UnityEngine;

[CreateAssetMenu(menuName = "Monster/Create Monster")]
public class MonsterData : ScriptableObject
{
    public string monsterId;
    public string displayName;
    public int maxHP;
    public bool enemyMonster;

    private long exp;
    private float xpMultiplier = 25f;

    [SerializeField, ShowIf("enemyMonster")]
    [Range(1,100)] private int level;


    [Tooltip("Attacks this monster can learn and the level at which it learns them")]
    public AttackEntry[] movePool;


    public long getExp() 
    {
        return this.exp;
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

    public void OnEnable()
    {
        if (!enemyMonster)
        {
            //this will change based on save data
            this.level = 40;
            CaculateExp();
        }
        else CaculateExp();
        
        
        Debug.Log($"EXP: {this.exp}, Level: {this.level}");
    }

}


[System.Serializable]
public class AttackEntry
{
    public AttackData attack;  // The ID of the attack
    public int levelLearned; // Level at which the monster learns this attack
}