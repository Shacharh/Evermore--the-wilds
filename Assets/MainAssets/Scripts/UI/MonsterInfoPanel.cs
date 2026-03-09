using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Screen-space overlay panel that shows a monster's stats when the player
/// clicks the "Info" option in the radial menu.
///
/// Builds its own Canvas entirely in code — no prefab needed.
/// Scales with screen size (reference 1280×720) so it looks the same at any resolution.
///
/// Usage:
///   MonsterInfoPanel.Instance.Show(monster);
///   MonsterInfoPanel.Instance.Hide();
/// </summary>
public class MonsterInfoPanel : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────

    public static MonsterInfoPanel Instance { get; private set; }

    /// <summary>Auto-spawned on scene load — no need to add it to the scene manually.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        new GameObject("MonsterInfoPanel").AddComponent<MonsterInfoPanel>();
    }

    // ── UI References ─────────────────────────────────────────────────────────

    private GameObject      panelRoot;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI hpText;
    private TextMeshProUGUI statsText;

    // ── State ─────────────────────────────────────────────────────────────────

    private Monster currentMonster;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
        Hide();
    }

    private void OnDestroy()
    {
        UnsubscribeFromMonster();
        if (Instance == this) Instance = null;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Show(Monster monster)
    {
        if (monster == null) return;
        UnsubscribeFromMonster();
        currentMonster = monster;
        currentMonster.OnHPChanged += OnHPChanged;
        Refresh();
        panelRoot.SetActive(true);
    }

    public void Hide()
    {
        panelRoot.SetActive(false);
        UnsubscribeFromMonster();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void UnsubscribeFromMonster()
    {
        if (currentMonster != null)
        {
            currentMonster.OnHPChanged -= OnHPChanged;
            currentMonster = null;
        }
    }

    private void OnHPChanged(int current, int max) => Refresh();

    private void Refresh()
    {
        if (currentMonster == null) return;

        // Prefer the MonsterData display name; fall back to GO name
        string displayName = currentMonster.Data != null
            ? currentMonster.Data.displayName
            : currentMonster.name.Replace("(Clone)", "").Trim();

        string side = currentMonster.IsEnemy ? "<color=#FF6666>Enemy</color>"
                                              : "<color=#66FF88>Ally</color>";

        titleText.text = $"<b>{displayName}</b>  Lv.{currentMonster.Level}  {side}";

        hpText.text = $"HP   <color=#66FF88>{currentMonster.CurrentHP}</color> / {currentMonster.MaxHP}";

        statsText.text =
            $"ATK  {currentMonster.Attack}\n" +
            $"DEF  {currentMonster.Defense}\n" +
            $"SPD  {currentMonster.Speed}\n" +
            $"DGE  {currentMonster.Dodge}\n" +
            $"CRT  {currentMonster.CritRate}%  ×{currentMonster.CritMod}";
    }

    // ── UI Construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        // Root canvas
        var canvasGO = new GameObject("InfoCanvas");
        canvasGO.transform.SetParent(transform);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        // ScaleWithScreenSize so the panel looks consistent at any resolution
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Panel root (bottom-right, 420 × 310 in 1280×720 reference space) ──
        panelRoot = MakeChild(canvasGO, "Panel");
        var panelRT = panelRoot.GetComponent<RectTransform>();
        panelRT.anchorMin         = new Vector2(1f, 0f);
        panelRT.anchorMax         = new Vector2(1f, 0f);
        panelRT.pivot             = new Vector2(1f, 0f);
        panelRT.anchoredPosition  = new Vector2(-24f, 24f);
        panelRT.sizeDelta         = new Vector2(420f, 310f);

        var bg = panelRoot.AddComponent<Image>();
        bg.color = new Color(0.07f, 0.07f, 0.11f, 0.95f);

        // ── Coloured top bar ──────────────────────────────────────────────────
        var barGO = MakeChild(panelRoot, "TopBar");
        var barRT = barGO.GetComponent<RectTransform>();
        barRT.anchorMin        = new Vector2(0f, 1f);
        barRT.anchorMax        = new Vector2(1f, 1f);
        barRT.pivot            = new Vector2(0.5f, 1f);
        barRT.anchoredPosition = Vector2.zero;
        barRT.sizeDelta        = new Vector2(0f, 50f);
        barGO.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.25f, 1f);

        // ── Title (inside top bar) ────────────────────────────────────────────
        var titleGO = MakeChild(barGO, "Title");
        var titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = Vector2.zero; titleRT.anchorMax = Vector2.one;
        titleRT.offsetMin = new Vector2(14f, 4f); titleRT.offsetMax = new Vector2(-14f, -4f);
        titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.fontSize        = 22f;
        titleText.color           = Color.white;
        titleText.alignment       = TextAlignmentOptions.Center;
        titleText.enableWordWrapping = false;

        // ── HP row ────────────────────────────────────────────────────────────
        var hpGO = MakeChild(panelRoot, "HPRow");
        var hpRT = hpGO.GetComponent<RectTransform>();
        hpRT.anchorMin         = new Vector2(0f, 1f);
        hpRT.anchorMax         = new Vector2(1f, 1f);
        hpRT.pivot             = new Vector2(0.5f, 1f);
        hpRT.anchoredPosition  = new Vector2(0f, -54f);
        hpRT.sizeDelta         = new Vector2(-28f, 34f);
        hpText = hpGO.AddComponent<TextMeshProUGUI>();
        hpText.fontSize        = 20f;
        hpText.color           = Color.white;
        hpText.alignment       = TextAlignmentOptions.Center;

        // ── Divider ───────────────────────────────────────────────────────────
        var divGO = MakeChild(panelRoot, "Divider");
        var divRT = divGO.GetComponent<RectTransform>();
        divRT.anchorMin        = new Vector2(0f, 1f);
        divRT.anchorMax        = new Vector2(1f, 1f);
        divRT.pivot            = new Vector2(0.5f, 1f);
        divRT.anchoredPosition = new Vector2(0f, -92f);
        divRT.sizeDelta        = new Vector2(-28f, 2f);
        divGO.AddComponent<Image>().color = new Color(0.35f, 0.35f, 0.5f, 1f);

        // ── Stats text ────────────────────────────────────────────────────────
        var statsGO = MakeChild(panelRoot, "Stats");
        var statsRT = statsGO.GetComponent<RectTransform>();
        statsRT.anchorMin        = new Vector2(0f, 1f);
        statsRT.anchorMax        = new Vector2(1f, 1f);
        statsRT.pivot            = new Vector2(0.5f, 1f);
        statsRT.anchoredPosition = new Vector2(0f, -100f);
        statsRT.sizeDelta        = new Vector2(-28f, 150f);
        statsText = statsGO.AddComponent<TextMeshProUGUI>();
        statsText.fontSize        = 18f;
        statsText.color           = new Color(0.88f, 0.88f, 0.88f, 1f);
        statsText.alignment       = TextAlignmentOptions.Left;
        statsText.lineSpacing     = 8f;

        // ── Close button ──────────────────────────────────────────────────────
        var closeGO = MakeChild(panelRoot, "Close");
        var closeRT = closeGO.GetComponent<RectTransform>();
        closeRT.anchorMin        = new Vector2(0.5f, 0f);
        closeRT.anchorMax        = new Vector2(0.5f, 0f);
        closeRT.pivot            = new Vector2(0.5f, 0f);
        closeRT.anchoredPosition = new Vector2(0f, 14f);
        closeRT.sizeDelta        = new Vector2(160f, 36f);

        var closeBg = closeGO.AddComponent<Image>();
        closeBg.color = new Color(0.65f, 0.15f, 0.15f, 1f);

        var closeBtn = closeGO.AddComponent<Button>();
        closeBtn.targetGraphic = closeBg;
        closeBtn.onClick.AddListener(Hide);

        // colour tint on hover
        var colors = closeBtn.colors;
        colors.highlightedColor = new Color(0.85f, 0.25f, 0.25f, 1f);
        colors.pressedColor     = new Color(0.5f, 0.1f, 0.1f, 1f);
        closeBtn.colors = colors;

        var closeLblGO = MakeChild(closeGO, "Lbl");
        var closeLblRT = closeLblGO.GetComponent<RectTransform>();
        closeLblRT.anchorMin = Vector2.zero; closeLblRT.anchorMax = Vector2.one;
        closeLblRT.sizeDelta = Vector2.zero;
        var closeTmp = closeLblGO.AddComponent<TextMeshProUGUI>();
        closeTmp.text      = "Close";
        closeTmp.fontSize  = 18f;
        closeTmp.fontStyle = FontStyles.Bold;
        closeTmp.color     = Color.white;
        closeTmp.alignment = TextAlignmentOptions.Center;
    }

    /// <summary>Creates a child GameObject with a RectTransform already added.</summary>
    private static GameObject MakeChild(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();
        return go;
    }
}
