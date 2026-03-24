using UnityEngine;

[System.Serializable]
public class AttackEffect
{
    public AttackEnum.AttackCategory category;

    // Used by damage / heal — magnitude / power of the effect
    [ShowIf("category", "heal|damage")]
    public int value;

    // Used by buff / debuff — number of stat stages to apply.
    // Direction (up/down) is determined by isDebuff.
    // E.g. stageCount=2 + isDebuff=false → raise stat by 2 stages.
    [ShowIf("category", "buff")]
    [Tooltip("Number of stat stages to apply (1 = one stage, 6 = maximum boost).\n" +
             "Whether this buffs or debuffs the stat is set by 'isDebuff'.")]
    [Range(1, 6)]
    public int stageCount = 1;
    public bool selfInflicted = false;

    // Used by buff / debuff
    [ShowIf("category", "buff")]
    public AttackEnum.AttackBuffType buffType;

    [ShowIf("category", "buff|status")]
    [Range(0, 100)] public int chance = 100; // Optional

    [ShowIf("category", "buff")]
    public bool isDebuff = false;

    [ShowIf("category", "buff|heal|status")]
    [Range(1, 7)]
    public int duration;

    [ShowIf("category", "heal")]
    public bool isInstantHeal;

    [ShowIf("category", "status")]
    public StatusEffectData statusEffect;
}
