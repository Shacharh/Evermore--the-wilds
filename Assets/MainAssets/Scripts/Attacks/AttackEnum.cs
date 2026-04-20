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

    /// <summary>
    /// Which team the attack can hit.
    /// enemy  — targets the opposing team only.
    /// ally   — targets monsters on the same team (including self).
    /// self   — instantly targets only the monster using the attack; no selection UI.
    /// </summary>
    public enum AttackTargetTeam
    {
        enemy,
        ally,
        self
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
