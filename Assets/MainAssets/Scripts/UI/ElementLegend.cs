using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Floating element-effectiveness legend panel.
/// Shown automatically by TutorialManager during the type-effectiveness lesson,
/// and accessible from the HUD at any time.
///
/// Usage:
///   ElementLegend.ShowLegend(uiRoot);
///   ElementLegend.HideLegend();
/// </summary>
public static class ElementLegend
{
    private static VisualElement _wrapper; // full-screen flex container (for centering)
    private static VisualElement _panel;
    private static VisualElement _root;

    // ── Public API ─────────────────────────────────────────────────────────────

    public static bool IsVisible => _wrapper != null &&
                                    _wrapper.style.display == DisplayStyle.Flex;

    public static void ShowLegend(VisualElement uiRoot)
    {
        if (uiRoot == null) return;

        if (_wrapper == null || _root != uiRoot)
        {
            _root = uiRoot;
            BuildPanel(uiRoot);
        }

        _wrapper.style.display = DisplayStyle.Flex;
    }

    public static void HideLegend()
    {
        if (_wrapper != null)
            _wrapper.style.display = DisplayStyle.None;
    }

    public static void Toggle(VisualElement uiRoot)
    {
        if (IsVisible) HideLegend();
        else           ShowLegend(uiRoot);
    }

    // ── UI Construction ────────────────────────────────────────────────────────

    private static void BuildPanel(VisualElement root)
    {
        _wrapper?.RemoveFromHierarchy();

        var s = UIStyleConfig.Load();

        // Full-screen transparent wrapper — centres its single child via flex.
        // This is more reliable than translate(-50%,-50%) with UI Toolkit sprites.
        _wrapper = new VisualElement();
        _wrapper.pickingMode = PickingMode.Ignore;
        _wrapper.style.position        = Position.Absolute;
        _wrapper.style.left            = 0; _wrapper.style.top    = 0;
        _wrapper.style.right           = 0; _wrapper.style.bottom = 0;
        _wrapper.style.alignItems      = Align.Center;
        _wrapper.style.justifyContent  = Justify.Center;

        _panel = new VisualElement();
        _panel.pickingMode = PickingMode.Position;
        _panel.style.width   = 300;
        _panel.style.overflow = Overflow.Hidden;
        _panel.style.paddingLeft   = 18; _panel.style.paddingRight  = 18;
        _panel.style.paddingTop    = 14; _panel.style.paddingBottom = 14;
        _panel.style.borderTopLeftRadius    = 8;
        _panel.style.borderTopRightRadius   = 8;
        _panel.style.borderBottomLeftRadius = 8;
        _panel.style.borderBottomRightRadius= 8;
        UIStyleConfig.ApplySprite(_panel, s?.panelSprite, new Color(0.05f, 0.05f, 0.12f, 0.97f));

        // Title row
        var titleRow = new VisualElement();
        titleRow.style.flexDirection  = FlexDirection.Row;
        titleRow.style.justifyContent = Justify.SpaceBetween;
        titleRow.style.alignItems     = Align.Center;
        titleRow.style.marginBottom   = 10;

        var title = new Label("Element Legend");
        title.style.fontSize                = 18;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.color                   = new Color(0.25f, 0.85f, 1f, 1f);
        title.pickingMode = PickingMode.Ignore;

        var closeBtn = new Button(HideLegend) { text = "✕" };
        closeBtn.style.fontSize            = 14;
        closeBtn.style.color               = Color.white;
        closeBtn.style.backgroundColor     = new Color(0.5f, 0.1f, 0.1f, 1f);
        closeBtn.style.width               = 24;
        closeBtn.style.height              = 24;
        closeBtn.style.borderTopWidth      = 0; closeBtn.style.borderBottomWidth = 0;
        closeBtn.style.borderLeftWidth     = 0; closeBtn.style.borderRightWidth  = 0;
        closeBtn.style.borderTopLeftRadius     = 4; closeBtn.style.borderTopRightRadius     = 4;
        closeBtn.style.borderBottomLeftRadius  = 4; closeBtn.style.borderBottomRightRadius  = 4;

        titleRow.Add(title);
        titleRow.Add(closeBtn);
        _panel.Add(titleRow);

        // Divider
        var div = new VisualElement();
        div.style.height          = 1;
        div.style.backgroundColor = new Color(1f, 1f, 1f, 0.15f);
        div.style.marginBottom    = 10;
        _panel.Add(div);

        // All 5 effectiveness tiers (colours match InputManager.EffectivenessToOutlineColor)
        AddSectionHeader(_panel, "Attack Effectiveness");
        AddEffectivenessRow(_panel, new Color(1f,  0.84f, 0f,   1f), "Super Effective", "2× damage");
        AddEffectivenessRow(_panel, new Color(1f,  0.55f, 0f,   1f), "Effective",        "1.5× damage");
        AddEffectivenessRow(_panel, new Color(0.9f, 0.9f, 0.9f, 1f), "Neutral",          "1× damage");
        AddEffectivenessRow(_panel, new Color(0.3f, 0.5f, 1f,   1f), "Weak",             "0.75× damage");
        AddEffectivenessRow(_panel, new Color(0.6f, 0.2f, 1f,   1f), "Super Weak",       "0.5× damage");

        var spacer = new VisualElement();
        spacer.style.height = 10;
        _panel.Add(spacer);

        AddSectionHeader(_panel, "Common Elements");
        AddElementRow(_panel, "🔥", "Fire",    "Strong vs. Nature & Ice");
        AddElementRow(_panel, "🌿", "Nature",  "Strong vs. Water & Earth");
        AddElementRow(_panel, "💧", "Water",   "Strong vs. Fire & Earth");
        AddElementRow(_panel, "⚡", "Thunder", "Strong vs. Water & Metal");
        AddElementRow(_panel, "⚙",  "Metal",   "Strong vs. Nature & Ice");
        AddElementRow(_panel, "🌑", "Shadow",  "Strong vs. Light & Nature");

        _wrapper.Add(_panel);
        root.Add(_wrapper);
    }

    private static void AddSectionHeader(VisualElement parent, string text)
    {
        var lbl = new Label(text);
        lbl.style.fontSize                = 13;
        lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
        lbl.style.color                   = new Color(0.6f, 0.85f, 1f, 1f);
        lbl.style.marginBottom            = 5;
        lbl.pickingMode = PickingMode.Ignore;
        parent.Add(lbl);
    }

    private static void AddEffectivenessRow(VisualElement parent, Color dotColor, string label, string desc)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems    = Align.Center;
        row.style.marginBottom  = 4;

        var dot = new VisualElement();
        dot.style.width               = 14;
        dot.style.height              = 14;
        dot.style.minWidth            = 14; // prevent flex squeeze
        dot.style.borderTopLeftRadius = dot.style.borderTopRightRadius =
        dot.style.borderBottomLeftRadius = dot.style.borderBottomRightRadius = 7;
        dot.style.backgroundColor     = dotColor;
        dot.style.marginRight         = 8;
        dot.pickingMode = PickingMode.Ignore;

        var nameLabel = new Label($"<b>{label}</b>");
        nameLabel.style.fontSize   = 13;
        nameLabel.style.color      = Color.white;
        nameLabel.style.width      = 106;
        nameLabel.style.minWidth   = 106;
        nameLabel.pickingMode = PickingMode.Ignore;

        var descLabel = new Label(desc);
        descLabel.style.fontSize   = 12;
        descLabel.style.color      = new Color(0.7f, 0.7f, 0.7f, 1f);
        descLabel.style.whiteSpace = WhiteSpace.Normal;
        descLabel.style.flexGrow   = 1;
        descLabel.pickingMode = PickingMode.Ignore;

        row.Add(dot);
        row.Add(nameLabel);
        row.Add(descLabel);
        parent.Add(row);
    }

    private static void AddElementRow(VisualElement parent, string icon, string element, string notes)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems    = Align.Center;
        row.style.marginBottom  = 3;

        var iconLbl = new Label(icon);
        iconLbl.style.fontSize   = 14;
        iconLbl.style.width      = 22;
        iconLbl.style.minWidth   = 22;
        iconLbl.pickingMode = PickingMode.Ignore;

        var nameLbl = new Label($"<b>{element}</b>");
        nameLbl.style.fontSize  = 13;
        nameLbl.style.color     = Color.white;
        nameLbl.style.width     = 62;
        nameLbl.style.minWidth  = 62;
        nameLbl.pickingMode = PickingMode.Ignore;

        var notesLbl = new Label(notes);
        notesLbl.style.fontSize  = 11;
        notesLbl.style.color     = new Color(0.7f, 0.7f, 0.7f, 1f);
        notesLbl.style.whiteSpace = WhiteSpace.Normal;
        notesLbl.style.flexGrow  = 1;
        notesLbl.pickingMode = PickingMode.Ignore;

        row.Add(iconLbl);
        row.Add(nameLbl);
        row.Add(notesLbl);
        parent.Add(row);
    }
}
