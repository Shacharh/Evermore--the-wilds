using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Monster")]
public class MonsterData : ScriptableObject
{
    public string monsterId;
    public string displayName;
    public int maxHP;

    [Tooltip("Attacks this monster can learn and the level at which it learns them")]
    public AttackEntry[] attacks;
}

[System.Serializable]
public class AttackEntry
{
    public string attackId;  // The ID of the attack
    public int levelLearned; // Level at which the monster learns this attack
}