using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

/// <summary>
/// Small controls reference shown in the bottom-left corner at battle start.
/// Auto-dismisses after the player ends their first turn (or after a timeout).
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

    // How many seconds before it fades on its own even if the player never acts.
    private const float AutoDismissSeconds = 20f;

    private UIDocument    _doc;
    private VisualElement _panel;
    private bool          _dismissed;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
        StartCoroutine(AutoDismiss());
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Called by PlayerTurnController when the player ends their first turn.
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

    private void BuildUI()
    {
        var s = UIStyleConfig.Load();
        _doc = gameObject.AddComponent<UIDocument>();
        _doc.panelSettings = s?.panelSettings;
        _doc.sortingOrder  = 50;   // below HUD (100) so it never covers anything important

        var root = _doc.rootVisualElement;
        root.pickingMode = PickingMode.Ignore;
        root.style.position = Position.Absolute;
        root.style.left = 0; root.style.top = 0; root.style.right = 0; root.style.bottom = 0;

        // Panel — anchored bottom-left
        _panel = new VisualElement();
        _panel.pickingMode = PickingMode.Ignore;
        _panel.style.position      = Position.Absolute;
        _panel.style.left          = 20;
        _panel.style.bottom        = 20;
        _panel.style.paddingTop    = 10; _panel.style.paddingBottom = 10;
        _panel.style.paddingLeft   = 14; _panel.style.paddingRight  = 14;
        _panel.style.backgroundColor       = new Color(0f, 0f, 0f, 0.58f);
        _panel.style.borderTopLeftRadius    = 5; _panel.style.borderTopRightRadius    = 5;
        _panel.style.borderBottomLeftRadius = 5; _panel.style.borderBottomRightRadius = 5;
        _panel.style.flexDirection = FlexDirection.Column;

        // Header
        var header = new Label("CONTROLS");
        header.pickingMode = PickingMode.Ignore;
        header.style.fontSize                = 10;
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.color                   = new Color(0.55f, 0.55f, 0.65f, 1f);
        header.style.marginBottom            = 5;
        header.style.unityTextAlign          = TextAnchor.MiddleLeft;
        _panel.Add(header);

        // Control rows
        AddRow("Click monster",    "Select & act");
        AddRow("SPACE",            "End turn");
        AddRow("Right-click / ESC","Cancel");
        AddRow("Q",                "Move");
        AddRow("E",                "Attack");

        root.Add(_panel);
    }

    private void AddRow(string key, string action)
    {
        var row = new VisualElement();
        row.pickingMode = PickingMode.Ignore;
        row.style.flexDirection = FlexDirection.Row;
        row.style.marginBottom  = 3;

        var keyLabel = new Label(key);
        keyLabel.pickingMode = PickingMode.Ignore;
        keyLabel.style.fontSize                = 13;
        keyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        keyLabel.style.color                   = new Color(0.9f, 0.85f, 0.5f, 1f);
        keyLabel.style.width                   = 130;
        keyLabel.style.unityTextAlign          = TextAnchor.MiddleLeft;

        var sep = new Label("→");
        sep.pickingMode = PickingMode.Ignore;
        sep.style.fontSize    = 13;
        sep.style.color       = new Color(0.5f, 0.5f, 0.5f, 1f);
        sep.style.marginLeft  = 4;
        sep.style.marginRight = 6;
        sep.style.unityTextAlign = TextAnchor.MiddleLeft;

        var actionLabel = new Label(action);
        actionLabel.pickingMode = PickingMode.Ignore;
        actionLabel.style.fontSize    = 13;
        actionLabel.style.color       = new Color(0.85f, 0.85f, 0.85f, 1f);
        actionLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

        row.Add(keyLabel);
        row.Add(sep);
        row.Add(actionLabel);
        _panel.Add(row);
    }
}
