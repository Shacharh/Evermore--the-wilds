using UnityEngine;

[System.Serializable]
public class AttackEffect
{
    public AttackEnum.AttackCategory category;

    // Used by damage / heal
    [ShowIf("category", "buff|heal|damage")]
    public int value;
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
