using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AttackDatabase))]
public class AttackDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        AttackDatabase db = (AttackDatabase)target;

        if (GUILayout.Button("Refresh Attacks"))
        {
            RefreshDatabase(db);
        }
    }

    private void RefreshDatabase(AttackDatabase db)
    {
        string[] guids = AssetDatabase.FindAssets("t:AttackData");
        AttackData[] allAttacks = new AttackData[guids.Length];

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            allAttacks[i] = AssetDatabase.LoadAssetAtPath<AttackData>(path);
        }

        db.attacks = allAttacks;
        db.Init(); // refresh the dictionary

        EditorUtility.SetDirty(db);
        Debug.Log($"AttackDatabase refreshed! Found {allAttacks.Length} attacks.");
    }
}
