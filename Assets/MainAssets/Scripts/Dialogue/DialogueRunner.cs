using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Executes a DialogueGraph at runtime.
/// Sequential mode: follows StartNode → BaseDialogueNode chain.
/// Taming mode: detects TamingStartNode, pools all TamingQuestionNodes, picks N randomly.
/// </summary>
public class DialogueRunner
{
    public event Action<SimpleDialogueNode>                  OnSimpleNode;
    public event Action<OptionDialogueNode>                  OnOptionNode;
    public event Action<TamingQuestionNode, int[]>           OnTamingQuestion;
    public event Action<DialogueEnum.DialogueOutcome, float> OnEnd; // float = dialogue score 0-1

    public bool IsTamingMode { get; private set; }
    public int  TamingStage  { get; private set; }
    public int  TamingTotal  { get; private set; }

    private BaseDialogueNode         _current;
    private List<TamingQuestionNode> _pool;
    private int                      _correct;
    private bool                     _hadReallyBad;

    private bool _hintRevealed;
    private int  _eliminatedIndex = -1;
    private bool _retryActive;

    public bool HintRevealed    => _hintRevealed;
    public int  EliminatedIndex => _eliminatedIndex;
    public bool RetryActive     => _retryActive;

    // ── Entry ─────────────────────────────────────────────────────────────────

    public void StartDialogue(DialogueGraph graph)
    {
        if (graph == null) { End(DialogueEnum.DialogueOutcome.None); return; }

        var tamingStart = graph.nodes.OfType<TamingStartNode>().FirstOrDefault();
        if (tamingStart != null) StartTamingMode(graph, tamingStart);
        else                     StartSequentialMode(graph);
    }

    // ── Sequential ────────────────────────────────────────────────────────────

    private void StartSequentialMode(DialogueGraph graph)
    {
        IsTamingMode = false;
        var start = graph.nodes.OfType<StartNode>().FirstOrDefault();
        if (start == null) { End(DialogueEnum.DialogueOutcome.None); return; }
        AdvanceTo(start.GetNext());
    }

    public void Advance()
    {
        if (!IsTamingMode && _current != null) AdvanceTo(_current.GetNextNode());
    }

    public void SelectOption(int index)
    {
        if (!IsTamingMode && _current is OptionDialogueNode o) AdvanceTo(o.GetNextNode(index));
    }

    private void AdvanceTo(BaseDialogueNode node)
    {
        _current = node;
        if (node == null)                { End(DialogueEnum.DialogueOutcome.None); return; }
        if (node is EndNode end)          { End(end.outcome); return; }
        if (node is SimpleDialogueNode s) { OnSimpleNode?.Invoke(s); return; }
        if (node is OptionDialogueNode o) { OnOptionNode?.Invoke(o); return; }
        AdvanceTo(node.GetNextNode());
    }

    // ── Taming ────────────────────────────────────────────────────────────────

    private void StartTamingMode(DialogueGraph graph, TamingStartNode settings)
    {
        IsTamingMode  = true;
        TamingStage   = 0;
        _correct      = 0;
        _hadReallyBad = false;

        var all = graph.nodes.OfType<TamingQuestionNode>().ToList();
        Shuffle(all);
        _pool       = all.Take(Mathf.Min(settings.questionsPerSession, all.Count)).ToList();
        TamingTotal = _pool.Count;

        if (TamingTotal == 0) { End(DialogueEnum.DialogueOutcome.CaptureFail); return; }
        PresentTamingQuestion();
    }

    private void PresentTamingQuestion()
    {
        if (TamingStage >= _pool.Count) { ResolveTamingOutcome(); return; }
        _hintRevealed    = false;
        _eliminatedIndex = -1;
        _retryActive     = false;
        int[] order = ShuffledIndices(_pool[TamingStage].answers.Length);
        OnTamingQuestion?.Invoke(_pool[TamingStage], order);
    }

    public void SubmitTamingAnswer(TamingQuestionNode question, int originalIndex)
    {
        if (!IsTamingMode) return;
        var tag = question.answers[originalIndex].tag;

        if (_retryActive && tag != DialogueEnum.AnswerTag.Correct)
        {
            _retryActive = false;
            PresentTamingQuestion();
            return;
        }

        switch (tag)
        {
            case DialogueEnum.AnswerTag.Correct:
                _correct++;
                TamingStage++;
                PresentTamingQuestion();
                break;
            case DialogueEnum.AnswerTag.Wrong:
                TamingStage++;
                PresentTamingQuestion();
                break;
            case DialogueEnum.AnswerTag.ReallyBad:
                _hadReallyBad = true;
                ResolveTamingOutcome();
                break;
        }
    }

    private void ResolveTamingOutcome()
    {
        DialogueEnum.DialogueOutcome outcome =
            _hadReallyBad          ? DialogueEnum.DialogueOutcome.CapturePenalty :
            _correct == TamingTotal ? DialogueEnum.DialogueOutcome.CaptureSuccess :
            _correct >= 1           ? DialogueEnum.DialogueOutcome.CapturePartial :
                                      DialogueEnum.DialogueOutcome.CaptureFail;
        End(outcome);
    }

    // ── Assist ────────────────────────────────────────────────────────────────

    public void ApplyHintReveal() => _hintRevealed = true;

    public int ApplyEliminateOption(TamingQuestionNode question)
    {
        var candidates = new List<int>();
        for (int i = 0; i < question.answers.Length; i++)
            if (question.answers[i].tag != DialogueEnum.AnswerTag.Correct && i != _eliminatedIndex)
                candidates.Add(i);
        if (candidates.Count == 0) return -1;
        _eliminatedIndex = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        return _eliminatedIndex;
    }

    public void ApplyAllowRetry() => _retryActive = true;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void End(DialogueEnum.DialogueOutcome outcome)
    {
        float score = IsTamingMode && TamingTotal > 0 ? (float)_correct / TamingTotal : 0f;
        OnEnd?.Invoke(outcome, score);
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static int[] ShuffledIndices(int count)
    {
        int[] arr = new int[count];
        for (int i = 0; i < count; i++) arr[i] = i;
        for (int i = count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
        return arr;
    }
}
