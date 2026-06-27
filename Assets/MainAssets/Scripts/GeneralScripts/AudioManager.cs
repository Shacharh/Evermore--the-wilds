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
    private AudioSource _musicSource;

    // Cached config values — used in Update to detect changes without re-applying every frame.
    private float _cachedSfxVolume      = -1f;
    private float _cachedSfxVolumeMax   = -1f;
    private float _cachedMusicVolume    = -1f;
    private float _cachedMusicVolumeMax = -1f;

    // Square-law curve: perceived loudness scales with t², matching the dB falloff curve.
    // t is normalised 0–1 (the UI shows 0–100 but divides before calling these).
    // The ceiling comes from SFXConfig so the design team can tune it without touching code.
    private float SFXLinear(float t)   => t <= 0f ? 0f : t * t * (config != null ? config.sfxVolumeMax   : 1.0f);
    private float MusicLinear(float t) => t <= 0f ? 0f : t * t * (config != null ? config.musicVolumeMax : 0.3f);

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
        _sfxSource.spatialBlend = 0f;

        _musicSource              = gameObject.AddComponent<AudioSource>();
        _musicSource.playOnAwake  = false;
        _musicSource.spatialBlend = 0f;
        _musicSource.loop         = true;

        ApplyVolumes();
    }

    // Applies the converted (square-law + cap) volumes to every AudioSource on this GameObject.
    // Called on startup so manually-placed Inspector sources are capped from the first frame,
    // not only after the player touches the slider.
    private void ApplyVolumes()
    {
        if (config == null) return;
        _sfxSource.volume = SFXLinear(config.sfxVolume);
        float musicLinear = MusicLinear(config.musicVolume);
        foreach (AudioSource src in GetComponents<AudioSource>())
            if (src != _sfxSource)
                src.volume = musicLinear;

        _cachedSfxVolume      = config.sfxVolume;
        _cachedSfxVolumeMax   = config.sfxVolumeMax;
        _cachedMusicVolume    = config.musicVolume;
        _cachedMusicVolumeMax = config.musicVolumeMax;
    }

    private void Update()
    {
        if (config == null) return;
        if (config.sfxVolume      == _cachedSfxVolume      &&
            config.sfxVolumeMax   == _cachedSfxVolumeMax   &&
            config.musicVolume    == _cachedMusicVolume     &&
            config.musicVolumeMax == _cachedMusicVolumeMax) return;

        ApplyVolumes();
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

    /// <summary>
    /// Play a single footstep tick for unit movement.
    /// PlayerMovement calls this on a timed interval while the player is moving.
    /// </summary>
    public static void PlayMovementStep() => Play(Instance?.config?.movementClip);

    /// <summary>How many seconds between footstep sounds (read from SFXConfig).</summary>
    public static float MovementStepInterval =>
        Instance?.config != null ? Instance.config.movementStepInterval : 0.4f;

    /// <summary>
    /// Start the menu background music loop.
    /// Call this from your main-menu scene (e.g. via MenuSceneAudio component).
    /// </summary>
    public static void PlayMenuMusic()  => PlayMusic(Instance?.config?.menuMusicClip);

    /// <summary>
    /// Start the in-game background music loop.
    /// Assign <c>gameMusicClip</c> on SFXConfig, then call this when entering the game scene.
    /// </summary>
    public static void PlayGameMusic()  => PlayMusic(Instance?.config?.gameMusicClip);

    /// <summary>Exposes the SFXConfig so the pause menu can read current volumes.</summary>
    public static SFXConfig Config => Instance?.config;

    /// <summary>
    /// Set SFX volume from a 0–100 slider value.
    /// Internally converts via square-law curve to a linear AudioSource volume.
    /// </summary>
    public static void SetSFXVolume(float slider100)
    {
        if (Instance?.config == null) return;
        Instance.config.sfxVolume  = Mathf.Clamp01(slider100 / 100f);
        Instance._sfxSource.volume = Instance.SFXLinear(Instance.config.sfxVolume);
    }

    /// <summary>
    /// Set music volume from a 0–100 slider value.
    /// Internally converts via square-law curve and caps at MaxMusicLinear (0.3).
    /// Updates every non-SFX AudioSource on this GameObject (covers manually-placed sources too).
    /// </summary>
    public static void SetMusicVolume(float slider100)
    {
        if (Instance?.config == null) return;
        Instance.config.musicVolume = Mathf.Clamp01(slider100 / 100f);
        float linear = Instance.MusicLinear(Instance.config.musicVolume);
        foreach (AudioSource src in Instance.GetComponents<AudioSource>())
        {
            if (src != Instance._sfxSource)
                src.volume = linear;
        }
    }

    /// <summary>Stop whichever music track is currently playing.</summary>
    public static void StopMusic()
    {
        if (Instance?._musicSource != null) Instance._musicSource.Stop();
    }

    // ── Internal ───────────────────────────────────────────────────────────────

    private static void Play(AudioClip clip)
    {
        if (Instance == null || clip == null) return;
        Instance._sfxSource.volume = Instance.SFXLinear(Instance.config?.sfxVolume ?? 0.8f);
        Instance._sfxSource.PlayOneShot(clip);
    }

    private static void PlayMusic(AudioClip clip)
    {
        if (Instance == null || clip == null) return;
        if (Instance._musicSource.clip == clip && Instance._musicSource.isPlaying) return;
        Instance._musicSource.clip   = clip;
        Instance._musicSource.volume = Instance.MusicLinear(Instance.config?.musicVolume ?? 0.7f);
        Instance._musicSource.Play();
    }
}
