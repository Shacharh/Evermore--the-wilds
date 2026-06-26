using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

/// <summary>
/// Full-screen key-binding panel built with UI Toolkit.
/// Auto-created at runtime — no scene setup required.
///
/// Open it via PauseMenu (the Key Bindings button), which calls Show().
/// When the player presses Back, Hide() fires the onClose event and PauseMenu restores itself.
/// </summary>
public class HotkeyRebindUI : MonoBehaviour
{
    public static HotkeyRebindUI Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        new GameObject("HotkeyRebindUI").AddComponent<HotkeyRebindUI>();
    }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired when the panel is closed via Hide() or the Back button.</summary>
    public event System.Action onClose;

    // ── UI references ─────────────────────────────────────────────────────────

    private UIDocument    _doc;
    private VisualElement _overlay;
    private VisualElement _listeningOverlay;
    private Label         _listeningLabel;

    // Key labels keyed by action, updated after each rebind
    private readonly Dictionary<HotkeyAction, Label> _keyLabels = new();

    // ── State ─────────────────────────────────────────────────────────────────

    private HotkeyAction? _listeningFor;
    private bool          _isVisible;

    // ── Action definitions — order determines display order ───────────────────

    private static readonly (HotkeyAction action, string label)[] ActionDefs =
    {
        (HotkeyAction.EndTurn,     "End Turn"),
        (HotkeyAction.Cancel,      "Cancel / Back"),
        (HotkeyAction.Pause,       "Pause"),
        (HotkeyAction.ResetCamera, "Reset Camera"),
        (HotkeyAction.Move,        "Move"),
        (HotkeyAction.Attack,      "Attack"),
        (HotkeyAction.Info,        "Monster Info"),
        (HotkeyAction.Attack1,     "Attack 1"),
        (HotkeyAction.Attack2,     "Attack 2"),
        (HotkeyAction.Attack3,     "Attack 3"),
    };

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!_isVisible || _listeningFor == null) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        foreach (Key key in System.Enum.GetValues(typeof(Key)))
        {
            if (key == Key.None) continue;
            if (!kb[key].wasPressedThisFrame) continue;

            if (key == Key.Escape)
            {
                EndListen();
                return;
            }

            HotkeyManager.Instance?.Rebind(_listeningFor.Value, key);
            RefreshKeyLabels();
            EndListen();
            return;
        }
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    public void Show()
    {
        RefreshKeyLabels();
        SetVisible(true);
    }

    public void Hide()
    {
        EndListen();
        SetVisible(false);
        var handler = onClose;
        onClose = null;   // clear before invoking so it isn't called twice
        handler?.Invoke();
    }

    // ── Internal ───────────────────────────────────────────────────────────────

    private void SetVisible(bool visible)
    {
        _isVisible = visible;
        if (_overlay != null)
            _overlay.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void BeginListen(HotkeyAction action, string displayName)
    {
        _listeningFor = action;
        if (_listeningLabel != null)
            _listeningLabel.text = $"Binding  [ {displayName} ]\nPress any key…\n" +
                                   "<size=70%>Escape to cancel</size>";
        if (_listeningOverlay != null)
            _listeningOverlay.style.display = DisplayStyle.Flex;
    }

    private void EndListen()
    {
        _listeningFor = null;
        if (_listeningOverlay != null)
            _listeningOverlay.style.display = DisplayStyle.None;
    }

    private void RefreshKeyLabels()
    {
        if (HotkeyManager.Instance == null) return;
        foreach (var kvp in _keyLabels)
            kvp.Value.text = HotkeyManager.Instance.GetDisplayName(kvp.Key);
    }

    // ── UI Construction ────────────────────────────────────────────────────────

    private void BuildUI()
    {
        var s = UIStyleConfig.Load();
        if (s?.panelSettings == null)
        {
            Debug.LogWarning("[HotkeyRebindUI] PanelSettings not assigned in UIStyleConfig.");
            return;
        }

        _doc = gameObject.AddComponent<UIDocument>();
        _doc.panelSettings = s.panelSettings;
        _doc.sortingOrder  = 20; // above PauseMenu (10)

        var root = _doc.rootVisualElement;

        // ── Full-screen modal overlay ─────────────────────────────────────────
        _overlay = new VisualElement();
        _overlay.style.position         = Position.Absolute;
        _overlay.style.left = _overlay.style.top = _overlay.style.right = _overlay.style.bottom = 0;
        _overlay.style.backgroundColor  = new Color(0f, 0f, 0f, 0.72f);
        _overlay.style.alignItems       = Align.Center;
        _overlay.style.justifyContent   = Justify.Center;
        root.Add(_overlay);

        // ── Center panel ──────────────────────────────────────────────────────
        var panel = new VisualElement();
        panel.style.width                  = 660;
        panel.style.maxHeight              = 720;
        panel.style.backgroundColor        = new Color(0.05f, 0.07f, 0.14f, 0.97f);
        panel.style.borderTopLeftRadius    = 8;
        panel.style.borderTopRightRadius   = 8;
        panel.style.borderBottomLeftRadius = 8;
        panel.style.borderBottomRightRadius= 8;
        panel.style.borderTopWidth    = 1; panel.style.borderBottomWidth = 1;
        panel.style.borderLeftWidth   = 1; panel.style.borderRightWidth  = 1;
        Color border = new Color(0.3f, 0.35f, 0.55f, 0.6f);
        panel.style.borderTopColor    = border; panel.style.borderBottomColor = border;
        panel.style.borderLeftColor   = border; panel.style.borderRightColor  = border;
        panel.style.overflow          = Overflow.Hidden;
        panel.style.flexDirection     = FlexDirection.Column;
        _overlay.Add(panel);

        // ── Header strip ──────────────────────────────────────────────────────
        var header = new VisualElement();
        header.style.backgroundColor = new Color(0.08f, 0.04f, 0.18f, 1f);
        header.style.paddingTop      = 18;
        header.style.paddingBottom   = 18;
        header.style.alignItems      = Align.Center;
        var titleLabel = new Label("KEY BINDINGS");
        titleLabel.style.fontSize                = 22;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.color                   = new Color(0.85f, 0.80f, 1f, 1f);
        header.Add(titleLabel);
        panel.Add(header);

        // Column header row
        var colHeader = MakeRow(null);
        colHeader.style.backgroundColor = new Color(0.1f, 0.1f, 0.2f, 0.6f);
        colHeader.style.paddingTop = colHeader.style.paddingBottom = 6;
        AddCell(colHeader, "ACTION",      220, FontStyle.Bold, new Color(0.65f, 0.65f, 0.85f));
        AddCell(colHeader, "CURRENT KEY", 160, FontStyle.Bold, new Color(0.65f, 0.65f, 0.85f));
        AddCell(colHeader, "",            130, FontStyle.Normal, Color.clear);
        panel.Add(colHeader);

        // ── Scrollable rows ───────────────────────────────────────────────────
        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.flexGrow     = 1;
        scroll.style.paddingLeft  = scroll.style.paddingRight = 16;
        scroll.style.paddingTop   = scroll.style.paddingBottom = 8;
        panel.Add(scroll);

        bool altRow = false;
        foreach (var (action, label) in ActionDefs)
        {
            var row = BuildActionRow(action, label, altRow);
            scroll.Add(row);
            altRow = !altRow;
        }

        // ── Footer ────────────────────────────────────────────────────────────
        var footer = new VisualElement();
        footer.style.flexDirection  = FlexDirection.Row;
        footer.style.justifyContent = Justify.SpaceBetween;
        footer.style.paddingLeft    = footer.style.paddingRight = 20;
        footer.style.paddingTop     = footer.style.paddingBottom = 16;
        footer.style.backgroundColor = new Color(0.04f, 0.04f, 0.10f, 1f);

        var backBtn  = MakeFooterButton("← Back",    new Color(0.55f, 0.15f, 0.15f, 1f));
        var resetBtn = MakeFooterButton("Reset All",  new Color(0.2f,  0.2f,  0.4f,  1f));
        backBtn.clicked  += Hide;
        resetBtn.clicked += () =>
        {
            HotkeyManager.Instance?.ResetToDefaults();
            RefreshKeyLabels();
        };
        UIStyleConfig.ApplySprite(backBtn,  s.buttonSprite, new Color(0.55f, 0.15f, 0.15f, 1f));
        UIStyleConfig.ApplySprite(resetBtn, s.buttonSprite, new Color(0.2f,  0.2f,  0.4f,  1f));
        footer.Add(backBtn);
        footer.Add(resetBtn);
        panel.Add(footer);

        // ── Listening overlay ─────────────────────────────────────────────────
        _listeningOverlay = new VisualElement();
        _listeningOverlay.style.position        = Position.Absolute;
        _listeningOverlay.style.left = _listeningOverlay.style.top =
        _listeningOverlay.style.right = _listeningOverlay.style.bottom = 0;
        _listeningOverlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.85f);
        _listeningOverlay.style.alignItems      = Align.Center;
        _listeningOverlay.style.justifyContent  = Justify.Center;
        _listeningOverlay.style.display         = DisplayStyle.None;

        _listeningLabel = new Label("Press any key…");
        _listeningLabel.style.fontSize                = 28;
        _listeningLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _listeningLabel.style.color                   = Color.white;
        _listeningLabel.style.unityTextAlign          = TextAnchor.MiddleCenter;
        _listeningLabel.style.whiteSpace              = WhiteSpace.Normal;
        _listeningOverlay.Add(_listeningLabel);
        panel.Add(_listeningOverlay);
    }

    // Builds one action row (label + current key + Change button)
    private VisualElement BuildActionRow(HotkeyAction action, string displayName, bool altRow)
    {
        var row = MakeRow(altRow ? new Color(0.07f, 0.09f, 0.18f, 0.5f) : Color.clear);
        row.style.paddingTop = row.style.paddingBottom = 6;

        // Action name
        AddCell(row, displayName, 220, FontStyle.Normal, new Color(0.85f, 0.85f, 0.9f));

        // Current key label (stored for refresh)
        string currentKey = HotkeyManager.Instance?.GetDisplayName(action) ?? "—";
        var keyLbl = new Label(currentKey);
        keyLbl.style.fontSize                = 15;
        keyLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
        keyLbl.style.color                   = new Color(1f, 0.9f, 0.5f);
        keyLbl.style.width                   = 160;
        keyLbl.style.unityTextAlign          = TextAnchor.MiddleCenter;
        row.Add(keyLbl);
        _keyLabels[action] = keyLbl;

        // Change button
        var changeBtn = new Button();
        changeBtn.text = "Change";
        changeBtn.style.width        = 110;
        changeBtn.style.height       = 30;
        changeBtn.style.fontSize     = 13;
        changeBtn.style.color        = Color.white;
        changeBtn.style.borderTopLeftRadius = changeBtn.style.borderTopRightRadius =
        changeBtn.style.borderBottomLeftRadius = changeBtn.style.borderBottomRightRadius = 4;
        var s = UIStyleConfig.Load();
        UIStyleConfig.ApplySprite(changeBtn, s?.buttonSprite, new Color(0.2f, 0.3f, 0.55f, 1f));
        string dn = displayName;
        changeBtn.clicked += () =>
        {
            AudioManager.PlayUIClick();
            BeginListen(action, dn);
        };
        row.Add(changeBtn);

        return row;
    }

    private static VisualElement MakeRow(Color? bgColor)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems    = Align.Center;
        row.style.paddingLeft   = row.style.paddingRight = 4;
        row.style.borderBottomWidth = 1;
        row.style.borderBottomColor = new Color(0.2f, 0.2f, 0.35f, 0.4f);
        if (bgColor.HasValue && bgColor.Value.a > 0f)
            row.style.backgroundColor = bgColor.Value;
        return row;
    }

    private static void AddCell(VisualElement row, string text, int width,
                                FontStyle style, Color color)
    {
        var lbl = new Label(text);
        lbl.style.width                   = width;
        lbl.style.fontSize                = 14;
        lbl.style.unityFontStyleAndWeight = style;
        lbl.style.color                   = color;
        lbl.style.unityTextAlign          = TextAnchor.MiddleLeft;
        lbl.style.paddingLeft             = 4;
        row.Add(lbl);
    }

    private static Button MakeFooterButton(string text, Color bgColor)
    {
        var btn = new Button();
        btn.text = text;
        btn.style.width    = 140;
        btn.style.height   = 40;
        btn.style.fontSize = 16;
        btn.style.color    = Color.white;
        btn.style.borderTopLeftRadius    = btn.style.borderTopRightRadius    = 5;
        btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 5;
        btn.style.backgroundColor = bgColor;
        return btn;
    }
}
