using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

/// <summary>
/// Evaluates win/lose conditions and shows a result screen.
/// AUTO-SETUP: creates its own UIDocument at runtime. No scene placement required.
/// Visual style is driven by a UIStyleConfig asset in Assets/Resources/.
/// Uses UI Toolkit — resolution-independent at any screen size.
/// </summary>
public class WinLoseManager : MonoBehaviour
{
    public static WinLoseManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        if (FindFirstObjectByType<WinLoseManager>() != null) return;
        new GameObject("WinLoseManager").AddComponent<WinLoseManager>();
    }

    // ── Conditions ────────────────────────────────────────────────────────────

    [Header("Win Conditions")]
    public bool winOnAllEnemiesDefeated     = true;
    public bool winOnSurviveNRounds         = false;
    public int  survivalRoundTarget         = 10;

    [Header("Lose Conditions")]
    public bool loseOnAllPlayerMonstersDefeated = true;

    // ── State ─────────────────────────────────────────────────────────────────

    private bool          _gameOver;
    private VisualElement _overlayEl;
    private Label         _titleLabel;
    private Label         _reasonLabel;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
        SetOverlayVisible(false);
    }

    private void Start() => StartCoroutine(LateSubscribe());

    private IEnumerator LateSubscribe()
    {
        yield return new WaitForSeconds(0.7f);

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.onNewRound.RemoveListener(OnNewRound);
            TurnManager.Instance.onNewRound.AddListener(OnNewRound);
        }

        foreach (var m in FindObjectsByType<Monster>(FindObjectsSortMode.None))
            m.OnDied += OnMonsterDied;
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnMonsterDied(Monster m) { if (!_gameOver) CheckConditions(); }

    private void OnNewRound(int round)
    {
        if (_gameOver) return;
        if (winOnSurviveNRounds && round > survivalRoundTarget)
            TriggerResult(true, $"Survived {survivalRoundTarget} rounds!");
    }

    // ── Condition checks ──────────────────────────────────────────────────────

    private void CheckConditions()
    {
        var tm = TurnManager.Instance;
        if (tm == null) return;

        if (loseOnAllPlayerMonstersDefeated && tm.PlayerController != null)
        {
            bool allDead = true;
            foreach (var m in tm.PlayerController.Monsters)
                if (m != null && m.IsAlive) { allDead = false; break; }
            if (allDead) { TriggerResult(false, "All your monsters were defeated."); return; }
        }

        if (winOnAllEnemiesDefeated && tm.EnemyController != null)
        {
            bool allDead = true;
            foreach (var m in tm.EnemyController.Monsters)
                if (m != null && m.IsAlive) { allDead = false; break; }
            if (allDead) { TriggerResult(true, "All enemies defeated!"); return; }
        }
    }

    private void TriggerResult(bool victory, string reason)
    {
        if (_gameOver) return;
        _gameOver = true;
        PauseMenu.Instance?.Close();
        _titleLabel.text  = victory ? "<color=#FFEE44>Victory!</color>" : "<color=#FF4444>Defeat</color>";
        _reasonLabel.text = reason;
        SetOverlayVisible(true);
        Time.timeScale = 0f;
        if (victory) AudioManager.PlayVictory(); else AudioManager.PlayDefeat();
    }

    private void SetOverlayVisible(bool v)
        => _overlayEl.style.display = v ? DisplayStyle.Flex : DisplayStyle.None;

    // ── Restart ───────────────────────────────────────────────────────────────

    private void RestartScene()
    {
        _gameOver = false;
        SetOverlayVisible(false);
        Time.timeScale = 1f;
        SceneManager.sceneLoaded += OnSceneReloaded;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnSceneReloaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneReloaded;
        StartCoroutine(LateSubscribe());
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ── UI Construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        var s   = UIStyleConfig.Load();
        var doc = gameObject.AddComponent<UIDocument>();
        doc.panelSettings = s?.panelSettings;
        doc.sortingOrder  = 1000;

        var root = doc.rootVisualElement;
        root.pickingMode = PickingMode.Ignore;

        // ── Full-screen dimmed overlay ─────────────────────────────────────
        _overlayEl = new VisualElement();
        _overlayEl.style.position        = Position.Absolute;
        _overlayEl.style.left = 0; _overlayEl.style.top    = 0;
        _overlayEl.style.right = 0; _overlayEl.style.bottom = 0;
        _overlayEl.style.backgroundColor = new Color(0f, 0f, 0f, 0.78f);
        _overlayEl.style.alignItems      = Align.Center;
        _overlayEl.style.justifyContent  = Justify.Center;

        // ── Center panel ──────────────────────────────────────────────────
        var panel = new VisualElement();
        panel.style.width         = 520;
        panel.style.minHeight     = 320;
        panel.style.flexDirection = FlexDirection.Column;
        panel.style.alignItems    = Align.Center;
        panel.style.borderTopLeftRadius    = 8; panel.style.borderTopRightRadius    = 8;
        panel.style.borderBottomLeftRadius = 8; panel.style.borderBottomRightRadius = 8;
        panel.style.overflow = Overflow.Hidden;
        UIStyleConfig.ApplySprite(panel, s?.panelSprite,
            s?.winlosePanelColor ?? new Color(0.06f, 0.06f, 0.10f, 0.97f));

        // ── Dark header strip — title lives entirely within this ───────────
        var titleHeader = new VisualElement();
        titleHeader.style.width           = new Length(100, LengthUnit.Percent);
        titleHeader.style.paddingTop      = 22;
        titleHeader.style.paddingBottom   = 22;
        titleHeader.style.paddingLeft     = 20;
        titleHeader.style.paddingRight    = 20;
        titleHeader.style.alignItems      = Align.Center;
        titleHeader.style.backgroundColor = new Color(0.08f, 0.04f, 0.14f, 1f);

        // Title — rich text colour set at runtime via TriggerResult()
        _titleLabel = new Label();
        _titleLabel.pickingMode = PickingMode.Ignore;
        _titleLabel.style.fontSize                = 64;
        _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _titleLabel.style.color                   = Color.white;
        _titleLabel.style.unityTextAlign          = TextAnchor.MiddleCenter;
        titleHeader.Add(_titleLabel);

        // ── Body — reason text and buttons ────────────────────────────────
        var body = new VisualElement();
        body.style.width         = new Length(100, LengthUnit.Percent);
        body.style.paddingTop    = 24; body.style.paddingBottom = 30;
        body.style.paddingLeft   = 30; body.style.paddingRight  = 30;
        body.style.alignItems    = Align.Center;
        body.style.flexDirection = FlexDirection.Column;

        // Reason
        _reasonLabel = new Label();
        _reasonLabel.pickingMode = PickingMode.Ignore;
        _reasonLabel.style.fontSize       = 20;
        _reasonLabel.style.color          = new Color(0.75f, 0.75f, 0.75f, 1f);
        _reasonLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        _reasonLabel.style.marginBottom   = 30;
        _reasonLabel.style.whiteSpace     = WhiteSpace.Normal;

        // Buttons side by side
        var btnRow = new VisualElement();
        btnRow.style.flexDirection  = FlexDirection.Row;
        btnRow.style.justifyContent = Justify.Center;

        var playAgainBtn = MakeWLButton("PLAY AGAIN",
            s?.playAgainColor ?? new Color(0.15f, 0.45f, 0.20f, 1f),
            s?.buttonSprite, RestartScene);
        playAgainBtn.style.marginRight = 10;

        var quitBtn = MakeWLButton("QUIT",
            s?.quitColor ?? new Color(0.45f, 0.12f, 0.12f, 1f),
            s?.buttonSprite, QuitGame);
        quitBtn.style.marginLeft = 10;

        btnRow.Add(playAgainBtn);
        btnRow.Add(quitBtn);
        body.Add(_reasonLabel);
        body.Add(btnRow);

        panel.Add(titleHeader);
        panel.Add(body);
        _overlayEl.Add(panel);
        root.Add(_overlayEl);
    }

    private static Button MakeWLButton(string text, Color fallback, Sprite sprite, System.Action onClick)
    {
        var btn = new Button(onClick);
        btn.text = text;
        btn.style.width               = 180; btn.style.height = 50;
        btn.style.fontSize            = 20;
        btn.style.unityFontStyleAndWeight = FontStyle.Bold;
        btn.style.color               = Color.white;
        btn.style.borderTopWidth      = 0; btn.style.borderBottomWidth = 0;
        btn.style.borderLeftWidth     = 0; btn.style.borderRightWidth  = 0;
        btn.style.borderTopLeftRadius     = 0; btn.style.borderTopRightRadius     = 0;
        btn.style.borderBottomLeftRadius  = 0; btn.style.borderBottomRightRadius  = 0;
        UIStyleConfig.ApplySprite(btn, sprite, fallback);
        return btn;
    }
}
