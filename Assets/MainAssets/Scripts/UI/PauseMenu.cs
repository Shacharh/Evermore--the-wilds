using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

/// <summary>
/// Pause menu toggled by the Pause hotkey (default: Escape).
/// Rendered via UI Toolkit. Assign PauseMenu.uxml and PanelSettings in UIStyleConfig.
/// AUTO-SETUP: creates itself at runtime — no scene placement required.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public static PauseMenu Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        new GameObject("PauseMenu").AddComponent<PauseMenu>();
    }

    // ── UI references ──────────────────────────────────────────────────────────

    private UIDocument    _uiDoc;
    private VisualElement _overlay;
    private Toggle        _edgeScrollToggle;

    // ── State ──────────────────────────────────────────────────────────────────

    private bool _isOpen;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
        SetVisible(false);
    }

    private void Update()
    {
        bool togglePressed = HotkeyManager.Instance != null
            ? HotkeyManager.Instance.WasPressedThisFrame(HotkeyAction.Pause)
            : Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

        if (togglePressed) Toggle();
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    public void Toggle() => SetVisible(!_isOpen);
    public void Open()   => SetVisible(true);
    public void Close()  => SetVisible(false);

    private void SetVisible(bool visible)
    {
        if (_overlay == null) return;
        _isOpen = visible;
        _overlay.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        Time.timeScale = visible ? 0f : 1f;

        if (visible && _edgeScrollToggle != null)
            _edgeScrollToggle.value = GameSettings.EdgeScrollEnabled;
    }

    private void OpenKeyBindings()
    {
        var hotkeyUI = Object.FindFirstObjectByType<HotkeyRebindUI>(FindObjectsInactive.Include);
        if (hotkeyUI == null)
        {
            Debug.LogWarning("[PauseMenu] HotkeyRebindUI not found.");
            return;
        }

        _overlay.style.display = DisplayStyle.None;
        hotkeyUI.onClose += RestoreFromKeyBindings;
        hotkeyUI.Show();
    }

    private void RestoreFromKeyBindings()
    {
        _overlay.style.display = DisplayStyle.Flex;
        if (_edgeScrollToggle != null)
            _edgeScrollToggle.value = GameSettings.EdgeScrollEnabled;
    }

    // ── UI Construction ────────────────────────────────────────────────────────

    private void BuildUI()
    {
        var s = UIStyleConfig.Load();

        if (s?.panelSettings == null || s?.pauseMenuUXML == null)
        {
            Debug.LogWarning("[PauseMenu] PanelSettings or UXML not assigned in UIStyleConfig — pause menu disabled.");
            return;
        }

        _uiDoc = gameObject.AddComponent<UIDocument>();
        _uiDoc.panelSettings   = s.panelSettings;
        _uiDoc.sortingOrder    = 10;
        _uiDoc.visualTreeAsset = s.pauseMenuUXML;

        var docRoot = _uiDoc.rootVisualElement;
        _overlay = docRoot.Q("overlay");

        if (_overlay == null)
        {
            Debug.LogError("[PauseMenu] 'overlay' element not found in UXML.");
            return;
        }

        var panel = docRoot.Q("panel-root");

        // Apply panel sprite / colour
        if (panel != null)
            UIStyleConfig.ApplySprite(panel, s.panelSprite, s.pausePanelColor);

        // Wire edge-scroll toggle
        _edgeScrollToggle = docRoot.Q<Toggle>("edge-scroll-toggle");
        if (_edgeScrollToggle != null)
        {
            _edgeScrollToggle.value = GameSettings.EdgeScrollEnabled;
            _edgeScrollToggle.RegisterValueChangedCallback(
                evt => GameSettings.EdgeScrollEnabled = evt.newValue);
        }

        // Wire buttons — apply sprite if set, then wire click
        var keyBindBtn = docRoot.Q<Button>("keybindings-button");
        if (keyBindBtn != null)
        {
            UIStyleConfig.ApplySprite(keyBindBtn, s.buttonSprite, s.keyBindButtonColor);
            keyBindBtn.clicked += () => { AudioManager.PlayUIClick(); OpenKeyBindings(); };
        }

        var resumeBtn = docRoot.Q<Button>("resume-button");
        if (resumeBtn != null)
        {
            UIStyleConfig.ApplySprite(resumeBtn, s.buttonSprite, s.resumeButtonColor);
            resumeBtn.clicked += () => { AudioManager.PlayUIClick(); Close(); };
        }
    }
}
