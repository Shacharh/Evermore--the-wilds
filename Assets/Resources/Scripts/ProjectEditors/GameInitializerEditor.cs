using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines.ExtrusionShapes;

[CustomEditor(typeof(GameInitializer))]
public class GameInitializerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.LabelField("Game Initializer", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Responsible for initializing core game systems.\n\n" +
            "• Should exist only once in the scene\n" +
            "• Runs on game startup\n" +
            "• Order-sensitive\n" +
            "• Lload it in the first game scene or when save file is loaded.",
            MessageType.Info
        );

        EditorGUILayout.Space();
        DrawDefaultInspector();
    }
}
