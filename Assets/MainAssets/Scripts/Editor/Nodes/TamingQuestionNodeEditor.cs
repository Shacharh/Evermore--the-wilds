#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using XNodeEditor;

[CustomNodeEditor(typeof(TamingQuestionNode))]
public class TamingQuestionNodeEditor : NodeEditor
{
    private static readonly Color CorrectBg  = new Color(0.10f, 0.40f, 0.10f, 1f);
    private static readonly Color WrongBg    = new Color(0.40f, 0.35f, 0.05f, 1f);
    private static readonly Color ReallyBadBg= new Color(0.40f, 0.08f, 0.08f, 1f);

    public override void OnBodyGUI()
    {
        serializedObject.Update();

        var node = (TamingQuestionNode)target;

        // ── Prompt ────────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Prompt", EditorStyles.boldLabel);
        SerializedProperty promptProp = serializedObject.FindProperty("prompt");
        promptProp.stringValue = EditorGUILayout.TextArea(promptProp.stringValue,
            GUILayout.MinHeight(48));
        EditorGUILayout.Space(4);

        // ── Question Type ─────────────────────────────────────────────────────
        SerializedProperty typeProp = serializedObject.FindProperty("questionType");
        EditorGUILayout.PropertyField(typeProp, new GUIContent("Type"));
        EditorGUILayout.Space(6);

        // ── Answers ───────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Answers", EditorStyles.boldLabel);

        SerializedProperty answersProp = serializedObject.FindProperty("answers");
        int correct = 0, wrong = 0, reallyBad = 0;

        for (int i = 0; i < answersProp.arraySize; i++)
        {
            var entry = answersProp.GetArrayElementAtIndex(i);
            var tagProp  = entry.FindPropertyRelative("tag");
            var textProp = entry.FindPropertyRelative("text");

            var tag = (DialogueEnum.AnswerTag)tagProp.enumValueIndex;
            switch (tag)
            {
                case DialogueEnum.AnswerTag.Correct:   correct++;   break;
                case DialogueEnum.AnswerTag.Wrong:      wrong++;     break;
                case DialogueEnum.AnswerTag.ReallyBad:  reallyBad++; break;
            }

            Color bg = tag switch
            {
                DialogueEnum.AnswerTag.Correct   => CorrectBg,
                DialogueEnum.AnswerTag.Wrong      => WrongBg,
                DialogueEnum.AnswerTag.ReallyBad  => ReallyBadBg,
                _                                 => Color.gray
            };

            var prev = GUI.backgroundColor;
            GUI.backgroundColor = bg;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = prev;

            EditorGUILayout.PropertyField(tagProp,  new GUIContent($"Answer {i + 1} Tag"));
            EditorGUILayout.PropertyField(textProp, new GUIContent("Text"));

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        // ── Validation ────────────────────────────────────────────────────────
        if (correct  != 1 || wrong != 1 || reallyBad != 1)
        {
            EditorGUILayout.HelpBox(
                $"Requires exactly 1 Correct, 1 Wrong, 1 ReallyBad.\n" +
                $"Current: {correct} Correct, {wrong} Wrong, {reallyBad} ReallyBad.",
                MessageType.Warning);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
