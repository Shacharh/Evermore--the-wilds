using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Shared GUI drawing code used by both the inline inspector (TypeMatchupTableEditor)
/// and the standalone window (TypeMatchupTableWindow).
/// </summary>
public static class TypeMatchupTableGUI
{
    // Colors used for GUI.backgroundColor — bright so they visibly tint the control.
    // Normal uses Color.white (Unity's default = no tint).
    private static readonly Color BgSuperEffective = new Color(0.3f, 1.0f, 0.3f, 1f);
    private static readonly Color BgEffective      = new Color(0.7f, 1.0f, 0.5f, 1f);
    private static readonly Color BgNormal         = Color.white;
    private static readonly Color BgWeak           = new Color(1.0f, 0.7f, 0.3f, 1f);
    private static readonly Color BgSuperWeak      = new Color(1.0f, 0.35f, 0.35f, 1f);

    private const float LabelWidth = 90f;
    private const float CellWidth  = 108f;
    private const float RowHeight  = 20f;

    /// <summary>Draws the full NxN matchup grid. Must be called inside a scroll view if needed.</summary>
    public static void DrawGrid(TypeMatchupTable matchupTable)
    {
        string[] typeNames = Enum.GetNames(typeof(AttackEnum.ElementType));
        int typeCount = typeNames.Length;

        // ── Header row ────────────────────────────────────────────────────────
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Def \\ Atk", EditorStyles.miniLabel, GUILayout.Width(LabelWidth));
        foreach (string typeName in typeNames)
            GUILayout.Label(typeName, EditorStyles.miniButtonMid, GUILayout.Width(CellWidth));
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(2f);

        // ── Data rows ─────────────────────────────────────────────────────────
        for (int row = 0; row < typeCount; row++)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(typeNames[row], EditorStyles.boldLabel, GUILayout.Width(LabelWidth));

            for (int col = 0; col < typeCount; col++)
            {
                TypeEffectiveness current = matchupTable.table[row].effectiveness[col];

                Rect cellRect = GUILayoutUtility.GetRect(CellWidth, RowHeight,
                    GUILayout.Width(CellWidth), GUILayout.Height(RowHeight));

                Color prevBg = GUI.backgroundColor;
                GUI.backgroundColor = GetBgColor(current);

                TypeEffectiveness newVal = (TypeEffectiveness)EditorGUI.EnumPopup(cellRect, current);

                GUI.backgroundColor = prevBg;

                if (newVal != current)
                {
                    Undo.RecordObject(matchupTable, "Change Type Matchup");
                    matchupTable.table[row].effectiveness[col] = newVal;
                    EditorUtility.SetDirty(matchupTable);
                }
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    /// <summary>Draws the color legend below the grid.</summary>
    public static void DrawLegend()
    {
        EditorGUILayout.LabelField("Legend", EditorStyles.boldLabel);
        DrawLegendRow(BgSuperEffective, "Super Effective  x2.0");
        DrawLegendRow(BgEffective,      "Effective        x1.5");
        DrawLegendRow(BgNormal,         "Normal           x1.0");
        DrawLegendRow(BgWeak,           "Weak             x0.5");
        DrawLegendRow(BgSuperWeak,      "Super Weak       x0.25");
    }

    private static Color GetBgColor(TypeEffectiveness e)
    {
        return e switch
        {
            TypeEffectiveness.SuperEffective => BgSuperEffective,
            TypeEffectiveness.Effective      => BgEffective,
            TypeEffectiveness.Normal         => BgNormal,
            TypeEffectiveness.Weak           => BgWeak,
            TypeEffectiveness.SuperWeak      => BgSuperWeak,
            _                                => BgNormal
        };
    }

    private static void DrawLegendRow(Color color, string label)
    {
        EditorGUILayout.BeginHorizontal();
        Rect swatchRect = GUILayoutUtility.GetRect(16f, 14f, GUILayout.Width(16f));
        EditorGUI.DrawRect(swatchRect, color);
        EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }
}
