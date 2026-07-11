using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Screen-space overlay panel showing a monster's stats + active stat stages.
/// Rendered via UI Toolkit (UIDocument + UXML). Assign MonsterInfoPanel.uxml and
/// a PanelSettings in UIStyleConfig.
///
/// Usage:
///   MonsterInfoPanel.Instance.Show(monster);
///   MonsterInfoPanel.Instance.Hide();
/// </summary>
public class MonsterInfoPanel : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────────────────

    public static MonsterInfoPanel Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        new GameObject("MonsterInfoPanel").AddComponent<MonsterInfoPanel>();
    }

    // ── UI references ──────────────────────────────────────────────────────────

    private UIDocument    _uiDoc;
    private VisualElement _root;
    private VisualElement _typeIcon;
    private Label         _nameLabel;
    private Label         _levelLabel;
    private Label         _sideLabel;
    private Label         _hpLabel;
    private Label         _statsLabel;
    private Label         _stagesLabel;
    private Label         _statusLabel;

    // ── State ──────────────────────────────────────────────────────────────────

    private Monster _currentMonster;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

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
        if (_root != null
            && _root.style.display == DisplayStyle.Flex
            && _currentMonster != null)
        {
            RefreshStages();
            RefreshStatuses();
        }
    }

    private void OnDestroy()
    {
        UnsubscribeFromMonster();
        if (Instance == this) Instance = null;
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    public bool IsVisible => _root != null && _root.style.display == DisplayStyle.Flex;

    public static event System.Action OnOpened;
    public static event System.Action OnClosed;

    public void Show(Monster monster)
    {
        if (monster == null || _root == null) return;
        UnsubscribeFromMonster();
        _currentMonster = monster;
        _currentMonster.OnHPChanged += OnHPChanged;
        Refresh();
        _root.style.display = DisplayStyle.Flex;
        OnOpened?.Invoke();
    }

    public void Hide()
    {
        if (_root != null) _root.style.display = DisplayStyle.None;
        UnsubscribeFromMonster();
        OnClosed?.Invoke();
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private void UnsubscribeFromMonster()
    {
        if (_currentMonster != null)
        {
            _currentMonster.OnHPChanged -= OnHPChanged;
            _currentMonster = null;
        }
    }

    private void OnHPChanged(int current, int max) => Refresh();

    private void Refresh()
    {
        if (_currentMonster == null) return;

        string displayName = _currentMonster.Data != null
            ? _currentMonster.Data.displayName
            : _currentMonster.name.Replace("(Clone)", "").Trim();

        string side = _currentMonster.IsEnemy
            ? "<color=#FF6666>Enemy</color>"
            : "<color=#66FF88>Ally</color>";

        if (_nameLabel  != null) _nameLabel.text  = displayName;
        if (_levelLabel != null) _levelLabel.text = $"Lv.{_currentMonster.Level}";
        if (_sideLabel  != null) _sideLabel.text  = side;

        if (_typeIcon != null && _currentMonster.Data != null)
        {
            var lib    = TypeIconLibrary.Instance;
            var sprite = lib != null ? lib.GetIcon(_currentMonster.Data.elementType) : null;
            if (sprite != null)
            {
                _typeIcon.style.backgroundImage = new StyleBackground(sprite);
                _typeIcon.style.display = DisplayStyle.Flex;
            }
            else
            {
                _typeIcon.style.display = DisplayStyle.None;
            }
        }

        _hpLabel.text =
            $"HP   <color=#66FF88>{_currentMonster.CurrentHP}</color> / {_currentMonster.MaxHP}";

        _statsLabel.text =
            $"ATK       {_currentMonster.Attack}\n" +
            $"DEF       {_currentMonster.Defense}\n" +
            $"SPD       {_currentMonster.Speed}  ({_currentMonster.TilesPerAP} tile(s)/AP)\n" +
            $"DGE       {_currentMonster.Dodge}\n" +
            $"CRT Chance  {_currentMonster.CritRate}%\n" +
            $"CRT Damage  ×{_currentMonster.CritMod}";

        RefreshStages();
    }

    private void RefreshStages()
    {
        if (_currentMonster == null || _stagesLabel == null) return;

        int atkS  = _currentMonster.GetCurrentStage(AttackEnum.AttackBuffType.Attack);
        int defS  = _currentMonster.GetCurrentStage(AttackEnum.AttackBuffType.Defense);
        int spdS  = _currentMonster.GetCurrentStage(AttackEnum.AttackBuffType.Speed);
        int dgeS  = _currentMonster.GetCurrentStage(AttackEnum.AttackBuffType.Dodge);
        int crtRS = _currentMonster.GetCurrentStage(AttackEnum.AttackBuffType.CritRate);
        int crtMS = _currentMonster.GetCurrentStage(AttackEnum.AttackBuffType.CritMod);

        _stagesLabel.text =
            $"{StageTag(atkS)}\n"  +
            $"{StageTag(defS)}\n"  +
            $"{StageTag(spdS)}\n"  +
            $"{StageTag(dgeS)}\n"  +
            $"{StageTag(crtRS)}\n" +
            $"{StageTag(crtMS)}";
    }

    private void RefreshStatuses()
    {
        if (_currentMonster == null || _statusLabel == null) return;

        var statuses = _currentMonster.ActiveStatuses;
        if (statuses == null || statuses.Count == 0)
        {
            _statusLabel.text = "<color=#444455>None</color>";
            return;
        }

        var parts = new System.Text.StringBuilder();
        for (int i = 0; i < statuses.Count; i++)
        {
            if (i > 0) parts.Append("  ");
            var s = statuses[i];
            string color = StatusColor(s.data.ID);
            parts.Append($"<color={color}>{s.data.ID}</color>");
            if (s.remainingTurns > 0)
                parts.Append($"<color=#888899>({s.remainingTurns})</color>");
        }
        _statusLabel.text = parts.ToString();
    }

    private static string StatusColor(AttackEnum.StatusEffect id) => id switch
    {
        AttackEnum.StatusEffect.Burn    => "#FF7744",
        AttackEnum.StatusEffect.Freeze  => "#66CCFF",
        AttackEnum.StatusEffect.Shock   => "#FFEE22",
        AttackEnum.StatusEffect.Poison  => "#BB44FF",
        AttackEnum.StatusEffect.Sleep   => "#AABBCC",
        _                               => "#CCCCCC"
    };

    private static string StageTag(int stage)
    {
        if (stage > 0) return $"<color=#66FF44>+{stage}</color>";
        if (stage < 0) return $"<color=#FF6666>{stage}</color>";
        return "<color=#444455> —</color>";
    }

    // ── UI Construction ────────────────────────────────────────────────────────

    private void BuildUI()
    {
        var s = UIStyleConfig.Load();

        if (s?.panelSettings == null || s?.monsterInfoPanelUXML == null)
        {
            Debug.LogWarning("[MonsterInfoPanel] PanelSettings or UXML not assigned in UIStyleConfig — panel disabled.");
            return;
        }

        _uiDoc = gameObject.AddComponent<UIDocument>();
        _uiDoc.panelSettings   = s.panelSettings;
        _uiDoc.sortingOrder    = 1;
        _uiDoc.visualTreeAsset = s.monsterInfoPanelUXML;

        var docRoot = _uiDoc.rootVisualElement;
        _root = docRoot.Q("panel-root");

        if (_root == null)
        {
            Debug.LogError("[MonsterInfoPanel] 'panel-root' element not found in UXML.");
            return;
        }

        // Apply panel sprite / fallback colour to the whole panel.
        // The top-bar is transparent in USS so the sprite's built-in dark header shows through.
        UIStyleConfig.ApplySprite(_root, s.panelSprite, s.infoPanelColor);

        // Apply button sprite / colour to close button
        var closeBtn = _root.Q<Button>("close-button");
        if (closeBtn != null)
        {
            UIStyleConfig.ApplySprite(closeBtn, s.buttonSprite, s.closeButtonColor);
            closeBtn.clicked += Hide;
        }

        // Cache element references
        _typeIcon    = _root.Q("type-icon");
        _nameLabel   = _root.Q<Label>("name-label");
        _levelLabel  = _root.Q<Label>("level-label");
        _sideLabel   = _root.Q<Label>("side-label");
        _hpLabel     = _root.Q<Label>("hp-label");
        _statsLabel  = _root.Q<Label>("stats-label");
        _stagesLabel = _root.Q<Label>("stages-label");
        _statusLabel = _root.Q<Label>("status-label");
    }
}
