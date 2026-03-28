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

    [Header("Action Points")]
    [Tooltip("AP cost for this monster to move one tile.")]
    public int moveCost = 1;

    [Header("Animation")]
    [AnimatorBool] public string MovementAnimationBoolean;
    [AnimatorTrigger] public string DoamageAnimationTrigger;
    [AnimatorTrigger] public string DogeAnimationTrigger;
    [AnimatorTrigger] public string DeathAnimationTrigger;

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
        moveCost = Mathf.Max(1, moveCost);
    }
}

[System.Serializable]
public class AttackEntry
{
    public AttackData attack;
    [Range(1, 100)] public int levelLearned;

    /// <summary>
    /// The Animator Trigger to fire when this attack is executed.
    /// Shows a dropdown of every Trigger parameter found in the project's
    /// AnimatorControllers -- no typos possible.
    /// </summary>
    [AnimatorTrigger]
    public string AnimationTrigger;
}