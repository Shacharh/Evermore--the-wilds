using UnityEngine;

public class AttackEnum : MonoBehaviour
{
    public enum ElementType
    {
        Fire,
        Water,
        Wind,
        Earth,
        Poison,
        Electric,
        Plant,
        Metal
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
        cube,      // Chebyshev square   — max(|dx|,|dy|) ≤ range
        sphere,    // Manhattan diamond  — |dx|+|dy| ≤ range
        cross,     // Cardinal plus (+)  — same row OR same column  (was: old "line")
        line,      // Directional ray    — fires along selected adjacent tile's direction (melee)
        column,    // Perpendicular line — fires ⊥ to direction, centred on selected tile
        cone       // Bowling-pin cone   — expands from selected tile along direction
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
