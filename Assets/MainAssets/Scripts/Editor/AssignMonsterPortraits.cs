using UnityEditor;
using UnityEngine;

/// <summary>
/// One-click tool that matches each MonsterData asset to its portrait sprite
/// by comparing the monster's displayName to sprites named "{Name}_ICON" in
/// Assets/MainAssets/Materials/2d/Unit_Markers/Units/.
///
/// Run via:  Tools → Evermore → Assign Monster Portraits
/// </summary>
public static class AssignMonsterPortraits
{
    private const string IconFolder = "Assets/MainAssets/Materials/2d/Unit_Markers/Units";

    [MenuItem("Tools/Evermore/Assign Monster Portraits")]
    public static void Run()
    {
        int assigned = 0;
        int skipped  = 0;

        // Find every MonsterData asset in the project
        string[] guids = AssetDatabase.FindAssets("t:MonsterData");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var data    = AssetDatabase.LoadAssetAtPath<MonsterData>(path);
            if (data == null) continue;

            // Build expected icon filename: "{displayName}_ICON.png"
            string name        = string.IsNullOrEmpty(data.displayName) ? data.name : data.displayName;
            string iconPath    = $"{IconFolder}/{name}_ICON.png";
            var    sprite      = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);

            if (sprite == null)
            {
                Debug.LogWarning($"[AssignMonsterPortraits] No icon found for '{name}' at {iconPath}");
                skipped++;
                continue;
            }

            if (data.portrait == sprite)
            {
                skipped++;
                continue;
            }

            data.portrait = sprite;
            EditorUtility.SetDirty(data);
            assigned++;
            Debug.Log($"[AssignMonsterPortraits] {name} ← {sprite.name}");
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Assign Monster Portraits",
            $"Done.\nAssigned: {assigned}\nSkipped / not found: {skipped}", "OK");
    }
}
