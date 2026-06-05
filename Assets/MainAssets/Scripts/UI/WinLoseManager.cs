using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Evaluates win/lose conditions and shows a result screen.
/// AUTO-SETUP: creates its own Canvas at runtime. No scene placement required.
/// Visual style is driven by a UIStyleConfig asset in Assets/Resources/.
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

    private bool             _gameOver;
    private GameObject       _overlay;
    private TextMeshProUGUI  _titleText;
    private TextMeshProUGUI  _reasonText;

    // Style
    private Sprite _panelSprite;
    private Sprite _buttonSprite;
    private Color  _panelColor;
    private Color  _playAgainColor;
    private Color  _quitColor;

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
        _titleText.text  = victory ? "<color=#FFEE44>Victory!</color>" : "<color=#FF4444>Defeat</color>";
        _reasonText.text = reason;
        SetOverlayVisible(true);
        Time.timeScale = 0f;
    }

    private void SetOverlayVisible(bool v) => _overlay.SetActive(v);

    // ── Restart (bug fix: overlay persisted across scene reload) ──────────────

    private void RestartScene()
    {
        _gameOver = false;
        SetOverlayVisible(false);      // hide BEFORE the scene loads
        Time.timeScale = 1f;
        SceneManager.sceneLoaded += OnSceneReloaded;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnSceneReloaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneReloaded;
        StartCoroutine(LateSubscribe());   // re-subscribe to new scene's monsters
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
        var s         = UIStyleConfig.Load();
        _panelSprite   = s?.panelSprite;
        _buttonSprite  = s?.buttonSprite;
        _panelColor    = s?.winlosePanelColor ?? new Color(0.06f, 0.06f, 0.10f, 0.97f);
        _playAgainColor = s?.playAgainColor   ?? new Color(0.15f, 0.45f, 0.20f, 1f);
        _quitColor     = s?.quitColor         ?? new Color(0.45f, 0.12f, 0.12f, 1f);

        var canvasGO        = new GameObject("WinLoseCanvas");
        canvasGO.transform.SetParent(transform);
        var canvas          = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler                 = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        _overlay = MakeChild(canvasGO, "Overlay");
        var oRT = _overlay.GetComponent<RectTransform>();
        oRT.anchorMin = Vector2.zero; oRT.anchorMax = Vector2.one; oRT.sizeDelta = Vector2.zero;
        _overlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);

        var panel   = MakeChild(_overlay, "Panel");
        var panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin        = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax        = new Vector2(0.5f, 0.5f);
        panelRT.pivot            = new Vector2(0.5f, 0.5f);
        panelRT.anchoredPosition = Vector2.zero;
        panelRT.sizeDelta        = new Vector2(500f, 320f);
        UIStyleConfig.ApplySprite(panel.AddComponent<Image>(), _panelSprite, _panelColor);

        var titleGO = MakeChild(panel, "Title");
        var titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0f, 1f); titleRT.anchorMax = new Vector2(1f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0f, -30f); titleRT.sizeDelta = new Vector2(0f, 80f);
        _titleText = titleGO.AddComponent<TextMeshProUGUI>();
        _titleText.fontSize = 52f; _titleText.fontStyle = FontStyles.Bold;
        _titleText.color = Color.white; _titleText.alignment = TextAlignmentOptions.Center;
        _titleText.richText = true;

        var reasonGO = MakeChild(panel, "Reason");
        var reasonRT = reasonGO.GetComponent<RectTransform>();
        reasonRT.anchorMin = new Vector2(0f, 1f); reasonRT.anchorMax = new Vector2(1f, 1f);
        reasonRT.pivot = new Vector2(0.5f, 1f);
        reasonRT.anchoredPosition = new Vector2(0f, -120f); reasonRT.sizeDelta = new Vector2(-40f, 50f);
        _reasonText = reasonGO.AddComponent<TextMeshProUGUI>();
        _reasonText.fontSize = 20f; _reasonText.color = new Color(0.75f, 0.75f, 0.75f);
        _reasonText.alignment = TextAlignmentOptions.Center;

        AddButton(panel, "Play Again", _playAgainColor, 0.2f, -110f, RestartScene);
        AddButton(panel, "Quit",       _quitColor,      0.2f,  110f, QuitGame);
    }

    private void AddButton(GameObject panel, string label, Color bgColor,
                           float anchorY, float offsetX, System.Action onClick)
    {
        var go = MakeChild(panel, label + "Btn");
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, anchorY); rt.anchorMax = new Vector2(0.5f, anchorY);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(offsetX, 0f); rt.sizeDelta = new Vector2(180f, 48f);

        var img = go.AddComponent<Image>();
        UIStyleConfig.ApplySprite(img, _buttonSprite, bgColor);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick?.Invoke());

        var lblGO = MakeChild(go, "Lbl");
        var lblRT = lblGO.GetComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one; lblRT.sizeDelta = Vector2.zero;
        var tmp = lblGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 20f; tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white; tmp.alignment = TextAlignmentOptions.Center;
    }

    private static GameObject MakeChild(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();
        return go;
    }
}
