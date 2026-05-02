using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Evaluates configurable win/lose conditions and shows a result screen when one
/// is triggered.
///
/// AUTO-SETUP — Attach this component to any GameObject in the scene (or let it
/// auto-create).  Configure the conditions in the Inspector.
///
/// Toggleable win conditions:
///   • All enemy monsters defeated   (default ON)
///   • Survive N turns               (default OFF)
///
/// Toggleable lose conditions:
///   • All player monsters defeated  (default ON)
///
/// The result screen shows "Victory!" or "Defeat", and lets the player restart
/// or quit.  The game is paused (Time.timeScale = 0) while the screen is shown.
/// </summary>
public class WinLoseManager : MonoBehaviour
{
    public static WinLoseManager Instance { get; private set; }

    // ── Auto-Create ───────────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        if (FindFirstObjectByType<WinLoseManager>() != null) return;
        new GameObject("WinLoseManager").AddComponent<WinLoseManager>();
    }

    // ── Inspector-configurable conditions ─────────────────────────────────────

    [Header("Win Conditions")]
    [Tooltip("Win when every enemy monster is defeated.")]
    public bool winOnAllEnemiesDefeated = true;

    [Tooltip("Win after surviving this many full rounds (player + enemy = 1 round).")]
    public bool winOnSurviveNRounds = false;

    [Tooltip("Number of rounds to survive (used when winOnSurviveNRounds is true).")]
    public int survivalRoundTarget = 10;

    [Header("Lose Conditions")]
    [Tooltip("Lose when every player monster is defeated.")]
    public bool loseOnAllPlayerMonstersDefeated = true;

    // ── State ─────────────────────────────────────────────────────────────────

    private bool _gameOver = false;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
        SetOverlayVisible(false);
    }

    private void Start()
    {
        StartCoroutine(LateSubscribe());
    }

    /// <summary>
    /// Waits until TurnManager has finished setup (it waits 0.5 s itself), then
    /// subscribes to all monster OnDied events and to the turn counter.
    /// </summary>
    private IEnumerator LateSubscribe()
    {
        yield return new WaitForSeconds(0.7f);

        // Subscribe to round counter for survival win condition
        if (TurnManager.Instance != null)
            TurnManager.Instance.onNewRound.AddListener(OnNewRound);

        // Subscribe to every monster's OnDied event
        foreach (var monster in FindObjectsByType<Monster>(FindObjectsSortMode.None))
            monster.OnDied += OnMonsterDied;
    }

    private void OnMonsterDied(Monster monster)
    {
        if (_gameOver) return;
        CheckConditions();
    }

    private void OnNewRound(int round)
    {
        if (_gameOver) return;
        if (winOnSurviveNRounds && round > survivalRoundTarget)
            TriggerResult(true, $"Survived {survivalRoundTarget} rounds!");
    }

    // ── Condition evaluation ──────────────────────────────────────────────────

    private void CheckConditions()
    {
        var tm = TurnManager.Instance;
        if (tm == null) return;

        // ── Lose checks ───────────────────────────────────────────────────────
        if (loseOnAllPlayerMonstersDefeated && tm.PlayerController != null)
        {
            bool allDead = true;
            foreach (var m in tm.PlayerController.Monsters)
                if (m != null && m.IsAlive) { allDead = false; break; }

            if (allDead)
            {
                TriggerResult(false, "All your monsters were defeated.");
                return;
            }
        }

        // ── Win checks ────────────────────────────────────────────────────────
        if (winOnAllEnemiesDefeated && tm.EnemyController != null)
        {
            bool allDead = true;
            foreach (var m in tm.EnemyController.Monsters)
                if (m != null && m.IsAlive) { allDead = false; break; }

            if (allDead)
            {
                TriggerResult(true, "All enemies defeated!");
                return;
            }
        }
    }

    // ── Result screen ─────────────────────────────────────────────────────────

    private void TriggerResult(bool victory, string reason)
    {
        if (_gameOver) return;
        _gameOver = true;

        // Close pause menu if open
        PauseMenu.Instance?.Close();

        _titleText.text  = victory ? "<color=#FFEE44>Victory!</color>" : "<color=#FF4444>Defeat</color>";
        _reasonText.text = reason;
        SetOverlayVisible(true);
        Time.timeScale = 0f;

        Debug.Log($"[WinLoseManager] Game over — {(victory ? "WIN" : "LOSS")}: {reason}");
    }

    // ── UI ────────────────────────────────────────────────────────────────────

    private GameObject      _overlay;
    private TextMeshProUGUI _titleText;
    private TextMeshProUGUI _reasonText;

    private void BuildUI()
    {
        var canvasGO        = new GameObject("WinLoseCanvas");
        canvasGO.transform.SetParent(transform);
        var canvas          = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // topmost layer

        var scaler                 = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // Full-screen dark overlay
        _overlay = MakeChild(canvasGO, "Overlay");
        var overlayRT       = _overlay.GetComponent<RectTransform>();
        overlayRT.anchorMin = Vector2.zero;
        overlayRT.anchorMax = Vector2.one;
        overlayRT.sizeDelta = Vector2.zero;
        _overlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);

        // Center panel
        var panel    = MakeChild(_overlay, "Panel");
        var panelRT  = panel.GetComponent<RectTransform>();
        panelRT.anchorMin        = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax        = new Vector2(0.5f, 0.5f);
        panelRT.pivot            = new Vector2(0.5f, 0.5f);
        panelRT.anchoredPosition = Vector2.zero;
        panelRT.sizeDelta        = new Vector2(500f, 320f);
        panel.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.10f, 0.97f);

        // Victory / Defeat title
        var titleGO = MakeChild(panel, "Title");
        var titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin        = new Vector2(0f, 1f);
        titleRT.anchorMax        = new Vector2(1f, 1f);
        titleRT.pivot            = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0f, -30f);
        titleRT.sizeDelta        = new Vector2(0f, 80f);
        _titleText          = titleGO.AddComponent<TextMeshProUGUI>();
        _titleText.fontSize  = 52f;
        _titleText.fontStyle = FontStyles.Bold;
        _titleText.color     = Color.white;
        _titleText.alignment = TextAlignmentOptions.Center;
        _titleText.richText  = true;

        // Reason subtitle
        var reasonGO = MakeChild(panel, "Reason");
        var reasonRT = reasonGO.GetComponent<RectTransform>();
        reasonRT.anchorMin        = new Vector2(0f, 1f);
        reasonRT.anchorMax        = new Vector2(1f, 1f);
        reasonRT.pivot            = new Vector2(0.5f, 1f);
        reasonRT.anchoredPosition = new Vector2(0f, -120f);
        reasonRT.sizeDelta        = new Vector2(-40f, 50f);
        _reasonText           = reasonGO.AddComponent<TextMeshProUGUI>();
        _reasonText.fontSize   = 20f;
        _reasonText.color      = new Color(0.75f, 0.75f, 0.75f);
        _reasonText.alignment  = TextAlignmentOptions.Center;

        // Play Again button
        AddButton(panel, "Play Again", new Color(0.15f, 0.45f, 0.2f),
                  anchorY: 0.2f, offsetX: -110f, onClick: RestartScene);

        // Quit button
        AddButton(panel, "Quit", new Color(0.45f, 0.12f, 0.12f),
                  anchorY: 0.2f, offsetX: 110f, onClick: QuitGame);
    }

    private void AddButton(GameObject panel, string label, Color bgColor,
                           float anchorY, float offsetX, System.Action onClick)
    {
        var go = MakeChild(panel, label + "Btn");
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, anchorY);
        rt.anchorMax        = new Vector2(0.5f, anchorY);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(offsetX, 0f);
        rt.sizeDelta        = new Vector2(180f, 48f);

        var img = go.AddComponent<Image>();
        img.color = bgColor;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick?.Invoke());

        var colors = btn.colors;
        colors.highlightedColor = new Color(bgColor.r + 0.15f, bgColor.g + 0.15f, bgColor.b + 0.15f);
        colors.pressedColor     = new Color(bgColor.r - 0.1f,  bgColor.g - 0.1f,  bgColor.b - 0.1f);
        btn.colors = colors;

        var lblGO = MakeChild(go, "Lbl");
        var lblRT = lblGO.GetComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
        lblRT.sizeDelta = Vector2.zero;
        var tmp = lblGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 20f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
    }

    private void SetOverlayVisible(bool v) => _overlay.SetActive(v);

    private void RestartScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static GameObject MakeChild(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();
        return go;
    }
}
