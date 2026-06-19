using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Screen-space overlay panel showing a monster's stats + active stat stages.
///
/// Layout (bottom-right corner):
///   ┌─────────────────── Name  Lv.X  [Side] ───────────────────┐
///   │  HP:  NNN / MMM                                           │
///   │─────────────────────────────────────── STAGE ─────────────│  (divider)
///   │  ATK       123                              +2 (green)    │
///   │  DEF        80                               — (grey)     │
///   │  SPD        95                               — (grey)     │
///   │  DGE        15                              -1 (red)      │
///   │  CRT Chance  29%                             —            │
///   │  CRT Damage  ×8                              —            │
///   │                    [ Close ]                              │
///   └───────────────────────────────────────────────────────────┘
///
/// The stage column updates every frame so buff/debuff changes appear instantly.
///
/// Usage:
///   MonsterInfoPanel.Instance.Show(monster);
///   MonsterInfoPanel.Instance.Hide();
/// </summary>
public class MonsterInfoPanel : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────

    public static MonsterInfoPanel Instance { get; private set; }

    /// <summary>Auto-spawned on scene load — no scene setup required.
    /// Place this script on a scene GameObject to expose sprite fields in the Inspector.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        new GameObject("MonsterInfoPanel").AddComponent<MonsterInfoPanel>();
    }

    // Style — loaded from UIStyleConfig in BuildUI()
    private Sprite _panelSprite;
    private Sprite _buttonSprite;
    private Color  _panelColor;
    private Color  _headerColor;
    private Color  _closeColor;

    // ── UI References ─────────────────────────────────────────────────────────

    private GameObject      panelRoot;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI hpText;
    private TextMeshProUGUI statsText;
    private TextMeshProUGUI stagesText;   // right column — live buff/debuff stages
    private UnityEngine.UI.Image typeIconImage; // element type sprite (top-left of title bar)

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

    private void Update()
    {
        // Refresh the stage column every frame while the panel is visible.
        // Stages can change mid-battle (enemy applies a debuff, etc.) so polling
        // is simpler than wiring up a dedicated stage-change event.
        if (panelRoot.activeSelf && currentMonster != null)
            RefreshStages();
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

        string displayName = currentMonster.Data != null
            ? currentMonster.Data.displayName
            : currentMonster.name.Replace("(Clone)", "").Trim();

        string side = currentMonster.IsEnemy ? "<color=#FF6666>Enemy</color>"
                                              : "<color=#66FF88>Ally</color>";

        titleText.text = $"<b>{displayName}</b>  Lv.{currentMonster.Level}  {side}";

        // Update the type icon sprite
        if (typeIconImage != null && currentMonster.Data != null)
        {
            var lib = TypeIconLibrary.Instance;
            var sprite = lib != null ? lib.GetIcon(currentMonster.Data.elementType) : null;
            typeIconImage.sprite  = sprite;
            typeIconImage.enabled = sprite != null;
        }

        hpText.text = $"HP   <color=#66FF88>{currentMonster.CurrentHP}</color> / {currentMonster.MaxHP}";

        statsText.text =
            $"ATK       {currentMonster.Attack}\n" +
            $"DEF       {currentMonster.Defense}\n" +
            $"SPD       {currentMonster.Speed}  ({currentMonster.TilesPerAP} tile(s)/AP)\n" +
            $"DGE       {currentMonster.Dodge}\n" +
            $"CRT Chance  {currentMonster.CritRate}%\n" +
            $"CRT Damage  ×{currentMonster.CritMod}";

        RefreshStages();
    }

    /// <summary>
    /// Rebuilds the stage column.  Called every frame (from Update) while the
    /// panel is visible, so players see stage changes the moment they happen.
    /// </summary>
    private void RefreshStages()
    {
        if (currentMonster == null || stagesText == null) return;

        int atkS  = currentMonster.GetCurrentStage(AttackEnum.AttackBuffType.Attack);
        int defS  = currentMonster.GetCurrentStage(AttackEnum.AttackBuffType.Defense);
        int spdS  = currentMonster.GetCurrentStage(AttackEnum.AttackBuffType.Speed);
        int dgeS  = currentMonster.GetCurrentStage(AttackEnum.AttackBuffType.Dodge);
        int crtRS = currentMonster.GetCurrentStage(AttackEnum.AttackBuffType.CritRate);
        int crtMS = currentMonster.GetCurrentStage(AttackEnum.AttackBuffType.CritMod);

        stagesText.text =
            $"{StageTag(atkS)}\n"  +
            $"{StageTag(defS)}\n"  +
            $"{StageTag(spdS)}\n"  +
            $"{StageTag(dgeS)}\n"  +
            $"{StageTag(crtRS)}\n" +
            $"{StageTag(crtMS)}";
    }

    /// <summary>
    /// Returns a coloured rich-text string representing a stage value.
    ///   positive  → green  "+N"
    ///   negative  → red    "−N"
    ///   zero      → grey   " —"
    /// </summary>
    private static string StageTag(int stage)
    {
        if (stage > 0) return $"<color=#66FF44>+{stage}</color>";
        if (stage < 0) return $"<color=#FF6666>{stage}</color>";
        return "<color=#444455> —</color>";
    }

    // ── UI Construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        var s      = UIStyleConfig.Load();
        _panelSprite  = s?.panelSprite;
        _buttonSprite = s?.buttonSprite;
        _panelColor   = s?.infoPanelColor   ?? new Color(0.07f, 0.07f, 0.11f, 0.95f);
        _headerColor  = s?.infoHeaderColor  ?? new Color(0.15f, 0.15f, 0.25f, 1f);
        _closeColor   = s?.closeButtonColor ?? new Color(0.65f, 0.15f, 0.15f, 1f);
        float h       = s?.panelHeaderHeight ?? 50f;   // header height drives all body offsets

        // Root canvas — ScreenSpaceOverlay, sort 500 (same as AttackInfoPanel)
        var canvasGO = new GameObject("InfoCanvas");
        canvasGO.transform.SetParent(transform);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Panel root — bottom-right, 420 × 370 px ──────────────────────────
        panelRoot = MakeChild(canvasGO, "Panel");
        var panelRT = panelRoot.GetComponent<RectTransform>();
        panelRT.anchorMin        = new Vector2(1f, 0f);
        panelRT.anchorMax        = new Vector2(1f, 0f);
        panelRT.pivot            = new Vector2(1f, 0f);
        panelRT.anchoredPosition = new Vector2(-24f, 24f);
        panelRT.sizeDelta        = new Vector2(420f, 370f);

        UIStyleConfig.ApplySprite(panelRoot.AddComponent<Image>(), _panelSprite, _panelColor);

        // ── Top bar ───────────────────────────────────────────────────────────
        var barGO = MakeChild(panelRoot, "TopBar");
        var barRT = barGO.GetComponent<RectTransform>();
        barRT.anchorMin = new Vector2(0f, 1f); barRT.anchorMax = new Vector2(1f, 1f);
        barRT.pivot     = new Vector2(0.5f, 1f);
        barRT.anchoredPosition = Vector2.zero;
        barRT.sizeDelta        = new Vector2(0f, h);
        // Only add a coloured bar when no panel sprite is set.
        // When a sprite is used, the header area is already part of the sprite artwork.
        if (_panelSprite == null)
            barGO.AddComponent<Image>().color = _headerColor;

        // ── Type icon (inside top bar, left side) ────────────────────────────
        var iconGO = MakeChild(barGO, "TypeIcon");
        var iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin        = new Vector2(0f, 0.5f);
        iconRT.anchorMax        = new Vector2(0f, 0.5f);
        iconRT.pivot            = new Vector2(0f, 0.5f);
        iconRT.anchoredPosition = new Vector2(8f, 0f);
        iconRT.sizeDelta        = new Vector2(32f, 32f);
        typeIconImage           = iconGO.AddComponent<UnityEngine.UI.Image>();
        typeIconImage.preserveAspect = true;
        typeIconImage.enabled   = false; // hidden until a sprite is assigned

        // ── Title (inside top bar) ────────────────────────────────────────────
        var titleGO = MakeChild(barGO, "Title");
        var titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = Vector2.zero; titleRT.anchorMax = Vector2.one;
        titleRT.offsetMin = new Vector2(46f, 4f); titleRT.offsetMax = new Vector2(-14f, -4f);
        titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.fontSize           = 22f;
        titleText.color              = Color.white;
        titleText.alignment          = TextAlignmentOptions.Center;
        titleText.enableWordWrapping = false;

        // ── HP row ────────────────────────────────────────────────────────────
        var hpGO = MakeChild(panelRoot, "HPRow");
        var hpRT = hpGO.GetComponent<RectTransform>();
        hpRT.anchorMin        = new Vector2(0f, 1f);
        hpRT.anchorMax        = new Vector2(1f, 1f);
        hpRT.pivot            = new Vector2(0.5f, 1f);
        hpRT.anchoredPosition = new Vector2(0f, -(h + 4f));
        hpRT.sizeDelta        = new Vector2(-28f, 30f);   // 30 px tall
        hpText = hpGO.AddComponent<TextMeshProUGUI>();
        hpText.fontSize   = 20f;
        hpText.color      = Color.white;
        hpText.alignment  = TextAlignmentOptions.Center;

        // ── "STAGE" column header (right portion, above divider) ──────────────
        // Occupies the gap between HP row bottom (−84) and the divider (−90).
        var stageHdrGO = MakeChild(panelRoot, "StageHeader");
        var stageHdrRT = stageHdrGO.GetComponent<RectTransform>();
        stageHdrRT.anchorMin        = new Vector2(1f, 1f);
        stageHdrRT.anchorMax        = new Vector2(1f, 1f);
        stageHdrRT.pivot            = new Vector2(1f, 1f);
        stageHdrRT.anchoredPosition = new Vector2(-14f, -(h + 34f));
        stageHdrRT.sizeDelta        = new Vector2(120f, 14f);
        var stageHdrTmp = stageHdrGO.AddComponent<TextMeshProUGUI>();
        stageHdrTmp.text              = "STAGE";
        stageHdrTmp.fontSize          = 13f;
        stageHdrTmp.color             = new Color(0.55f, 0.55f, 0.65f, 1f);
        stageHdrTmp.alignment         = TextAlignmentOptions.Right;
        stageHdrTmp.fontStyle         = FontStyles.Italic;
        stageHdrTmp.enableWordWrapping = false;

        // ── Divider (full width) ──────────────────────────────────────────────
        // Only draw the code divider when no panel sprite is set.
        // When a sprite is used its artwork already provides the visual separator.
        var divGO = MakeChild(panelRoot, "Divider");
        var divRT = divGO.GetComponent<RectTransform>();
        divRT.anchorMin        = new Vector2(0f, 1f);
        divRT.anchorMax        = new Vector2(1f, 1f);
        divRT.pivot            = new Vector2(0.5f, 1f);
        divRT.anchoredPosition = new Vector2(0f, -(h + 50f));
        divRT.sizeDelta        = new Vector2(-28f, 2f);
        if (_panelSprite == null)
            divGO.AddComponent<Image>().color = new Color(0.35f, 0.35f, 0.5f, 1f);

        // ── Stats text — left column (stat name + value) ──────────────────────
        var statsGO = MakeChild(panelRoot, "Stats");
        var statsRT = statsGO.GetComponent<RectTransform>();
        statsRT.anchorMin        = new Vector2(0f, 1f);
        statsRT.anchorMax        = new Vector2(0f, 1f);
        statsRT.pivot            = new Vector2(0f, 1f);
        statsRT.anchoredPosition = new Vector2(14f, -(h + 56f));
        statsRT.sizeDelta        = new Vector2(270f, 200f);
        statsText = statsGO.AddComponent<TextMeshProUGUI>();
        statsText.fontSize           = 18f;
        statsText.color              = new Color(0.88f, 0.88f, 0.88f, 1f);
        statsText.alignment          = TextAlignmentOptions.Left;
        statsText.lineSpacing        = 8f;
        statsText.enableWordWrapping = false;

        // ── Stages text — right column (live buff/debuff stage values) ─────────
        var stagesGO = MakeChild(panelRoot, "Stages");
        var stagesRT = stagesGO.GetComponent<RectTransform>();
        stagesRT.anchorMin        = new Vector2(1f, 1f);
        stagesRT.anchorMax        = new Vector2(1f, 1f);
        stagesRT.pivot            = new Vector2(1f, 1f);
        stagesRT.anchoredPosition = new Vector2(-14f, -(h + 56f));  // same Y as statsText
        stagesRT.sizeDelta        = new Vector2(120f, 200f);
        stagesText = stagesGO.AddComponent<TextMeshProUGUI>();
        stagesText.fontSize           = 18f;
        stagesText.color              = new Color(0.88f, 0.88f, 0.88f, 1f);
        stagesText.alignment          = TextAlignmentOptions.Right;
        stagesText.lineSpacing        = 8f;
        stagesText.enableWordWrapping = false;
        stagesText.richText           = true;

        // ── Close button ──────────────────────────────────────────────────────
        var closeGO = MakeChild(panelRoot, "Close");
        var closeRT = closeGO.GetComponent<RectTransform>();
        closeRT.anchorMin        = new Vector2(0.5f, 0f);
        closeRT.anchorMax        = new Vector2(0.5f, 0f);
        closeRT.pivot            = new Vector2(0.5f, 0f);
        closeRT.anchoredPosition = new Vector2(0f, 14f);
        closeRT.sizeDelta        = new Vector2(160f, 36f);

        var closeBg = closeGO.AddComponent<Image>();
        UIStyleConfig.ApplySprite(closeBg, _buttonSprite, _closeColor);

        var closeBtn = closeGO.AddComponent<Button>();
        closeBtn.targetGraphic = closeBg;
        closeBtn.onClick.AddListener(Hide);

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

    private static GameObject MakeChild(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();
        return go;
    }
}
