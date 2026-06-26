using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Small tooltip that follows the mouse cursor and shows the AP cost of moving
/// to the currently hovered tile while the player is in movement mode.
///
/// Usage (from InputManager):
///   MoveCostHint.Show("Move cost: 2 AP");
///   MoveCostHint.Hide();
///
/// Auto-created singleton — no prefab or scene setup needed.
/// Uses UI Toolkit — resolution-independent at any screen size.
/// </summary>
public class MoveCostHint : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────

    public static MoveCostHint Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        new GameObject("MoveCostHint").AddComponent<MoveCostHint>();
    }

    // ── UI References ─────────────────────────────────────────────────────────

    private UIDocument    _doc;
    private VisualElement _panel;
    private Label         _label;
    private bool          _visible;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
    }

    private void Update()
    {
        if (!_visible || _panel == null) return;

        Vector2 mp = Mouse.current?.position.ReadValue() ?? Vector2.zero;

        // Mouse.position: X left→right, Y bottom→top (Unity/Input System screen space).
        // UIToolkit:      X left→right, Y top→bottom — must flip Y explicitly.
        // Scale by the ratio of panel size to screen size to handle any reference resolution
        // set in PanelSettings (e.g. 1920×1080 reference on a 2560×1440 screen).
        var root = _doc?.rootVisualElement;
        Rect rect = (root != null && root.contentRect.width > 0)
            ? root.contentRect
            : new Rect(0, 0, Screen.width, Screen.height);

        float scaleX = rect.width  / Screen.width;
        float scaleY = rect.height / Screen.height;

        _panel.style.left = mp.x * scaleX + 16;
        _panel.style.top  = (Screen.height - mp.y) * scaleY + 16;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Shows the hint at the current mouse position with the given text.</summary>
    public static void Show(string text)
    {
        if (Instance == null) return;
        Instance._label.text = text;
        Instance._panel.style.display = DisplayStyle.Flex;
        Instance._visible = true;
    }

    /// <summary>Hides the hint.</summary>
    public static void Hide()
    {
        if (Instance == null) return;
        Instance._panel.style.display = DisplayStyle.None;
        Instance._visible = false;
    }

    // ── UI Construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        var s = UIStyleConfig.Load();
        _doc = gameObject.AddComponent<UIDocument>();
        _doc.panelSettings = s?.panelSettings;
        _doc.sortingOrder  = 600;

        var root = _doc.rootVisualElement;
        root.pickingMode = PickingMode.Ignore;
        root.style.position = Position.Absolute;
        root.style.left = 0; root.style.top = 0; root.style.right = 0; root.style.bottom = 0;

        // Panel — position driven every frame in Update()
        _panel = new VisualElement();
        _panel.pickingMode = PickingMode.Ignore;
        _panel.style.position = Position.Absolute;
        _panel.style.left     = 0;
        _panel.style.top      = 0;
        _panel.style.backgroundColor       = new Color(0.04f, 0.10f, 0.04f, 0.92f);
        _panel.style.paddingTop            = 8;  _panel.style.paddingBottom = 8;
        _panel.style.paddingLeft           = 14; _panel.style.paddingRight  = 14;
        _panel.style.borderTopLeftRadius    = 4; _panel.style.borderTopRightRadius    = 4;
        _panel.style.borderBottomLeftRadius = 4; _panel.style.borderBottomRightRadius = 4;
        _panel.style.display = DisplayStyle.None;

        _label = new Label();
        _label.pickingMode = PickingMode.Ignore;
        _label.style.fontSize                = 22;
        _label.style.unityFontStyleAndWeight = FontStyle.Bold;
        _label.style.color                   = new Color(0.6f, 1f, 0.6f, 1f);
        _label.style.unityTextAlign          = TextAnchor.MiddleCenter;
        _label.style.whiteSpace              = WhiteSpace.NoWrap;

        _panel.Add(_label);
        root.Add(_panel);
    }
}
