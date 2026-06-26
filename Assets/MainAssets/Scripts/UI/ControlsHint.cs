using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Small controls reference shown in the bottom-left corner at battle start.
/// Key labels are read from HotkeyManager so they always match the actual bindings.
/// Auto-dismisses after the player ends their first turn, or after 20 seconds.
/// Auto-created singleton — no scene setup needed.
/// </summary>
public class ControlsHint : MonoBehaviour
{
    public static ControlsHint Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        new GameObject("ControlsHint").AddComponent<ControlsHint>();
    }

    private const float AutoDismissSeconds = 20f;

    private UIDocument _doc;
    private VisualElement _panel;
    private bool _dismissed;

    // Key label references so Start() can fill them from HotkeyManager
    private Label _endTurnKey;
    private Label _cancelKey;
    private Label _moveKey;
    private Label _attackKey;
    private Label _infoKey;
    private Label _resetCamKey;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
        StartCoroutine(AutoDismiss());
    }

    private void Start()
    {
        // HotkeyManager.Instance is guaranteed to exist by now (all Awakes have run).
        RefreshKeyLabels();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void Dismiss() => Instance?.DoDismiss();

    private void DoDismiss()
    {
        if (_dismissed) return;
        _dismissed = true;
        StartCoroutine(FadeOut());
    }

    private IEnumerator AutoDismiss()
    {
        yield return new WaitForSecondsRealtime(AutoDismissSeconds);
        DoDismiss();
    }

    private IEnumerator FadeOut()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / 0.6f;
            if (_panel != null) _panel.style.opacity = 1f - t;
            yield return null;
        }
        if (_panel != null) _panel.style.display = DisplayStyle.None;
    }

    private void RefreshKeyLabels()
    {
        var hk = HotkeyManager.Instance;
        if (hk == null) return;

        if (_endTurnKey  != null) _endTurnKey.text  = hk.GetDisplayName(HotkeyAction.EndTurn);
        if (_cancelKey   != null) _cancelKey.text   = hk.GetDisplayName(HotkeyAction.Cancel);
        if (_moveKey     != null) _moveKey.text     = hk.GetDisplayName(HotkeyAction.Move);
        if (_attackKey   != null) _attackKey.text   = hk.GetDisplayName(HotkeyAction.Attack);
        if (_infoKey     != null) _infoKey.text     = hk.GetDisplayName(HotkeyAction.Info);
        if (_resetCamKey != null) _resetCamKey.text = hk.GetDisplayName(HotkeyAction.ResetCamera);
    }

    private void BuildUI()
    {
        var s = UIStyleConfig.Load();
        _doc = gameObject.AddComponent<UIDocument>();
        _doc.panelSettings = s?.panelSettings;
        _doc.sortingOrder  = 50;

        var root = _doc.rootVisualElement;
        root.pickingMode = PickingMode.Ignore;
        root.style.position = Position.Absolute;
        root.style.left = 0; root.style.top = 0; root.style.right = 0; root.style.bottom = 0;

        _panel = new VisualElement();
        _panel.pickingMode = PickingMode.Ignore;
        _panel.style.position = Position.Absolute;
        _panel.style.right    = 20;
        _panel.style.top      = 24;   // aligns roughly with the MENU button height
        _panel.style.paddingTop    = 10; _panel.style.paddingBottom = 10;
        _panel.style.paddingLeft   = 14; _panel.style.paddingRight  = 14;
        _panel.style.backgroundColor       = new Color(0.04f, 0.06f, 0.12f, 0.88f);
        _panel.style.borderTopLeftRadius    = 5; _panel.style.borderTopRightRadius    = 5;
        _panel.style.borderBottomLeftRadius = 5; _panel.style.borderBottomRightRadius = 5;
        // Highlight border — red top edge, subtle red on sides/bottom
        _panel.style.borderTopWidth    = 2; _panel.style.borderBottomWidth = 1;
        _panel.style.borderLeftWidth   = 1; _panel.style.borderRightWidth  = 1;
        _panel.style.borderTopColor    = new Color(0.88f, 0.22f, 0.22f, 1f);
        _panel.style.borderBottomColor = new Color(0.88f, 0.22f, 0.22f, 0.35f);
        _panel.style.borderLeftColor   = new Color(0.88f, 0.22f, 0.22f, 0.35f);
        _panel.style.borderRightColor  = new Color(0.88f, 0.22f, 0.22f, 0.35f);
        _panel.style.flexDirection = FlexDirection.Column;

        var header = new Label("CONTROLS");
        header.pickingMode = PickingMode.Ignore;
        header.style.fontSize                = 10;
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.color                   = new Color(0.88f, 0.22f, 0.22f, 1f);  // red to match border
        header.style.marginBottom            = 5;
        _panel.Add(header);

        // Rows — key labels stored so Start() can fill them from HotkeyManager
        _endTurnKey  = AddRow("Space",     "End turn");
        _cancelKey   = AddRow("Backspace", "Cancel / back");
        _moveKey     = AddRow("M",         "Move (when selected)");
        _attackKey   = AddRow("T",         "Attack (when selected)");
        _infoKey     = AddRow("I",         "Monster info");
        _resetCamKey = AddRow("Home",      "Reset camera angle");
        AddRow("Right-drag", "Rotate camera", isStatic: true);
        AddRow("Click monster", "Select & open menu", isStatic: true);

        root.Add(_panel);
    }

    // Returns the key-label so the caller can store it for later update.
    // isStatic rows (like "Click monster") don't need a stored reference.
    private Label AddRow(string defaultKey, string action, bool isStatic = false)
    {
        var row = new VisualElement();
        row.pickingMode = PickingMode.Ignore;
        row.style.flexDirection = FlexDirection.Row;
        row.style.marginBottom  = 3;

        var keyLabel = new Label(defaultKey);
        keyLabel.pickingMode = PickingMode.Ignore;
        keyLabel.style.fontSize                = 13;
        keyLabel.style.unityFontStyleAndWeight = isStatic ? FontStyle.Normal : FontStyle.Bold;
        keyLabel.style.color = isStatic
            ? new Color(0.75f, 0.75f, 0.75f, 1f)
            : new Color(0.9f, 0.85f, 0.5f, 1f);
        keyLabel.style.width         = 140;
        keyLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

        var sep = new Label("→");
        sep.pickingMode = PickingMode.Ignore;
        sep.style.fontSize    = 13;
        sep.style.color       = new Color(0.5f, 0.5f, 0.5f, 1f);
        sep.style.marginLeft  = 4;
        sep.style.marginRight = 6;

        var actionLabel = new Label(action);
        actionLabel.pickingMode = PickingMode.Ignore;
        actionLabel.style.fontSize    = 13;
        actionLabel.style.color       = new Color(0.85f, 0.85f, 0.85f, 1f);

        row.Add(keyLabel);
        row.Add(sep);
        row.Add(actionLabel);
        _panel.Add(row);

        return keyLabel;
    }
}
