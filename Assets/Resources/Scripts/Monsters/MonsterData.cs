using UnityEngine;

[CreateAssetMenu(menuName = "Monster/Create Monster")]
public class MonsterData : ScriptableObject
{
    [Header("Monster General")]
    public string monsterId;
    public string displayName;
    public string elementType;

    [Header("Base Stats")]
    public int baseHP;
    public int baseAttack;
    public int baseDefense;
    public int baseSpeed;
    [Range(0, 100)] public int baseCritRate;
    public int baseCritMod;
    [Range(0, 100)] public int baseDodge;

    [Tooltip("Attacks this monster can learn and the level at which it learns them")]
    public AttackEntry[] movePool;

    private void OnValidate()
    {
        baseHP = Mathf.Max(0, baseHP);
        baseAttack = Mathf.Max(0, baseAttack);
        baseDefense = Mathf.Max(0, baseDefense);
        baseSpeed = Mathf.Max(0, baseSpeed);
        baseCritRate = Mathf.Clamp(baseCritRate, 0, 100);
        baseCritMod = Mathf.Max(0, baseCritMod);
        baseDodge = Mathf.Clamp(baseDodge, 0, 100);
    }

}


[System.Serializable]
public class AttackEntry
{
    public AttackData attack;  // The ID of the attack
    [Range(1,100)] public int levelLearned; // Level at which the monster learns this attack
}