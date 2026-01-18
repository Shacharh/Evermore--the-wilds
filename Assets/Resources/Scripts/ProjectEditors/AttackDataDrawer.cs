using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(AttackData))]
public class AttackDataDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Find all AttackData assets
        string[] guids = AssetDatabase.FindAssets("t:AttackData");
        AttackData[] allAttacks = new AttackData[guids.Length];
        string[] options = new string[guids.Length];

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            allAttacks[i] = AssetDatabase.LoadAssetAtPath<AttackData>(path);
            options[i] = allAttacks[i].name;
        }

        // Get current value
        AttackData currentAttack = (AttackData)property.objectReferenceValue;
        int currentIndex = Mathf.Max(0, System.Array.IndexOf(allAttacks, currentAttack));

        // Draw popup
        int newIndex = EditorGUI.Popup(position, label.text, currentIndex, options);

        if (newIndex != currentIndex)
        {
            property.objectReferenceValue = allAttacks[newIndex];
        }
    }
}
