using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Designer-editable default key bindings.
/// Create one via Assets → Create → Evermore → Hotkey Config,
/// then assign it to the HotkeyManager in the scene.
///
/// Players can override any binding at runtime through the settings menu;
/// those overrides are saved in PlayerPrefs and loaded on top of these defaults.
/// </summary>
[CreateAssetMenu(fileName = "HotkeyConfig", menuName = "Evermore/Hotkey Config")]
public class HotkeyConfig : ScriptableObject
{
    [Header("Always active")]
    [Tooltip("Pass / end the player's turn.")]
    public Key endTurn = Key.Space;

    [Tooltip("Cancel / go back one menu level.")]
    public Key cancel  = Key.Backspace;

    [Tooltip("Open the pause menu.")]
    public Key pause   = Key.Escape;

    [Header("When a friendly monster is selected")]
    [Tooltip("Enter movement mode for the selected monster.")]
    public Key move   = Key.M;

    [Tooltip("Open the attack selection menu.")]
    public Key attack = Key.T;

    [Tooltip("Show the monster's info panel.")]
    public Key info   = Key.I;

    [Header("When the attack menu is open")]
    [Tooltip("Use the monster's first attack.")]
    public Key attack1 = Key.Digit1;

    [Tooltip("Use the monster's second attack.")]
    public Key attack2 = Key.Digit2;

    [Tooltip("Use the monster's third attack.")]
    public Key attack3 = Key.Digit3;
}
