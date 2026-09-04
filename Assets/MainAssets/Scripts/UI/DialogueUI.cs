using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// UIDocument panel that drives all dialogue sequences.
/// AUTO-SETUP: creates itself at runtime — no scene placement required.
/// Requires UIStyleConfig (Resources/UIStyleConfig) to have PanelSettings assigned.
/// </summary>
public class DialogueUI : MonoBehaviour
{
    public static DialogueUI Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        new GameObject("DialogueUI").AddComponent<DialogueUI>();
    }

    // ── Colours ────────────────────────────────────────────────────────────────
    private static readonly Color PanelBg       = new Color(0.08f, 0.08f, 0.12f, 0.97f);
    private static readonly Color HeaderBg      = new Color(0.15f, 0.15f, 0.22f, 1f);
    private static readonly Color AnswerWrong   = new Color(0.18f, 0.18f, 0.26f, 1f);
    private static readonly Color AnswerHov     = new Color(0.28f, 0.28f, 0.40f, 1f);
    private static readonly Color AssistBg      = new Color(0.35f, 0.25f, 0.55f, 1f);
    private static readonly Color AssistHov     = new Color(0.50f, 0.35f, 0.75f, 1f);
    private static readonly Color CancelBg      = new Color(0.40f, 0.10f, 0.10f, 1f);
    private static readonly Color CancelHov     = new Color(0.60f, 0.15f, 0.15f, 1f);
    private static readonly Color TextPrimary   = Color.white;
    private static readonly Color TextSecondary = new Color(0.7f, 0.7f, 0.7f, 1f);
    private static readonly Color TextAccent    = new Color(1f, 0.85f, 0.3f, 1f);

    private UIDocument    _doc;
    private VisualElement _root;

    // ── Runner & State ─────────────────────────────────────────────────────────
    private DialogueRunner     _runner;
    private Monster            _tamingTarget;
    private TamingQuestionNode _currentQuestion;
    private int[]              _currentOrder;
    private int                _assistUsesThisSession;
    private VisualElement      _contentArea;
    private Label              _stageLabel;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        var s = UIStyleConfig.Load();
        if (s?.panelSettings == null)
        {
            Debug.LogWarning("[DialogueUI] PanelSettings not set in UIStyleConfig — dialogue UI disabled.");
            return;
        }

        _doc               = gameObject.AddComponent<UIDocument>();
        _doc.panelSettings = s.panelSettings;
        _doc.sortingOrder  = 300;

        _root             = _doc.rootVisualElement;
        _root.pickingMode = PickingMode.Ignore;
        _root.style.display  = DisplayStyle.None;
        _root.style.position = Position.Absolute;
        _root.style.left = 0; _root.style.right  = 0;
        _root.style.top  = 0; _root.style.bottom = 0;
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    public void OpenTaming(Monster target, DialogueGraph graph)
    {
        if (_root == null) return;
        _tamingTarget          = target;
        _assistUsesThisSession = 0;

        _runner = new DialogueRunner();
        _runner.OnTamingQuestion += ShowTamingQuestion;
        _runner.OnSimpleNode     += ShowSimpleNode;
        _runner.OnOptionNode     += ShowOptionNode;
        _runner.OnEnd            += OnEnd;

        BuildShell(target.Data?.displayName ?? target.name, target);
        Show();
        _runner.StartDialogue(graph);
    }

    public void OpenGeneral(DialogueGraph graph)
    {
        if (_root == null || graph == null) return;
        _tamingTarget = null;

        var start = System.Linq.Enumerable.OfType<StartNode>(graph.nodes).FirstOrDefault();
        string speakerName = start?.speakerName ?? "";

        _runner = new DialogueRunner();
        _runner.OnSimpleNode += ShowSimpleNode;
        _runner.OnOptionNode += ShowOptionNode;
        _runner.OnEnd        += OnEnd;

        BuildShell(speakerName);
        Show();
        _runner.StartDialogue(graph);
    }

    public void OpenWorld(DialogueGraph graph)
    {
        OpenGeneral(graph);
    }

    public void Close()
    {
        if (_root == null) return;
        _root.style.display = DisplayStyle.None;
        _root.pickingMode   = PickingMode.Ignore;
        _runner       = null;
        _tamingTarget = null;
    }

    // ── Shell Build ────────────────────────────────────────────────────────────

    private void BuildShell(string speakerName, Monster portrait = null)
    {
        _root.Clear();

        var overlay = new VisualElement();
        overlay.style.position        = Position.Absolute;
        overlay.style.left = 0; overlay.style.right  = 0;
        overlay.style.top  = 0; overlay.style.bottom = 0;
        overlay.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.5f));
        overlay.style.alignItems      = Align.Center;
        overlay.style.justifyContent  = Justify.Center;
        _root.Add(overlay);

        // Row container — holds portrait panel (optional) + dialogue panel
        var outer = new VisualElement();
        outer.style.flexDirection = FlexDirection.Row;
        outer.RegisterCallback<ClickEvent>(e => e.StopPropagation());
        overlay.Add(outer);

        bool hasPortrait = portrait != null;
        if (hasPortrait)
            BuildPortraitSection(outer, portrait);

        // ── Dialogue panel ────────────────────────────────────────────────────
        var panel = new VisualElement();
        panel.style.width           = 520;
        panel.style.backgroundColor = new StyleColor(PanelBg);
        panel.style.flexDirection   = FlexDirection.Column;
        panel.style.borderTopRightRadius    = 12;
        panel.style.borderBottomRightRadius = 12;
        if (!hasPortrait)
        {
            panel.style.borderTopLeftRadius    = 12;
            panel.style.borderBottomLeftRadius = 12;
        }
        outer.Add(panel);

        var header = new VisualElement();
        header.style.flexDirection   = FlexDirection.Row;
        header.style.justifyContent  = Justify.SpaceBetween;
        header.style.alignItems      = Align.Center;
        header.style.backgroundColor = new StyleColor(HeaderBg);
        header.style.paddingLeft = 16; header.style.paddingRight  = 16;
        header.style.paddingTop  = 12; header.style.paddingBottom = 12;
        header.style.borderTopRightRadius = 12;
        if (!hasPortrait)
            header.style.borderTopLeftRadius = 12;
        panel.Add(header);

        var speakerLabel = new Label(speakerName.ToUpper());
        speakerLabel.style.color                   = new StyleColor(TextAccent);
        speakerLabel.style.fontSize                = 15;
        speakerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.Add(speakerLabel);

        _stageLabel = new Label("");
        _stageLabel.style.color    = new StyleColor(TextSecondary);
        _stageLabel.style.fontSize = 13;
        header.Add(_stageLabel);

        _contentArea = new VisualElement();
        _contentArea.style.paddingLeft   = 20;
        _contentArea.style.paddingRight  = 20;
        _contentArea.style.paddingTop    = 16;
        _contentArea.style.paddingBottom = 16;
        _contentArea.style.flexDirection = FlexDirection.Column;
        panel.Add(_contentArea);
    }

    private static void BuildPortraitSection(VisualElement parent, Monster monster)
    {
        var section = new VisualElement();
        section.style.width                  = 150;
        section.style.backgroundColor        = new StyleColor(new Color(0.05f, 0.05f, 0.10f, 1f));
        section.style.flexDirection          = FlexDirection.Column;
        section.style.alignItems             = Align.Center;
        section.style.justifyContent         = Justify.Center;
        section.style.paddingTop             = 20;
        section.style.paddingBottom          = 20;
        section.style.borderTopLeftRadius    = 12;
        section.style.borderBottomLeftRadius = 12;
        parent.Add(section);

        // Portrait image
        var img = new VisualElement();
        img.style.width  = 110;
        img.style.height = 110;
        img.style.borderTopLeftRadius     = 8;
        img.style.borderTopRightRadius    = 8;
        img.style.borderBottomLeftRadius  = 8;
        img.style.borderBottomRightRadius = 8;
        if (monster.Data?.portrait != null)
            img.style.backgroundImage = new StyleBackground(monster.Data.portrait);
        else
            img.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.3f, 1f));
        section.Add(img);

        // Monster name
        var nameLabel = new Label(monster.Data?.displayName ?? monster.name);
        nameLabel.style.color                   = new StyleColor(TextAccent);
        nameLabel.style.fontSize                = 13;
        nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        nameLabel.style.marginTop               = 10;
        nameLabel.style.unityTextAlign          = TextAnchor.MiddleCenter;
        nameLabel.style.whiteSpace              = WhiteSpace.Normal;
        section.Add(nameLabel);

        // Element type
        var typeLabel = new Label(monster.Data?.elementType.ToString() ?? "");
        typeLabel.style.color          = new StyleColor(TextSecondary);
        typeLabel.style.fontSize       = 10;
        typeLabel.style.marginTop      = 3;
        typeLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        section.Add(typeLabel);

        // Current HP
        var hpLabel = new Label($"HP  {monster.CurrentHP} / {monster.MaxHP}");
        hpLabel.style.color          = new StyleColor(new Color(0.5f, 1f, 0.5f, 1f));
        hpLabel.style.fontSize       = 10;
        hpLabel.style.marginTop      = 4;
        hpLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        section.Add(hpLabel);
    }

    // ── Content Renderers ──────────────────────────────────────────────────────

    private void ShowTamingQuestion(TamingQuestionNode question, int[] order)
    {
        _currentQuestion = question;
        _currentOrder    = order;
        RenderTamingQuestion();
    }

    private void RenderTamingQuestion()
    {
        _contentArea.Clear();
        var question = _currentQuestion;
        var order    = _currentOrder;

        if (_runner != null)
            _stageLabel.text = $"Question {_runner.TamingStage + 1} / {_runner.TamingTotal}";

        var typeBadge = new Label($"[{question.questionType}]");
        typeBadge.style.color       = new StyleColor(TextSecondary);
        typeBadge.style.fontSize    = 10;
        typeBadge.style.marginBottom = 4;
        _contentArea.Add(typeBadge);

        var prompt = new Label(question.prompt);
        prompt.style.color       = new StyleColor(TextPrimary);
        prompt.style.fontSize    = 15;
        prompt.style.whiteSpace  = WhiteSpace.Normal;
        prompt.style.marginBottom = 16;
        _contentArea.Add(prompt);

        string[] letters = { "A", "B", "C" };
        for (int i = 0; i < order.Length; i++)
        {
            int origIdx = order[i];
            if (origIdx < 0 || origIdx >= question.answers.Length) continue;

            var answer       = question.answers[origIdx];
            bool isEliminated = _runner != null && _runner.EliminatedIndex == origIdx;
            bool isHinted     = _runner != null && _runner.HintRevealed && answer.tag == DialogueEnum.AnswerTag.Correct;

            string displayText = $"{letters[i]}: {answer.text}";
            if (isHinted) displayText += " ✓";

            Color normalBg = isEliminated ? new Color(0.1f, 0.1f, 0.1f, 0.4f) : AnswerWrong;
            Color hoverBg  = isEliminated ? normalBg : AnswerHov;

            int capturedOrig = origIdx;
            var btn = MakeButton(displayText, normalBg, hoverBg, () =>
            {
                if (isEliminated) return;
                _runner?.SubmitTamingAnswer(question, capturedOrig);
            });
            btn.style.marginBottom   = 6;
            btn.style.width          = new StyleLength(new Length(100f, LengthUnit.Percent));
            btn.style.height         = 46;
            btn.style.fontSize       = 13;
            btn.style.unityTextAlign = TextAnchor.MiddleLeft;
            btn.style.paddingLeft    = 12;
            if (isEliminated) btn.SetEnabled(false);
            _contentArea.Add(btn);
        }

        var footer = new VisualElement();
        footer.style.flexDirection  = FlexDirection.Row;
        footer.style.justifyContent = Justify.SpaceBetween;
        footer.style.marginTop      = 12;
        _contentArea.Add(footer);

        var assistItem = FindAvailableAssistItem();
        if (assistItem != null)
        {
            var assistBtn = MakeButton("ASSIST", AssistBg, AssistHov, () => UseAssistItem(assistItem));
            assistBtn.style.width = 120; assistBtn.style.height = 40;
            footer.Add(assistBtn);
        }
        else
        {
            footer.Add(new VisualElement());
        }

        var cancelBtn = MakeButton("CANCEL", CancelBg, CancelHov, OnCancel);
        cancelBtn.style.width = 100; cancelBtn.style.height = 40;
        footer.Add(cancelBtn);
    }

    private void ShowSimpleNode(SimpleDialogueNode node)
    {
        _contentArea.Clear();
        _stageLabel.text = "";

        var prompt = new Label(node.prompt);
        prompt.style.color       = new StyleColor(TextPrimary);
        prompt.style.fontSize    = 15;
        prompt.style.whiteSpace  = WhiteSpace.Normal;
        prompt.style.marginBottom = 20;
        _contentArea.Add(prompt);

        var continueBtn = MakeButton("CONTINUE", AnswerWrong, AnswerHov, () => _runner?.Advance());
        continueBtn.style.alignSelf = Align.FlexEnd;
        continueBtn.style.width     = 140;
        continueBtn.style.height    = 42;
        _contentArea.Add(continueBtn);
    }

    private void ShowOptionNode(OptionDialogueNode node)
    {
        _contentArea.Clear();
        _stageLabel.text = "";

        var prompt = new Label(node.prompt);
        prompt.style.color       = new StyleColor(TextPrimary);
        prompt.style.fontSize    = 15;
        prompt.style.whiteSpace  = WhiteSpace.Normal;
        prompt.style.marginBottom = 16;
        _contentArea.Add(prompt);

        for (int i = 0; i < node.options.Count; i++)
        {
            int idx = i;
            var opt = node.options[i];
            var btn = MakeButton(opt.text, AnswerWrong, AnswerHov, () => _runner?.SelectOption(idx));
            btn.style.marginBottom   = 6;
            btn.style.width          = new StyleLength(new Length(100f, LengthUnit.Percent));
            btn.style.height         = 46;
            btn.style.fontSize       = 13;
            btn.style.unityTextAlign = TextAnchor.MiddleLeft;
            btn.style.paddingLeft    = 12;
            _contentArea.Add(btn);
        }
    }

    // ── End Handling ───────────────────────────────────────────────────────────

    private void OnEnd(DialogueEnum.DialogueOutcome outcome, float score)
    {
        var target = _tamingTarget;
        Close();
        if (target != null)
            TamingSystem.Instance?.OnDialogueComplete(outcome, score);
    }

    private void OnCancel()
    {
        var target = _tamingTarget;
        Close();
        if (target != null)
            TamingSystem.Instance?.OnDialogueComplete(DialogueEnum.DialogueOutcome.CaptureFail, 0f);
    }

    // ── Assist Logic ───────────────────────────────────────────────────────────

    private ItemData FindAvailableAssistItem()
    {
        var inv = PlayerInventory.Instance;
        if (inv == null) return null;
        foreach (var (item, qty) in inv.GetAll())
        {
            if (item.Archetype != ItemEnum.Archetype.DialogAssist) continue;
            if (qty <= 0) continue;
            if (_assistUsesThisSession >= item.UsesPerSession) continue;
            return item;
        }
        return null;
    }

    private void UseAssistItem(ItemData item)
    {
        var inv = PlayerInventory.Instance;
        if (inv == null || _runner == null || _currentQuestion == null) return;

        inv.RemoveItem(item);
        _assistUsesThisSession++;

        switch (item.AssistType)
        {
            case ItemEnum.DialogAssistType.HintReveal:
                _runner.ApplyHintReveal();
                BattleMessage.Show("The correct answer shines!", 1.5f);
                break;
            case ItemEnum.DialogAssistType.EliminateOption:
                _runner.ApplyEliminateOption(_currentQuestion);
                BattleMessage.Show("One bad option was removed!", 1.5f);
                break;
            case ItemEnum.DialogAssistType.AllowRetry:
                _runner.ApplyAllowRetry();
                BattleMessage.Show("Your next wrong answer won't count!", 1.5f);
                break;
        }

        RenderTamingQuestion();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private void Show()
    {
        _root.pickingMode   = PickingMode.Position;
        _root.style.display = DisplayStyle.Flex;
    }

    private static Button MakeButton(string text, Color normal, Color hover, System.Action onClick)
    {
        var btn = new Button(onClick) { text = text };
        btn.style.backgroundColor         = new StyleColor(normal);
        btn.style.color                   = new StyleColor(Color.white);
        btn.style.borderTopWidth          = 0; btn.style.borderBottomWidth = 0;
        btn.style.borderLeftWidth         = 0; btn.style.borderRightWidth  = 0;
        btn.style.borderTopLeftRadius     = 6;
        btn.style.borderTopRightRadius    = 6;
        btn.style.borderBottomLeftRadius  = 6;
        btn.style.borderBottomRightRadius = 6;
        btn.RegisterCallback<MouseEnterEvent>(_ => btn.style.backgroundColor = new StyleColor(hover));
        btn.RegisterCallback<MouseLeaveEvent>(_ => btn.style.backgroundColor = new StyleColor(normal));
        return btn;
    }
}
