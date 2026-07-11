using UnityEngine;

[CreateAssetMenu(menuName = "Monster/Create Monster")]
public class MonsterData : ScriptableObject
{
    [Header("Monster General")]
    public string monsterId;
    public string displayName;
    public AttackEnum.ElementType elementType;
    [Tooltip("2D portrait sprite shown in the battle roster panel (square, transparent background recommended).")]
    public Sprite portrait;
    [Tooltip("Flying monsters ignore ground obstructions and can pass over them.\n" +
             "They still cannot LAND on an obstructed tile.")]
    public bool isFlying;

    [Header("Base Stats")]
    public int baseHP;
    public int baseAttack;
    public int baseDefense;
    public int baseSpeed;
    [Range(0, 100)] public int baseCritRate;
    public int baseCritMod;
    [Range(0, 100)] public int baseDodge;

    [Header("Animation")]
     public string MovementAnimationBoolean;
     public string DoamageAnimationTrigger;
     public string DogeAnimationTrigger;
     public string DeathAnimationTrigger;

    [Header("AI")]
    [Tooltip("Optional personality that biases this monster's action scoring.\n" +
             "Leave empty to use only the global AIGameStateScorePoints baseline.\n" +
             "Create via: Assets → Create → Evermore → AI → Monster Personality")]
    public MonsterPersonality personality;

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
    public AttackData attack;
    [Range(1, 100)] public int levelLearned;

    /// <summary>
    /// The Animator Trigger to fire when this attack is executed.
    /// Shows a dropdown of every Trigger parameter found in the project's
    /// AnimatorControllers -- no typos possible.
    /// </summary>
    
    public string AnimationTrigger;

    [Tooltip("Relative path to the VFX spawn point child on the monster prefab.\n" +
             "Set via the picker in the Inspector — drag the monster root and select a child.\n" +
             "Leave empty to spawn at the monster's root position.")]
    public string vfxSpawnPointPath;
    public Vector3 vfxSpawnOffset;
}