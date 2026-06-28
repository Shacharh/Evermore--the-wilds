using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// QA dev control panel. Press backtick (`) to show/hide.
/// - Add AP to the player
/// - Apply / clear status effects on any monster in the scene
/// </summary>
public class DevPanel : MonoBehaviour
{
    public static DevPanel Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Instance != null) return;
        new GameObject("DevPanel").AddComponent<DevPanel>();
#endif
    }

    private const int StatusTurns = 3;

    private UIDocument    _doc;
    private VisualElement _panel;
    private VisualElement _monsterRow;
    private Label         _selectedLabel;
    private bool          _visible;
    private Monster       _target;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
        SetVisible(false);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
        {
            Debug.Log($"[DevPanel] F9 pressed — toggling panel {(_visible ? "OFF" : "ON")}");
            SetVisible(!_visible);
        }
    }

    // ── Visibility ────────────────────────────────────────────────────────────

    private void SetVisible(bool v)
    {
        _visible = v;
        _panel.style.display = v ? DisplayStyle.Flex : DisplayStyle.None;
        if (v) RefreshMonsterList();
    }

    // ── Monster picker ────────────────────────────────────────────────────────

    private void RefreshMonsterList()
    {
        _monsterRow.Clear();
        _target = null;
        UpdateSelectedLabel();

        foreach (var m in FindObjectsByType<Monster>(FindObjectsSortMode.None))
        {
            if (m == null || !m.IsAlive) continue;
            Monster captured = m;
            string  label    = m.name.Replace("(Clone)", "").Trim();
            Color   bg       = m.IsEnemy
                ? new Color(0.75f, 0.22f, 0.22f, 1f)
                : new Color(0.18f, 0.60f, 0.28f, 1f);

            var btn = MakeBtn(label, bg, () => { _target = captured; UpdateSelectedLabel(); });
            btn.style.marginRight  = 4;
            btn.style.marginBottom = 4;
            _monsterRow.Add(btn);
        }
    }

    private void UpdateSelectedLabel()
    {
        _selectedLabel.text = _target != null
            ? $"Target: {_target.name.Replace("(Clone)", "").Trim()}"
            : "Target: none";
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    private void AddPlayerAP(int amount)
    {
        var ptc = TurnManager.Instance?.PlayerController;
        if (ptc == null) { Debug.LogWarning("[DevPanel] No PlayerController."); return; }
        ptc.GainAP(amount);
    }

    private void ApplyStatus(AttackEnum.StatusEffect id)
    {
        if (_target == null || !_target.IsAlive)
        {
            Debug.LogWarning("[DevPanel] Select a living monster first.");
            return;
        }
        _target.DevApplyStatus(id, StatusTurns);
    }

    private void ClearStatuses()
    {
        if (_target == null) { Debug.LogWarning("[DevPanel] Select a monster first."); return; }
        _target.DevClearStatuses();
    }

    // ── UI Build ──────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        var s = UIStyleConfig.Load();
        _doc = gameObject.AddComponent<UIDocument>();
        _doc.panelSettings = s?.panelSettings;
        _doc.sortingOrder  = 200;

        var root = _doc.rootVisualElement;
        root.pickingMode    = PickingMode.Ignore;
        root.style.position = Position.Absolute;
        root.style.left = 0; root.style.top = 0; root.style.right = 0; root.style.bottom = 0;

        // ── Panel ─────────────────────────────────────────────────────────────
        _panel = new VisualElement();
        _panel.style.position        = Position.Absolute;
        _panel.style.left            = 16;
        _panel.style.bottom          = 16;
        _panel.style.width           = 270;
        _panel.style.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 0.95f);
        ApplyPanelBorder(_panel, new Color(1f, 0.78f, 0.1f, 1f));
        _panel.style.paddingTop    = 10; _panel.style.paddingBottom = 12;
        _panel.style.paddingLeft   = 12; _panel.style.paddingRight  = 12;
        _panel.style.flexDirection = FlexDirection.Column;

        // ── Header ────────────────────────────────────────────────────────────
        var header = new Label("DEV TOOLS   [ F9 to toggle ]");
        header.style.fontSize                = 11;
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.color                   = new Color(1f, 0.78f, 0.1f, 1f);
        header.style.marginBottom            = 10;
        _panel.Add(header);

        // ── AP Section ───────────────────────────────────────────────────────
        _panel.Add(Divider("AP  (Player)"));
        var apRow = Row();
        apRow.Add(MakeBtn("+5",  new Color(0.18f, 0.45f, 0.80f, 1f), () => AddPlayerAP(5)));
        apRow.Add(Spacer(4));
        apRow.Add(MakeBtn("+10", new Color(0.18f, 0.45f, 0.80f, 1f), () => AddPlayerAP(10)));
        apRow.Add(Spacer(4));
        apRow.Add(MakeBtn("MAX", new Color(0.10f, 0.28f, 0.55f, 1f), () =>
        {
            var ptc = TurnManager.Instance?.PlayerController;
            if (ptc != null) ptc.GainAP(ptc.MaxAP);
        }));
        _panel.Add(apRow);

        // ── Monster Picker ────────────────────────────────────────────────────
        _panel.Add(Divider("Target Monster"));
        _monsterRow = new VisualElement();
        _monsterRow.style.flexDirection  = FlexDirection.Row;
        _monsterRow.style.flexWrap       = Wrap.Wrap;
        _monsterRow.style.marginBottom   = 4;
        _panel.Add(_monsterRow);

        _selectedLabel = new Label("Target: none");
        _selectedLabel.style.fontSize    = 10;
        _selectedLabel.style.color       = new Color(0.6f, 0.6f, 0.6f, 1f);
        _selectedLabel.style.marginBottom = 10;
        _panel.Add(_selectedLabel);

        // ── Status Section ────────────────────────────────────────────────────
        _panel.Add(Divider($"Apply Status  ({StatusTurns} turns)"));
        var statusRow = Row();
        statusRow.style.flexWrap = Wrap.Wrap;
        statusRow.Add(MakeBtn("BRN", new Color(1f,    0.40f, 0.08f, 1f), () => ApplyStatus(AttackEnum.StatusEffect.Burn)));
        statusRow.Add(Spacer(4));
        statusRow.Add(MakeBtn("FRZ", new Color(0.20f, 0.72f, 1f,   1f), () => ApplyStatus(AttackEnum.StatusEffect.Freeze)));
        statusRow.Add(Spacer(4));
        statusRow.Add(MakeBtn("SHK", new Color(0.92f, 0.88f, 0f,   1f), () => ApplyStatus(AttackEnum.StatusEffect.Shock)));
        statusRow.Add(Spacer(4));
        statusRow.Add(MakeBtn("PSN", new Color(0.60f, 0.12f, 0.85f,1f), () => ApplyStatus(AttackEnum.StatusEffect.Poison)));
        statusRow.Add(Spacer(4));
        statusRow.Add(MakeBtn("ZZZ", new Color(0.38f, 0.38f, 0.75f,1f), () => ApplyStatus(AttackEnum.StatusEffect.Sleep)));
        _panel.Add(statusRow);

        var clearBtn = MakeBtn("CLEAR ALL STATUSES", new Color(0.38f, 0.10f, 0.10f, 1f), ClearStatuses);
        clearBtn.style.width       = new Length(100, LengthUnit.Percent);
        clearBtn.style.marginTop   = 6;
        _panel.Add(clearBtn);

        root.Add(_panel);
    }

    // ── UI Helpers ────────────────────────────────────────────────────────────

    private static void ApplyPanelBorder(VisualElement el, Color accent)
    {
        var dim = new Color(accent.r, accent.g, accent.b, 0.3f);
        el.style.borderTopWidth    = 2; el.style.borderBottomWidth = 1;
        el.style.borderLeftWidth   = 1; el.style.borderRightWidth  = 1;
        el.style.borderTopColor    = accent;
        el.style.borderBottomColor = dim;
        el.style.borderLeftColor   = dim;
        el.style.borderRightColor  = dim;
        el.style.borderTopLeftRadius    = 5; el.style.borderTopRightRadius    = 5;
        el.style.borderBottomLeftRadius = 5; el.style.borderBottomRightRadius = 5;
    }

    private static Label Divider(string text)
    {
        var l = new Label(text);
        l.style.fontSize     = 10;
        l.style.color        = new Color(0.45f, 0.45f, 0.45f, 1f);
        l.style.marginBottom = 4;
        l.style.marginTop    = 6;
        return l;
    }

    private static VisualElement Row()
    {
        var r = new VisualElement();
        r.style.flexDirection = FlexDirection.Row;
        r.style.marginBottom  = 4;
        return r;
    }

    private static VisualElement Spacer(float w)
    {
        var s = new VisualElement();
        s.style.width = w;
        return s;
    }

    private static Button MakeBtn(string label, Color bg, System.Action onClick)
    {
        var btn = new Button(onClick);
        btn.text = label;
        btn.style.fontSize                = 11;
        btn.style.unityFontStyleAndWeight = FontStyle.Bold;
        btn.style.color                   = Color.white;
        btn.style.height                  = 24;
        btn.style.paddingLeft             = 8;
        btn.style.paddingRight            = 8;
        btn.style.borderTopWidth          = 0; btn.style.borderBottomWidth = 0;
        btn.style.borderLeftWidth         = 0; btn.style.borderRightWidth  = 0;
        btn.style.borderTopLeftRadius     = 3; btn.style.borderTopRightRadius    = 3;
        btn.style.borderBottomLeftRadius  = 3; btn.style.borderBottomRightRadius = 3;
        btn.style.backgroundColor         = bg;
        return btn;
    }
}
