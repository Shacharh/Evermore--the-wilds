using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Screen-space overlay panel at the bottom-left showing attack details on hover.
/// Rendered via UI Toolkit. Assign AttackInfoPanel.uxml and PanelSettings in UIStyleConfig.
///
/// Usage (from RadialMenu card hover):
///   AttackInfoPanel.Show(attackData);
///   AttackInfoPanel.Hide();
/// </summary>
public class AttackInfoPanel : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────────────────

    public static AttackInfoPanel Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        new GameObject("AttackInfoPanel").AddComponent<AttackInfoPanel>();
    }

    // ── UI references ──────────────────────────────────────────────────────────

    private UIDocument _uiDoc;
    private VisualElement _root;
    private Label _titleLabel;
    private Label _apLabel;
    private Label _descLabel;
    private Label _statsLabel;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
        if (_root != null) _root.style.display = DisplayStyle.None;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    public static void Show(AttackData attack)
    {
        if (Instance == null || Instance._root == null || attack == null) return;
        Instance.Refresh(attack);
        Instance._root.style.display = DisplayStyle.Flex;
    }

    public static void Hide()
    {
        if (Instance == null || Instance._root == null) return;
        Instance._root.style.display = DisplayStyle.None;
    }

    // ── Refresh ────────────────────────────────────────────────────────────────

    private void Refresh(AttackData attack)
    {
        _titleLabel.text = attack.DisplayName;
        if (_apLabel != null) _apLabel.text = $"{attack.ConsumeActionPoints} AP";

        _descLabel.text = string.IsNullOrWhiteSpace(attack.Description)
            ? "<i>No description.</i>"
            : attack.Description;

        string accStr   = attack.GuaranteedHit ? "Always hits" : $"{attack.Accuracy}%";
        string shapeStr = attack.TargetShape.ToString();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"TYPE   {attack.Element}");

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

        sb.AppendLine($"RNG    {attack.Range}");
        sb.Append    ($"SHAPE  {shapeStr}");

        _statsLabel.text = sb.ToString();
    }

    // ── UI Construction ────────────────────────────────────────────────────────

    private void BuildUI()
    {
        var s = UIStyleConfig.Load();

        if (s?.panelSettings == null || s?.attackInfoPanelUXML == null)
        {
            Debug.LogWarning("[AttackInfoPanel] PanelSettings or UXML not assigned in UIStyleConfig — panel disabled.");
            return;
        }

        _uiDoc = gameObject.AddComponent<UIDocument>();
        _uiDoc.panelSettings   = s.panelSettings;
        _uiDoc.sortingOrder    = 1;
        _uiDoc.visualTreeAsset = s.attackInfoPanelUXML;

        _root = _uiDoc.rootVisualElement.Q("panel-root");

        if (_root == null)
        {
            Debug.LogError("[AttackInfoPanel] 'panel-root' element not found in UXML.");
            return;
        }

        // Sprite fills the full panel; top-bar is transparent so sprite's header shows.
        UIStyleConfig.ApplySprite(_root, s.panelSprite, s.attackPanelColor);

        // Keep the header at the correct height so text sits within the sprite's dark section.
        var topBar = _root.Q("top-bar");
        if (topBar != null)
            topBar.style.height = s.panelHeaderHeight;

        // Cache element references
        _titleLabel = _root.Q<Label>("title-label");
        _apLabel    = _root.Q<Label>("ap-label");
        _descLabel  = _root.Q<Label>("desc-label");
        _statsLabel = _root.Q<Label>("stats-label");
    }
}
