using UnityEngine;

[CreateAssetMenu(menuName = "Monster/Create Monster")]
public class MonsterData : ScriptableObject
{
    public string monsterId;
    public string displayName;

    [Tooltip("Attacks this monster can learn and the level at which it learns them")]
    public AttackEntry[] movePool;

}


[System.Serializable]
public class AttackEntry
{
    public AttackData attack;  // The ID of the attack
    [Range(1,100)] public int levelLearned; // Level at which the monster learns this attack
}