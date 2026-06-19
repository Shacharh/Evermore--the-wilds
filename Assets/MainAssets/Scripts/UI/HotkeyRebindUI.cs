using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Powers the key-rebinding panel in the Settings / Pause menu.
///
/// Setup steps (do once in the Editor):
///   1. Create a panel GameObject with this script attached.
///   2. Fill the "Rows" array — one entry per action.
///      Each entry needs a display name, a TextMeshProUGUI for the current key,
///      and a Button the player clicks to start rebinding.
///   3. Optionally assign a "Listening Panel" (the "Press any key…" overlay)
///      and a Reset All button.
///   4. Show()/Hide() the panel from your pause menu controller.
/// </summary>
public class HotkeyRebindUI : MonoBehaviour
{
    // ── Row data ──────────────────────────────────────────────────────────────

    [System.Serializable]
    public class HotkeyRow
    {
        [Tooltip("Which action this row controls.")]
        public HotkeyAction action;

        [Tooltip("Label shown on the left side of the row (e.g. \"Move\", \"Attack\").")]
        public string displayName;

        [Tooltip("TextMeshProUGUI that shows the currently bound key.")]
        public TextMeshProUGUI keyLabel;

        [Tooltip("Button the player clicks to start rebinding this action.")]
        public Button rebindButton;
    }

    // ── Inspector fields ──────────────────────────────────────────────────────

    [Header("Rows — one per rebindable action")]
    [SerializeField] private HotkeyRow[] rows;

    [Header("References")]
    [Tooltip("Overlay shown while waiting for the player to press a key.")]
    [SerializeField] private GameObject listeningPanel;

    [Tooltip("Optional text inside the listening overlay — updated per action.")]
    [SerializeField] private TextMeshProUGUI listeningLabel;

    [Tooltip("Resets every binding back to the designer defaults.")]
    [SerializeField] private Button resetAllButton;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired when the panel is hidden (Back button or Hide() call).</summary>
    public event System.Action onClose;

    // ── State ─────────────────────────────────────────────────────────────────

    private HotkeyAction? _listeningFor;
    private bool          _configuredByBuilder;

    // ── Builder API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Called by HotkeyPanelBuilder when the panel is constructed entirely in
    /// code.  Provides all row data and UI references so no manual Inspector
    /// wiring is needed. Start() skips re-wiring if this has been called.
    /// </summary>
    public void Configure(HotkeyRow[]      builtRows,
                          Button            resetBtn,
                          GameObject        listeningOverlay,
                          TextMeshProUGUI   listeningTxt)
    {
        rows                 = builtRows;
        resetAllButton       = resetBtn;
        listeningPanel       = listeningOverlay;
        listeningLabel       = listeningTxt;
        _configuredByBuilder = true;

        WireListeners();
        // NOTE: RefreshLabels() is intentionally NOT called here.
        // Configure() runs in Awake(), and HotkeyManager.Instance may not be
        // set yet at that point. Start() always calls RefreshLabels() instead,
        // by which time all Awake() calls across the scene are complete.
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        if (!_configuredByBuilder)
        {
            // Manual Inspector setup path
            WireListeners();
        }

        if (listeningPanel != null)
            listeningPanel.SetActive(false);

        // Always refresh here — HotkeyManager.Instance is guaranteed to exist
        // by the time Start() runs (all Awake() calls have already completed).
        RefreshLabels();
    }

    private void WireListeners()
    {
        foreach (var row in rows)
        {
            var captured = row;
            if (row.rebindButton != null)
                row.rebindButton.onClick.AddListener(() => BeginRebind(captured.action));
        }

        if (resetAllButton != null)
            resetAllButton.onClick.AddListener(ResetAll);
    }

    void Update()
    {
        if (_listeningFor == null) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        // Scan every key for a press this frame
        foreach (Key key in System.Enum.GetValues(typeof(Key)))
        {
            if (key == Key.None) continue;
            if (!kb[key].wasPressedThisFrame) continue;

            if (key == Key.Escape)
            {
                // Escape cancels the rebind — old binding is kept
                EndListen();
                return;
            }

            HotkeyManager.Instance?.Rebind(_listeningFor.Value, key);
            RefreshLabels();
            EndListen();
            return;
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Show() => gameObject.SetActive(true);

    public void Hide()
    {
        gameObject.SetActive(false);
        onClose?.Invoke();   // notify PauseMenu (or whoever opened us) to restore itself
        onClose = null;      // clear so the same handler isn't called twice
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void BeginRebind(HotkeyAction action)
    {
        _listeningFor = action;

        if (listeningPanel != null) listeningPanel.SetActive(true);

        if (listeningLabel != null)
        {
            string name = GetDisplayNameFor(action);
            listeningLabel.text = $"Binding  [ {name} ]\nPress any key…\n" +
                                  $"<size=70%>Escape to cancel</size>";
        }
    }

    private void EndListen()
    {
        _listeningFor = null;
        if (listeningPanel != null) listeningPanel.SetActive(false);
    }

    private void ResetAll()
    {
        HotkeyManager.Instance?.ResetToDefaults();
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        if (HotkeyManager.Instance == null) return;
        foreach (var row in rows)
            if (row.keyLabel != null)
                row.keyLabel.text = HotkeyManager.Instance.GetDisplayName(row.action);
    }

    private string GetDisplayNameFor(HotkeyAction action)
    {
        foreach (var row in rows)
            if (row.action == action && !string.IsNullOrEmpty(row.displayName))
                return row.displayName;
        return action.ToString();
    }
}
