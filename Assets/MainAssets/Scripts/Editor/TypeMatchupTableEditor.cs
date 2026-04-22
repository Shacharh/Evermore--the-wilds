using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TypeMatchupTable))]
public class TypeMatchupTableEditor : Editor
{
    private Vector2 scrollPos;

    public override void OnInspectorGUI()
    {
        TypeMatchupTable matchupTable = (TypeMatchupTable)target;
        matchupTable.EnsureSize();

        string[] typeNames = Enum.GetNames(typeof(AttackEnum.ElementType));
        int typeCount = typeNames.Length;

        // ── Header ────────────────────────────────────────────────────────────
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Type Matchup Table", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Open Type Editor", GUILayout.Width(130f)))
            TypeMatchupTableWindow.Open(matchupTable);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(2f);

        EditorGUILayout.HelpBox(
            "Row = Defender type  (the monster being hit)\n" +
            "Column = Attack type  (the element of the incoming move)\n\n" +
            "Read as: \"When a [Row] monster is hit by a [Column] attack...\"",
            MessageType.Info);

        GUILayout.Space(4f);

        // ── Grid (scrollable) ─────────────────────────────────────────────────
        float rowHeight   = 20f;
        float totalHeight = rowHeight * (typeCount + 1) + 8f;

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos,
            GUILayout.Height(Mathf.Min(totalHeight + 20f, 520f)),
            GUILayout.ExpandWidth(true));

        TypeMatchupTableGUI.DrawGrid(matchupTable);

        EditorGUILayout.EndScrollView();

        GUILayout.Space(4f);

        // ── Legend ────────────────────────────────────────────────────────────
        TypeMatchupTableGUI.DrawLegend();
    }
}
