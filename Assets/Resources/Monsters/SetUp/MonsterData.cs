using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Monster")]
public class MonsterData : ScriptableObject
{
    public string monsterId;
    public string displayName;
    public int maxHP;

    [Tooltip("Attack IDs this monster can use")]
    public string[] attackIds;
}
