using UnityEditor;
using UnityEngine;
using System.Linq;

public class AttackDataPostprocessor : AssetPostprocessor
{
    static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        // If ANY asset was deleted -> refresh
        if (deletedAssets.Length > 0)
        {
            EditorApplication.delayCall += RefreshAllDatabases;
            return;
        }

        // Only check types for assets that still exist
        bool attackDataAddedOrMoved =
            importedAssets.Any(IsAttackData) ||
            movedAssets.Any(IsAttackData);

        if (!attackDataAddedOrMoved)
            return;

        EditorApplication.delayCall += RefreshAllDatabases;
    }

    private static bool IsAttackData(string path)
    {
        return AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(AttackData);
    }

    private static void RefreshAllDatabases()
    {
        string[] dbGuids = AssetDatabase.FindAssets("t:AttackDatabase");

        foreach (string guid in dbGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AttackDatabase db = AssetDatabase.LoadAssetAtPath<AttackDatabase>(path);

            if (db == null)
                continue;

            RefreshDatabase(db);
        }
    }

    private static void RefreshDatabase(AttackDatabase db)
    {
        string[] guids = AssetDatabase.FindAssets("t:AttackData");
        AttackData[] allAttacks = new AttackData[guids.Length];

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            allAttacks[i] = AssetDatabase.LoadAssetAtPath<AttackData>(path);
        }

        db.attacks = allAttacks;
        db.Init();

        EditorUtility.SetDirty(db);
        Debug.Log($"[AttackDatabase] Auto-refreshed: {db.name} ({allAttacks.Length} attacks)");
    }
}
