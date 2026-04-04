using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(StatusEffectData))]
public class StatusEffectDataEditor : Editor
{
    private AttackEnum.StatusEffect _previousID;

    private void OnEnable()
    {
        var idProp = serializedObject.FindProperty("id");
        _previousID = (AttackEnum.StatusEffect)idProp.enumValueIndex;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var idProp = serializedObject.FindProperty("id");
        AttackEnum.StatusEffect currentID = (AttackEnum.StatusEffect)idProp.enumValueIndex;

        if (currentID != _previousID)
        {
            ResetBehaviorFields();
            _previousID = currentID;
        }

        serializedObject.ApplyModifiedProperties();

        DrawDefaultInspector();
    }

    private void ResetBehaviorFields()
    {
        serializedObject.FindProperty("damage").intValue      = 0;
        serializedObject.FindProperty("extraAPCost").intValue = 0;
        serializedObject.FindProperty("stacks").boolValue     = false;
        serializedObject.FindProperty("dispel").boolValue     = false;
        serializedObject.FindProperty("dispelAll").boolValue  = false;
        serializedObject.ApplyModifiedProperties();
    }
}
