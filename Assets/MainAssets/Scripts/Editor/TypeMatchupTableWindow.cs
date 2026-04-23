using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Standalone editor window for editing the TypeMatchupTable.
/// Open via the "Open Type Editor" button on the TypeMatchupTable asset inspector.
/// </summary>
public class TypeMatchupTableWindow : EditorWindow
{
    private TypeMatchupTable matchupTable;
    private Vector2 scrollPos;

    public static void Open(TypeMatchupTable table)
    {
        TypeMatchupTableWindow window = GetWindow<TypeMatchupTableWindow>("Type Matchup Editor");
        window.matchupTable = table;
        window.minSize = new Vector2(500f, 400f);
        window.Show();
    }

    private void OnGUI()
    {
        if (matchupTable == null)
        {
            EditorGUILayout.HelpBox("No TypeMatchupTable assigned. Close and reopen from the asset inspector.", MessageType.Warning);
            return;
        }

        matchupTable.EnsureSize();

        // ── Toolbar ───────────────────────────────────────────────────────────
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label($"Editing: {matchupTable.name}", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(60f)))
        {
            EditorUtility.SetDirty(matchupTable);
            AssetDatabase.SaveAssets();
            Debug.Log($"[TypeMatchupTable] Saved '{matchupTable.name}'");
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4f);

        EditorGUILayout.HelpBox(
            "Row = Defender type  (the monster being hit)\n" +
            "Column = Attack type  (the element of the incoming move)\n" +
            "Read as: \"When a [Row] monster is hit by a [Column] attack...\"",
            MessageType.Info);

        GUILayout.Space(6f);

        // ── Grid ──────────────────────────────────────────────────────────────
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        TypeMatchupTableGUI.DrawGrid(matchupTable);
        EditorGUILayout.EndScrollView();

        GUILayout.Space(6f);

        // ── Legend ────────────────────────────────────────────────────────────
        TypeMatchupTableGUI.DrawLegend();
    }

    // Keep the window repainting while open so changes made in the inspector
    // are reflected here immediately (and vice versa).
    private void OnInspectorUpdate() => Repaint();
}
