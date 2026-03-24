using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Screen-space overlay panel that shows an attack's details when the player
/// hovers over an attack button in the radial (attack sub-)menu.
///
/// Appears at the bottom-LEFT of the screen, mirroring the MonsterInfoPanel
/// which sits bottom-right.  Instantly shows/hides — no fade — so it tracks
/// hover state frame-accurately.
///
/// Usage (from RadialMenuButton):
///   AttackInfoPanel.Show(attackData);
///   AttackInfoPanel.Hide();
///
/// Auto-created singleton — no prefab or scene setup needed.
/// </summary>
public class AttackInfoPanel : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────

    public static AttackInfoPanel Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        new GameObject("AttackInfoPanel").AddComponent<AttackInfoPanel>();
    }

    // ── UI References ─────────────────────────────────────────────────────────

    private GameObject      panelRoot;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI descText;
    private TextMeshProUGUI statsText;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
        panelRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Shows the panel populated with <paramref name="attack"/>'s data.</summary>
    public static void Show(AttackData attack)
    {
        if (Instance == null || attack == null) return;
        Instance.Refresh(attack);
        Instance.panelRoot.SetActive(true);
    }

    /// <summary>Hides the panel.</summary>
    public static void Hide()
    {
        if (Instance == null) return;
        Instance.panelRoot.SetActive(false);
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    private void Refresh(AttackData attack)
    {
        // ── Title ─────────────────────────────────────────────────────────────
        titleText.text = $"<b>{attack.DisplayName}</b>";

        // ── Description ───────────────────────────────────────────────────────
        descText.text = string.IsNullOrWhiteSpace(attack.Description)
            ? "<i>No description.</i>"
            : attack.Description;

        // ── Stat block — content depends on the primary effect's category ─────
        string accStr   = attack.GuaranteedHit ? "Always hits" : $"{attack.Accuracy}%";
        string shapeStr = attack.TargetShape.ToString();

        var sb = new System.Text.StringBuilder();

        AttackEffect primary = attack.Effects != null && attack.Effects.Count > 0
            ? attack.Effects[0] : null;

        if (primary != null)
        {
            switch (primary.category)
            {
                case AttackEnum.AttackCategory.damage:
                    sb.AppendLine($"DMG    {primary.value}");
                    sb.AppendLine($"ACC    {accStr}");
                    break;

                case AttackEnum.AttackCategory.heal:
                    sb.AppendLine($"HEAL   {primary.value}");
                    // Heals usually target self/allies, so accuracy isn't relevant
                    break;

                case AttackEnum.AttackCategory.buff:
                    string buffSign = primary.isDebuff ? "−" : "+";
                    sb.AppendLine($"STAT   {primary.buffType}");
                    sb.AppendLine($"STAGES {buffSign}{primary.stageCount}");
                    if (primary.chance < 100)
                        sb.AppendLine($"CHANCE {primary.chance}%");
                    sb.AppendLine($"DURTN  {primary.duration} turns");
                    break;

                case AttackEnum.AttackCategory.status:
                    string statusName = primary.statusEffect != null
                        ? primary.statusEffect.name : "Unknown";
                    sb.AppendLine($"STATUS {statusName}");
                    if (primary.chance < 100)
                        sb.AppendLine($"CHANCE {primary.chance}%");
                    sb.AppendLine($"DURTN  {primary.duration} turns");
                    break;
            }
        }
        else
        {
            sb.AppendLine("No effects set.");
        }

        // Always show AP cost, range, and shape regardless of effect type
        sb.AppendLine($"AP     {attack.ConsumeActionPoints}");
        sb.AppendLine($"RNG    {attack.Range}");
        sb.Append    ($"SHAPE  {shapeStr}");

        statsText.text = sb.ToString();
    }

    // ── UI Construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        // Root canvas — ScreenSpaceOverlay, sort order 500 (same as MonsterInfoPanel)
        var canvasGO = new GameObject("AttackInfoCanvas");
        canvasGO.transform.SetParent(transform);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Panel root — bottom-LEFT, 420 × 300 px ───────────────────────────
        panelRoot = MakeChild(canvasGO, "Panel");
        var panelRT = panelRoot.GetComponent<RectTransform>();
        panelRT.anchorMin        = new Vector2(0f, 0f);
        panelRT.anchorMax        = new Vector2(0f, 0f);
        panelRT.pivot            = new Vector2(0f, 0f);
        panelRT.anchoredPosition = new Vector2(24f, 24f);   // 24 px from bottom-left
        panelRT.sizeDelta        = new Vector2(420f, 300f);

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
        barGO.AddComponent<Image>().color = new Color(0.10f, 0.15f, 0.25f, 1f); // blue-tinted

        // ── Title ─────────────────────────────────────────────────────────────
        var titleGO = MakeChild(barGO, "Title");
        var titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = Vector2.zero; titleRT.anchorMax = Vector2.one;
        titleRT.offsetMin = new Vector2(14f, 4f); titleRT.offsetMax = new Vector2(-14f, -4f);
        titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.fontSize           = 22f;
        titleText.color              = new Color(0.85f, 0.95f, 1f, 1f);   // pale blue-white
        titleText.alignment          = TextAlignmentOptions.Center;
        titleText.enableWordWrapping = false;

        // ── Description text ──────────────────────────────────────────────────
        var descGO = MakeChild(panelRoot, "Desc");
        var descRT = descGO.GetComponent<RectTransform>();
        descRT.anchorMin        = new Vector2(0f, 1f);
        descRT.anchorMax        = new Vector2(1f, 1f);
        descRT.pivot            = new Vector2(0.5f, 1f);
        descRT.anchoredPosition = new Vector2(0f, -54f);
        descRT.sizeDelta        = new Vector2(-28f, 48f);
        descText = descGO.AddComponent<TextMeshProUGUI>();
        descText.fontSize        = 16f;
        descText.color           = new Color(0.78f, 0.78f, 0.78f, 1f);
        descText.alignment       = TextAlignmentOptions.Center;
        descText.enableWordWrapping = true;

        // ── Divider ───────────────────────────────────────────────────────────
        var divGO = MakeChild(panelRoot, "Divider");
        var divRT = divGO.GetComponent<RectTransform>();
        divRT.anchorMin        = new Vector2(0f, 1f);
        divRT.anchorMax        = new Vector2(1f, 1f);
        divRT.pivot            = new Vector2(0.5f, 1f);
        divRT.anchoredPosition = new Vector2(0f, -106f);
        divRT.sizeDelta        = new Vector2(-28f, 2f);
        divGO.AddComponent<Image>().color = new Color(0.25f, 0.35f, 0.5f, 1f);

        // ── Stats block ───────────────────────────────────────────────────────
        var statsGO = MakeChild(panelRoot, "Stats");
        var statsRT = statsGO.GetComponent<RectTransform>();
        statsRT.anchorMin        = new Vector2(0f, 1f);
        statsRT.anchorMax        = new Vector2(1f, 1f);
        statsRT.pivot            = new Vector2(0.5f, 1f);
        statsRT.anchoredPosition = new Vector2(0f, -114f);
        statsRT.sizeDelta        = new Vector2(-28f, 160f);
        statsText = statsGO.AddComponent<TextMeshProUGUI>();
        statsText.fontSize        = 18f;
        statsText.color           = new Color(0.88f, 0.88f, 0.88f, 1f);
        statsText.alignment       = TextAlignmentOptions.Left;
        statsText.lineSpacing     = 8f;
        statsText.enableWordWrapping = false;
    }

    private static GameObject MakeChild(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();
        return go;
    }
}
