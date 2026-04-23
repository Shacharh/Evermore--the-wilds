using System;
using UnityEngine;

/// <summary>
/// How effective an attack type is against a defender type.
/// Displayed as a friendly dropdown in the inspector; converted to a float multiplier at runtime.
/// </summary>
public enum TypeEffectiveness
{
    SuperWeak,       // x0.25
    Weak,            // x0.5
    Normal,          // x1.0
    Effective,       // x1.5
    SuperEffective   // x2.0
}

public static class TypeEffectivenessHelper
{
    public static float ToMultiplier(TypeEffectiveness e)
    {
        return e switch
        {
            TypeEffectiveness.SuperWeak       => 0.25f,
            TypeEffectiveness.Weak            => 0.5f,
            TypeEffectiveness.Normal          => 1f,
            TypeEffectiveness.Effective       => 1.5f,
            TypeEffectiveness.SuperEffective  => 2f,
            _                                 => 1f
        };
    }
}

/// <summary>
/// ScriptableObject that stores the full NxN type matchup table.
/// Row = defender type (the monster being hit).
/// Column = attack type (the element of the incoming attack).
///
/// Create via: Assets > Create > Evermore > Type Matchup Table
/// Assign to GameInitializer in the scene inspector.
/// </summary>
[CreateAssetMenu(menuName = "Evermore/Type Matchup Table")]
public class TypeMatchupTable : ScriptableObject
{
    [HideInInspector]
    public TypeMatchupRow[] table;

    /// <summary>
    /// Returns the damage multiplier for an attack of <paramref name="attackType"/>
    /// hitting a monster of <paramref name="defenderType"/>.
    /// Falls back to 1.0 (neutral) if the table is missing or mis-sized.
    /// </summary>
    public float GetMultiplier(AttackEnum.ElementType defenderType, AttackEnum.ElementType attackType)
    {
        int row = (int)defenderType;
        int col = (int)attackType;

        if (table == null || row >= table.Length || table[row] == null)
            return 1f;

        if (col >= table[row].effectiveness.Length)
            return 1f;

        return TypeEffectivenessHelper.ToMultiplier(table[row].effectiveness[col]);
    }

    /// <summary>
    /// Resizes the table to match the current number of ElementType values.
    /// Called automatically by OnValidate and by the custom editor.
    /// Existing values are preserved; new cells default to Normal.
    /// </summary>
    public void EnsureSize()
    {
        int typeCount = Enum.GetValues(typeof(AttackEnum.ElementType)).Length;

        if (table == null || table.Length != typeCount)
        {
            TypeMatchupRow[] resized = new TypeMatchupRow[typeCount];
            for (int i = 0; i < typeCount; i++)
                resized[i] = (table != null && i < table.Length && table[i] != null)
                    ? table[i]
                    : new TypeMatchupRow(typeCount);
            table = resized;
        }

        for (int i = 0; i < typeCount; i++)
        {
            if (table[i] == null)
                table[i] = new TypeMatchupRow(typeCount);

            if (table[i].effectiveness == null || table[i].effectiveness.Length != typeCount)
            {
                TypeEffectiveness[] resized = new TypeEffectiveness[typeCount];
                for (int j = 0; j < typeCount; j++)
                    resized[j] = (table[i].effectiveness != null && j < table[i].effectiveness.Length)
                        ? table[i].effectiveness[j]
                        : TypeEffectiveness.Normal;
                table[i].effectiveness = resized;
            }
        }
    }

    private void OnValidate() => EnsureSize();
}

[Serializable]
public class TypeMatchupRow
{
    public TypeEffectiveness[] effectiveness;

    public TypeMatchupRow() { }

    public TypeMatchupRow(int size)
    {
        effectiveness = new TypeEffectiveness[size];
        for (int i = 0; i < size; i++)
            effectiveness[i] = TypeEffectiveness.Normal;
    }
}
