using UnityEngine;

/// <summary>
/// Designer-editable audio clip library for the game's SFX system.
/// Create one via Assets → Create → Evermore → SFX Config, then assign it to
/// the AudioManager component in the scene.
///
/// Attack-specific clips are assigned per-attack on each AttackData ScriptableObject.
/// Clips here are used as fallbacks or for categories that aren't per-attack.
/// </summary>
[CreateAssetMenu(fileName = "SFXConfig", menuName = "Evermore/SFX Config")]
public class SFXConfig : ScriptableObject
{
    [Header("Combat — Fallbacks")]
    [Tooltip("Played when an attack hits and the AttackData has no specific clip assigned.")]
    public AudioClip genericHitClip;

    [Tooltip("Played when an attack misses or is dodged.")]
    public AudioClip missClip;

    [Tooltip("Played when any monster is defeated.")]
    public AudioClip monsterDeathClip;

    [Header("Outcome")]
    [Tooltip("Played when the player wins the battle.")]
    public AudioClip victoryClip;

    [Tooltip("Played when the player loses the battle.")]
    public AudioClip defeatClip;

    [Header("UI")]
    [Tooltip("Short click sound for any button press.")]
    public AudioClip uiClickClip;

    [Tooltip("Optional hover sound when the cursor enters a button or card.")]
    public AudioClip uiHoverClip;

    [Header("Movement")]
    [Tooltip("Footstep / movement sound played while any unit is walking.")]
    public AudioClip movementClip;

    [Range(0.1f, 2f)]
    [Tooltip("Seconds between each footstep sound while moving.")]
    public float movementStepInterval = 0.4f;

    [Header("Music")]
    [Tooltip("Background music loop for the main menu scene.")]
    public AudioClip menuMusicClip;

    [Tooltip("Background music loop for combat / overworld scenes (optional — can be set per-scene via AudioManager.PlayGameMusic).")]
    public AudioClip gameMusicClip;

    [Header("Volume — Caps (set by design team)")]
    [Range(0f, 1f)]
    [Tooltip("Hard ceiling for SFX volume. The in-game slider's '100' maps to this value.")]
    public float sfxVolumeMax = 1.0f;

    [Range(0f, 1f)]
    [Tooltip("Hard ceiling for music volume. The in-game slider's '100' maps to this value.")]
    public float musicVolumeMax = 0.3f;

    [Header("Volume — Player Preferences (0–1, driven by the in-game slider)")]
    [Range(0f, 1f)]
    [Tooltip("Normalised slider position saved at runtime. Do not edit by hand.")]
    public float sfxVolume = 0.8f;

    [Range(0f, 1f)]
    [Tooltip("Normalised slider position saved at runtime. Do not edit by hand.")]
    public float musicVolume = 0.7f;
}
