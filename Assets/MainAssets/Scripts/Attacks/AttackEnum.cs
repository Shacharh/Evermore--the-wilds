using UnityEngine;

public class AttackEnum : MonoBehaviour
{
    public enum ElementType
    {
        Fire,
        Water,
        Wind,
        Grass,
        Electric
    }

    public enum StatusEffect
    {
        Burn,
        Freeze,
        Shock,
        Poison,
        Sleep
    }

    public enum AttackCategory
    {
        damage,
        heal,
        buff,
        status
    }

    public enum AttackTarget
    {
        self,
        singleTarget,
        AOE
    }

    public enum AttackTargetShape
    {
        cube,
        sphere,
        line,
        column
    }


    public enum AttackBuffType
    {
        HP,
        Attack,
        Defense,
        Speed,
        CritRate,
        CritMod,
        Dodge
    }
}
