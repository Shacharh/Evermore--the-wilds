using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// ── Action enum ───────────────────────────────────────────────────────────────

/// <summary>Every rebindable action in the game.</summary>
public enum HotkeyAction
{
    // Always-active
    EndTurn,
    Cancel,
    Pause,

    // Monster-selected
    Move,
    Attack,
    Info,

    // Attack-menu-open
    Attack1,
    Attack2,
    Attack3,
}

// ── Manager ───────────────────────────────────────────────────────────────────

/// <summary>
/// Singleton that owns all key bindings.
///
/// Priority (highest → lowest):
///   1. Player overrides  — saved in PlayerPrefs, override defaults per-action.
///   2. HotkeyConfig SO   — assigned in Inspector, edited by the level designer.
///   3. Hard-coded fallback — used if no HotkeyConfig is assigned.
///
/// Use <see cref="WasPressedThisFrame"/> from other scripts instead of reading
/// Keyboard.current directly, so rebinding "just works" everywhere.
/// </summary>
public class HotkeyManager : MonoBehaviour
{
    public static HotkeyManager Instance { get; private set; }

    [SerializeField]
    [Tooltip("Assign the HotkeyConfig ScriptableObject created for this project.")]
    private HotkeyConfig defaultConfig;

    private readonly Dictionary<HotkeyAction, Key> _bindings = new();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadBindings();
    }

    // ── Binding load / save ───────────────────────────────────────────────────

    private void LoadBindings()
    {
        // Step 1 — start from designer defaults (or hardcoded if none assigned)
        if (defaultConfig != null)
        {
            _bindings[HotkeyAction.EndTurn] = defaultConfig.endTurn;
            _bindings[HotkeyAction.Cancel]  = defaultConfig.cancel;
            _bindings[HotkeyAction.Pause]   = defaultConfig.pause;
            _bindings[HotkeyAction.Move]    = defaultConfig.move;
            _bindings[HotkeyAction.Attack]  = defaultConfig.attack;
            _bindings[HotkeyAction.Info]    = defaultConfig.info;
            _bindings[HotkeyAction.Attack1] = defaultConfig.attack1;
            _bindings[HotkeyAction.Attack2] = defaultConfig.attack2;
            _bindings[HotkeyAction.Attack3] = defaultConfig.attack3;
        }
        else
        {
            Debug.LogWarning("[HotkeyManager] No HotkeyConfig assigned — using hardcoded defaults.");
            _bindings[HotkeyAction.EndTurn] = Key.Space;
            _bindings[HotkeyAction.Cancel]  = Key.Backspace;
            _bindings[HotkeyAction.Pause]   = Key.Escape;
            _bindings[HotkeyAction.Move]    = Key.M;
            _bindings[HotkeyAction.Attack]  = Key.T;
            _bindings[HotkeyAction.Info]    = Key.I;
            _bindings[HotkeyAction.Attack1] = Key.Digit1;
            _bindings[HotkeyAction.Attack2] = Key.Digit2;
            _bindings[HotkeyAction.Attack3] = Key.Digit3;
        }

        // Step 2 — layer player overrides on top
        foreach (HotkeyAction action in System.Enum.GetValues(typeof(HotkeyAction)))
        {
            string prefKey = "Hotkey_" + action;
            if (PlayerPrefs.HasKey(prefKey))
                _bindings[action] = (Key)PlayerPrefs.GetInt(prefKey);
        }
    }

    // ── Public query API ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the key bound to <paramref name="action"/> was pressed
    /// this frame.  Safe to call even when the keyboard is not connected.
    /// </summary>
    public bool WasPressedThisFrame(HotkeyAction action)
    {
        if (Keyboard.current == null) return false;
        if (!_bindings.TryGetValue(action, out Key key) || key == Key.None) return false;
        return Keyboard.current[key].wasPressedThisFrame;
    }

    /// <summary>Returns the Key currently bound to <paramref name="action"/>.</summary>
    public Key GetKey(HotkeyAction action)
        => _bindings.TryGetValue(action, out Key k) ? k : Key.None;

    // ── Rebinding ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Binds <paramref name="action"/> to <paramref name="newKey"/> and persists
    /// the choice in PlayerPrefs so it survives across sessions.
    /// </summary>
    public void Rebind(HotkeyAction action, Key newKey)
    {
        _bindings[action] = newKey;
        PlayerPrefs.SetInt("Hotkey_" + action, (int)newKey);
        PlayerPrefs.Save();
        Debug.Log($"[HotkeyManager] {action} rebound to {KeyDisplayName(newKey)}.");
    }

    /// <summary>
    /// Clears all player overrides from PlayerPrefs and reloads the designer
    /// defaults from the HotkeyConfig.
    /// </summary>
    public void ResetToDefaults()
    {
        foreach (HotkeyAction action in System.Enum.GetValues(typeof(HotkeyAction)))
            PlayerPrefs.DeleteKey("Hotkey_" + action);
        LoadBindings();
        Debug.Log("[HotkeyManager] All bindings reset to defaults.");
    }

    // ── Display helpers ───────────────────────────────────────────────────────

    /// <summary>Human-readable label for the key bound to <paramref name="action"/>.</summary>
    public string GetDisplayName(HotkeyAction action) => KeyDisplayName(GetKey(action));

    /// <summary>Human-readable label for any <see cref="Key"/> value.</summary>
    public static string KeyDisplayName(Key key) => key switch
    {
        Key.None        => "—",
        Key.Space       => "Space",
        Key.Backspace   => "Backspace",
        Key.Escape      => "Escape",
        Key.Enter       => "Enter",
        Key.Tab         => "Tab",
        Key.CapsLock    => "Caps",
        Key.LeftShift   => "L-Shift",
        Key.RightShift  => "R-Shift",
        Key.LeftCtrl    => "L-Ctrl",
        Key.RightCtrl   => "R-Ctrl",
        Key.LeftAlt     => "L-Alt",
        Key.RightAlt    => "R-Alt",
        Key.Digit0      => "0",
        Key.Digit1      => "1",
        Key.Digit2      => "2",
        Key.Digit3      => "3",
        Key.Digit4      => "4",
        Key.Digit5      => "5",
        Key.Digit6      => "6",
        Key.Digit7      => "7",
        Key.Digit8      => "8",
        Key.Digit9      => "9",
        Key.UpArrow     => "↑",
        Key.DownArrow   => "↓",
        Key.LeftArrow   => "←",
        Key.RightArrow  => "→",
        Key.Delete      => "Delete",
        Key.Insert      => "Insert",
        Key.Home        => "Home",
        Key.End         => "End",
        Key.PageUp      => "Page↑",
        Key.PageDown    => "Page↓",
        Key.F1  => "F1",  Key.F2  => "F2",  Key.F3  => "F3",  Key.F4  => "F4",
        Key.F5  => "F5",  Key.F6  => "F6",  Key.F7  => "F7",  Key.F8  => "F8",
        Key.F9  => "F9",  Key.F10 => "F10", Key.F11 => "F11", Key.F12 => "F12",
        _               => key.ToString()   // A–Z and everything else looks fine as-is
    };
}
