using XNode;
using UnityEngine;

[System.Serializable]
public class TamingAnswer
{
    [TextArea(1, 3)] public string text;
    public DialogueEnum.AnswerTag tag;
}

[CreateNodeMenu("Dialogue/Taming/Taming Question")]
[NodeTint(40, 140, 200)]
public class TamingQuestionNode : Node
{
    [TextArea(2, 5)] public string prompt;
    public DialogueEnum.QuestionType questionType;

    [Tooltip("Exactly 3 answers — one Correct, one Wrong, one ReallyBad.")]
    public TamingAnswer[] answers = new TamingAnswer[3]
    {
        new TamingAnswer { tag = DialogueEnum.AnswerTag.Correct   },
        new TamingAnswer { tag = DialogueEnum.AnswerTag.Wrong     },
        new TamingAnswer { tag = DialogueEnum.AnswerTag.ReallyBad }
    };
}
