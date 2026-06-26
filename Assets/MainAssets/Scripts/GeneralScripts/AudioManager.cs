using UnityEngine;

/// <summary>
/// Central audio manager — place one instance in the combat scene.
/// Assign an SFXConfig ScriptableObject in the Inspector.
///
/// Integration points for Peleg / audio designer:
///   • Attack SFX : set "SFX Clip" on each AttackData asset (drag your .wav/.mp3 there).
///                  AudioManager.PlayAttackSFX() prefers that clip, falls back to genericHitClip.
///   • UI clicks  : call AudioManager.PlayUIClick() inside any button-click handler.
///   • Victory    : call AudioManager.PlayVictory() in WinLoseManager when victory panel shows.
///   • Defeat     : call AudioManager.PlayDefeat() in WinLoseManager when defeat panel shows.
///   • Music      : attach an AudioSource with your music clip to the AudioManager GameObject,
///                  set it to loop, then control its volume via SFXConfig.musicVolume.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField]
    [Tooltip("Drag the SFXConfig ScriptableObject here.\n" +
             "Create it via Assets → Create → Evermore → SFX Config.")]
    private SFXConfig config;

    private AudioSource _sfxSource;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _sfxSource              = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake  = false;
        _sfxSource.spatialBlend = 0f; // 2D — no positional audio needed for a turn-based game
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── Public static API ──────────────────────────────────────────────────────

    /// <summary>
    /// Play the SFX clip from <paramref name="attackData"/> if one is assigned,
    /// otherwise fall back to the generic hit sound.
    /// Call this whenever an attack hits a target.
    /// </summary>
    public static void PlayAttackSFX(AttackData attackData)
    {
        if (Instance == null || Instance.config == null) return;
        AudioClip clip = (attackData != null && attackData.SFXClip != null)
            ? attackData.SFXClip
            : Instance.config.genericHitClip;
        Play(clip);
    }

    /// <summary>Play the miss / dodge sound.</summary>
    public static void PlayMiss()   => Play(Instance?.config?.missClip);

    /// <summary>Play the monster-death sound.</summary>
    public static void PlayDeath()  => Play(Instance?.config?.monsterDeathClip);

    /// <summary>Play the victory fanfare. Call from WinLoseManager on victory.</summary>
    public static void PlayVictory() => Play(Instance?.config?.victoryClip);

    /// <summary>Play the defeat sting. Call from WinLoseManager on defeat.</summary>
    public static void PlayDefeat()  => Play(Instance?.config?.defeatClip);

    /// <summary>Play the UI button-click sound. Wire to all button onClick events.</summary>
    public static void PlayUIClick() => Play(Instance?.config?.uiClickClip);

    /// <summary>Play the UI hover sound (optional). Wire to PointerEnter events on cards.</summary>
    public static void PlayUIHover() => Play(Instance?.config?.uiHoverClip);

    /// <summary>Play any arbitrary clip at the configured SFX volume.</summary>
    public static void PlaySFX(AudioClip clip) => Play(clip);

    // ── Internal ───────────────────────────────────────────────────────────────

    private static void Play(AudioClip clip)
    {
        if (Instance == null || clip == null) return;
        Instance._sfxSource.volume = Instance.config != null ? Instance.config.sfxVolume : 0.8f;
        Instance._sfxSource.PlayOneShot(clip);
    }
}
