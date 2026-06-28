using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Full-screen title card that announces whose turn it is.
/// A dark horizontal band fades in at the center of the screen, holds briefly,
/// then fades out. Color-coded: blue for player, red for enemy.
///
/// FEATURE FLAG: set FeatureEnabled = true when ready to show.
///
/// AUTO-SETUP: creates itself — no scene placement needed.
/// Wire-up: TurnManager.BeginTurn calls TurnIndicator.Show(isPlayer, turnNumber).
/// </summary>
public class TurnIndicator : MonoBehaviour
{
    public static TurnIndicator Instance { get; private set; }

    // ── Feature flag — flip to true to enable ────────────────────────────────
    private const bool FeatureEnabled = true;
    // ─────────────────────────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        new GameObject("TurnIndicator").AddComponent<TurnIndicator>();
    }

    private const float FadeInDuration  = 0.25f;
    private const float HoldDuration    = 1.5f;
    private const float FadeOutDuration = 0.6f;

    private UIDocument    _doc;
    private VisualElement _band;       // full-width dark strip
    private Label         _ownerLabel; // "PLAYER TURN" / "ENEMY TURN"
    private Label         _numLabel;   // "3"
    private Coroutine     _current;

    private static readonly Color PlayerAccent = new Color(0.25f, 0.65f, 1f,    1f);
    private static readonly Color EnemyAccent  = new Color(0.90f, 0.25f, 0.25f, 1f);

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public static void Show(bool isPlayer, int turnNumber)
    {
        if (!FeatureEnabled) return;
        Instance?.DoShow(isPlayer, turnNumber);
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private void DoShow(bool isPlayer, int turnNumber)
    {
        Color accent = isPlayer ? PlayerAccent : EnemyAccent;
        _ownerLabel.text          = isPlayer ? "PLAYER TURN" : "ENEMY TURN";
        _ownerLabel.style.color   = accent;
        _numLabel.text            = turnNumber.ToString();
        _numLabel.style.color     = accent;

        SetVisible(true);
        _band.style.opacity = 0f;

        if (_current != null) StopCoroutine(_current);
        _current = StartCoroutine(FadeSequence());
    }

    private IEnumerator FadeSequence()
    {
        // Fade in
        yield return Fade(0f, 1f, FadeInDuration);

        // Hold
        yield return new WaitForSecondsRealtime(HoldDuration);

        // Fade out
        yield return Fade(1f, 0f, FadeOutDuration);

        SetVisible(false);
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / duration;
            if (_band != null) _band.style.opacity = Mathf.Lerp(from, to, Mathf.Clamp01(t));
            yield return null;
        }
    }

    private void SetVisible(bool v)
    {
        if (_band == null) return;
        _band.style.display = v ? DisplayStyle.Flex : DisplayStyle.None;
    }

    // ── UI Construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        var s = UIStyleConfig.Load();
        _doc = gameObject.AddComponent<UIDocument>();
        _doc.panelSettings = s?.panelSettings;
        _doc.sortingOrder  = 80; // above HUD and ControlsHint, below WinLose (1000)

        var root = _doc.rootVisualElement;
        root.pickingMode    = PickingMode.Ignore;
        root.style.position = Position.Absolute;
        root.style.left = 0; root.style.top = 0; root.style.right = 0; root.style.bottom = 0;
        root.style.alignItems    = Align.Center;
        root.style.justifyContent = Justify.Center;

        // Full-width dark band centered on screen
        _band = new VisualElement();
        _band.pickingMode           = PickingMode.Ignore;
        _band.style.position        = Position.Relative;
        _band.style.width           = new Length(100, LengthUnit.Percent);
        _band.style.paddingTop      = 28;
        _band.style.paddingBottom   = 28;
        _band.style.backgroundColor = new Color(0f, 0f, 0f, 0.72f);
        _band.style.flexDirection   = FlexDirection.Row;
        _band.style.alignItems      = Align.Center;
        _band.style.justifyContent  = Justify.Center;

        // "PLAYER TURN" label — smaller, letter-spaced feel via bold caps
        _ownerLabel = new Label("PLAYER TURN");
        _ownerLabel.pickingMode                    = PickingMode.Ignore;
        _ownerLabel.style.fontSize                 = 28;
        _ownerLabel.style.unityFontStyleAndWeight  = FontStyle.Bold;
        _ownerLabel.style.color                    = PlayerAccent;
        _ownerLabel.style.unityTextAlign           = TextAnchor.MiddleCenter;
        _ownerLabel.style.marginRight              = 20;

        // Turn number — large
        _numLabel = new Label("1");
        _numLabel.pickingMode                   = PickingMode.Ignore;
        _numLabel.style.fontSize                = 72;
        _numLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _numLabel.style.color                   = PlayerAccent;
        _numLabel.style.unityTextAlign          = TextAnchor.MiddleCenter;

        _band.Add(_ownerLabel);
        _band.Add(_numLabel);
        root.Add(_band);
    }
}
