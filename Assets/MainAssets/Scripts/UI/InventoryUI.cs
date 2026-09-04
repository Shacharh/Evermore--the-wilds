using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Inventory popup panel (UI Toolkit).
///
/// AUTO-SETUP: creates itself at runtime — no scene placement required.
/// Requires UIStyleConfig (Resources/UIStyleConfig) to have a PanelSettings assigned.
///
/// HUDController adds the "INVENTORY" button that calls InventoryUI.Instance.Toggle().
/// </summary>
public class InventoryUI : MonoBehaviour
{
    public static InventoryUI Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        new GameObject("InventoryUI").AddComponent<InventoryUI>();
    }

    // ── Colours ────────────────────────────────────────────────────────────────
    private static readonly Color PanelBg       = new Color(0.08f, 0.08f, 0.12f, 0.97f);
    private static readonly Color HeaderBg      = new Color(0.15f, 0.15f, 0.22f, 1f);
    private static readonly Color ItemCardBg    = new Color(0.18f, 0.18f, 0.26f, 1f);
    private static readonly Color ItemCardHover = new Color(0.25f, 0.25f, 0.36f, 1f);
    private static readonly Color UseButtonBg   = new Color(0.2f,  0.6f,  0.3f,  1f);
    private static readonly Color UseButtonHov  = new Color(0.3f,  0.8f,  0.45f, 1f);
    private static readonly Color CloseButtonBg = new Color(0.5f,  0.15f, 0.15f, 1f);
    private static readonly Color TextPrimary   = Color.white;
    private static readonly Color TextSecondary = new Color(0.7f, 0.7f, 0.7f, 1f);
    private static readonly Color TextQty       = new Color(0.6f, 0.9f, 1f,  1f);

    private UIDocument    _doc;
    private VisualElement _root;

    public bool IsOpen { get; private set; }

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        var s = UIStyleConfig.Load();
        if (s?.panelSettings == null)
        {
            Debug.LogWarning("[InventoryUI] PanelSettings not set in UIStyleConfig — inventory disabled.");
            return;
        }

        _doc               = gameObject.AddComponent<UIDocument>();
        _doc.panelSettings = s.panelSettings;
        _doc.sortingOrder  = 200; // above HUDController (100) and PauseMenu (10)

        _root              = _doc.rootVisualElement;
        _root.pickingMode  = PickingMode.Ignore;
        _root.style.display   = DisplayStyle.None;
        _root.style.position  = Position.Absolute;
        _root.style.left      = 0; _root.style.right  = 0;
        _root.style.top       = 0; _root.style.bottom = 0;
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    public void Open()
    {
        if (_root == null) return;
        if (!CanOpenInventory()) return;
        BuildUI();
        _root.pickingMode      = PickingMode.Position;
        _root.style.display    = DisplayStyle.Flex;
        IsOpen = true;
    }

    public void Close()
    {
        if (_root == null) return;
        _root.style.display = DisplayStyle.None;
        _root.pickingMode   = PickingMode.Ignore;
        IsOpen = false;
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else        Open();
    }

    // ── UI Build ───────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        _root.Clear();

        // Full-screen dimmer — clicking outside the panel closes inventory
        // Also serves as the flex container that centers the panel
        var overlay = new VisualElement();
        overlay.style.position        = Position.Absolute;
        overlay.style.left = 0; overlay.style.right  = 0;
        overlay.style.top  = 0; overlay.style.bottom = 0;
        overlay.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.4f));
        overlay.style.alignItems      = Align.Center;
        overlay.style.justifyContent  = Justify.Center;
        overlay.RegisterCallback<ClickEvent>(_ => Close());
        _root.Add(overlay);

        // ── Panel (centered inside overlay) ───────────────────────────────────
        var panel = new VisualElement();
        panel.style.width           = 360;
        panel.style.maxHeight       = 520;
        panel.style.backgroundColor = new StyleColor(PanelBg);
        panel.style.borderTopLeftRadius     = 10;
        panel.style.borderTopRightRadius    = 10;
        panel.style.borderBottomLeftRadius  = 10;
        panel.style.borderBottomRightRadius = 10;
        panel.style.flexDirection = FlexDirection.Column;
        panel.RegisterCallback<ClickEvent>(e => e.StopPropagation());
        overlay.Add(panel);

        // ── Header ────────────────────────────────────────────────────────────
        var header = new VisualElement();
        header.style.flexDirection      = FlexDirection.Row;
        header.style.justifyContent     = Justify.SpaceBetween;
        header.style.alignItems         = Align.Center;
        header.style.backgroundColor    = new StyleColor(HeaderBg);
        header.style.paddingLeft        = 14;
        header.style.paddingRight       = 10;
        header.style.paddingTop         = 10;
        header.style.paddingBottom      = 10;
        header.style.borderTopLeftRadius  = 10;
        header.style.borderTopRightRadius = 10;
        panel.Add(header);

        var title = new Label("INVENTORY");
        title.style.color                   = new StyleColor(TextPrimary);
        title.style.fontSize                = 16;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.Add(title);

        var closeBtn = MakeButton("✕", CloseButtonBg, new Color(0.7f, 0.2f, 0.2f, 1f), Close);
        closeBtn.style.width = 28; closeBtn.style.height = 28; closeBtn.style.fontSize = 14;
        header.Add(closeBtn);

        // ── Scrollable content ────────────────────────────────────────────────
        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.flexGrow      = 1;
        scroll.style.paddingLeft   = 10;
        scroll.style.paddingRight  = 10;
        scroll.style.paddingTop    = 8;
        scroll.style.paddingBottom = 12;
        panel.Add(scroll);

        var inventory = PlayerInventory.Instance;
        if (inventory == null)
        {
            scroll.Add(MakeInfoLabel("No inventory found in scene."));
            return;
        }

        var items = inventory.GetAll();
        if (items.Count == 0)
        {
            scroll.Add(MakeInfoLabel("Inventory is empty."));
            return;
        }

        // Group by archetype
        var groups = new Dictionary<ItemEnum.Archetype, List<(ItemData, int)>>();
        foreach (var entry in items)
        {
            if (!groups.ContainsKey(entry.item.Archetype))
                groups[entry.item.Archetype] = new List<(ItemData, int)>();
            groups[entry.item.Archetype].Add(entry);
        }

        foreach (ItemEnum.Archetype archetype in System.Enum.GetValues(typeof(ItemEnum.Archetype)))
        {
            if (!groups.TryGetValue(archetype, out var group)) continue;

            var sectionLabel = new Label(ArchetypeLabel(archetype));
            sectionLabel.style.color                   = new StyleColor(TextSecondary);
            sectionLabel.style.fontSize                = 11;
            sectionLabel.style.marginTop               = 10;
            sectionLabel.style.marginBottom            = 4;
            sectionLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            scroll.Add(sectionLabel);

            foreach (var (item, qty) in group)
                scroll.Add(MakeItemCard(item, qty));
        }
    }

    private VisualElement MakeItemCard(ItemData item, int qty)
    {
        var card = new VisualElement();
        card.style.flexDirection   = FlexDirection.Row;
        card.style.alignItems      = Align.Center;
        card.style.backgroundColor = new StyleColor(ItemCardBg);
        card.style.borderTopLeftRadius     = 6;
        card.style.borderTopRightRadius    = 6;
        card.style.borderBottomLeftRadius  = 6;
        card.style.borderBottomRightRadius = 6;
        card.style.marginBottom  = 4;
        card.style.paddingLeft   = 10;
        card.style.paddingRight  = 8;
        card.style.paddingTop    = 8;
        card.style.paddingBottom = 8;

        card.RegisterCallback<MouseEnterEvent>(_ => card.style.backgroundColor = new StyleColor(ItemCardHover));
        card.RegisterCallback<MouseLeaveEvent>(_ => card.style.backgroundColor = new StyleColor(ItemCardBg));

        var info = new VisualElement();
        info.style.flexGrow = 1;
        card.Add(info);

        var nameRow = new VisualElement();
        nameRow.style.flexDirection = FlexDirection.Row;
        nameRow.style.alignItems    = Align.Center;
        info.Add(nameRow);

        var nameLabel = new Label(item.DisplayName);
        nameLabel.style.color    = new StyleColor(TextPrimary);
        nameLabel.style.fontSize = 13;
        nameRow.Add(nameLabel);

        var qtyLabel = new Label($" ×{qty}");
        qtyLabel.style.color    = new StyleColor(TextQty);
        qtyLabel.style.fontSize = 12;
        nameRow.Add(qtyLabel);

        if (!string.IsNullOrEmpty(item.Description))
        {
            var desc = new Label(item.Description);
            desc.style.color      = new StyleColor(TextSecondary);
            desc.style.fontSize   = 10;
            desc.style.whiteSpace = WhiteSpace.Normal;
            info.Add(desc);
        }

        if (item.APCost > 0)
        {
            var apTag = new Label($"{item.APCost} AP");
            apTag.style.color       = new StyleColor(new Color(1f, 0.85f, 0.3f, 1f));
            apTag.style.fontSize    = 10;
            apTag.style.marginRight = 6;
            card.Add(apTag);
        }

        var useBtn = MakeButton("Use", UseButtonBg, UseButtonHov, () => OnUseItem(item));
        useBtn.style.width = 46; useBtn.style.height = 28; useBtn.style.fontSize = 12;
        card.Add(useBtn);

        return card;
    }

    private static Button MakeButton(string text, Color normal, Color hover, System.Action onClick)
    {
        var btn = new Button(onClick) { text = text };
        btn.style.backgroundColor             = new StyleColor(normal);
        btn.style.color                       = new StyleColor(Color.white);
        btn.style.borderTopWidth              = 0; btn.style.borderBottomWidth = 0;
        btn.style.borderLeftWidth             = 0; btn.style.borderRightWidth  = 0;
        btn.style.borderTopLeftRadius         = 5;
        btn.style.borderTopRightRadius        = 5;
        btn.style.borderBottomLeftRadius      = 5;
        btn.style.borderBottomRightRadius     = 5;
        btn.RegisterCallback<MouseEnterEvent>(_ => btn.style.backgroundColor = new StyleColor(hover));
        btn.RegisterCallback<MouseLeaveEvent>(_ => btn.style.backgroundColor = new StyleColor(normal));
        return btn;
    }

    private static Label MakeInfoLabel(string text)
    {
        var lbl = new Label(text);
        lbl.style.color             = new StyleColor(TextSecondary);
        lbl.style.marginTop         = 10;
        lbl.style.unityTextAlign    = TextAnchor.MiddleCenter;
        return lbl;
    }

    // ── Item Use Logic ─────────────────────────────────────────────────────────

    private void OnUseItem(ItemData item)
    {
        if (!CanOpenInventory()) { Close(); return; }

        var ptc       = FindFirstObjectByType<PlayerTurnController>();
        var inventory = PlayerInventory.Instance;
        if (inventory == null || ptc == null) return;

        if (!inventory.HasItem(item))
        {
            BattleMessage.Show($"No {item.DisplayName} left!", 1.5f);
            Open(); // rebuild with updated quantities
            return;
        }

        switch (item.Archetype)
        {
            case ItemEnum.Archetype.Healing:
                HandleHealingItem(item, ptc, inventory);
                break;

            case ItemEnum.Archetype.Revival:
                BattleMessage.Show("Revival items require the Reaction System — coming soon!", 2.5f);
                break;

            case ItemEnum.Archetype.BuffDebuff:
                HandleBuffDebuffItem(item);
                break;

            case ItemEnum.Archetype.APAffecting:
                HandleAPItem(item, ptc, inventory);
                break;

            case ItemEnum.Archetype.AcceptanceRateEnhancing:
                HandleAcceptanceRateItem(item, ptc, inventory);
                break;

            case ItemEnum.Archetype.DialogAssist:
                BattleMessage.Show("Use Dialog Assist items from within an active dialogue session!", 2.5f);
                break;
        }
    }

    private void HandleHealingItem(ItemData item, PlayerTurnController ptc, PlayerInventory inventory)
    {
        if (item.HealMode == ItemEnum.HealMode.PartyHeal)
        {
            if (!ptc.TrySpendAPForItem(item.APCost)) return;
            inventory.RemoveItem(item);
            ApplyPartyHeal(item);
            BattleMessage.Show($"Used {item.DisplayName}! All allies healed.", 2f);
            Close();
        }
        else
        {
            // Targeted or AreaHeal — hand off to InputManager
            Close();
            FindFirstObjectByType<InputManager>()?.BeginItemTargeting(item);
        }
    }

    private void HandleBuffDebuffItem(ItemData item)
    {
        if (item.StatusEffect == null)
        {
            BattleMessage.Show($"{item.DisplayName} has no status effect configured!", 2f);
            return;
        }
        Close();
        FindFirstObjectByType<InputManager>()?.BeginItemTargeting(item);
    }

    private void HandleAcceptanceRateItem(ItemData item, PlayerTurnController ptc, PlayerInventory inventory)
    {
        if (item.APCost > 0 && !ptc.TrySpendAPForItem(item.APCost)) return;
        inventory.RemoveItem(item);
        TamingSystem.Instance?.AddAcceptanceBonus(item.AcceptanceBonus);
        string pct = $"{item.AcceptanceBonus * 100f:F0}%";
        BattleMessage.Show($"Used {item.DisplayName}! +{pct} acceptance on next dialogue.", 2.5f);
        Close();
    }

    private void HandleAPItem(ItemData item, PlayerTurnController ptc, PlayerInventory inventory)
    {
        if (item.APCost > 0 && !ptc.TrySpendAPForItem(item.APCost)) return;
        inventory.RemoveItem(item);

        if (item.APDelta > 0)
            ptc.GainAP(item.APDelta);
        else if (item.APDelta < 0)
            ptc.SpendAP(-item.APDelta);

        string sign = item.APDelta >= 0 ? "+" : "";
        BattleMessage.Show($"Used {item.DisplayName}! {sign}{item.APDelta} AP.", 2f);
        Close();
        Open(); // reopen so player can use another item
    }

    // ── Immediate Effects ──────────────────────────────────────────────────────

    private static void ApplyPartyHeal(ItemData item)
    {
        var all = FindObjectsByType<Monster>(FindObjectsSortMode.None);
        foreach (var m in all)
        {
            if (m == null || m.IsEnemy || !m.IsAlive) continue;
            int amount = item.HealAmount + Mathf.RoundToInt(m.MaxHP * item.HealPercent);
            m.HealHP(amount);
            if (item.ClearsStatusEffects) m.ClearAllStatuses();
        }
    }

    // ── Guard ──────────────────────────────────────────────────────────────────

    private static bool CanOpenInventory()
    {
        if (TurnManager.Instance != null && !TurnManager.Instance.IsPlayerTurn)
        {
            BattleMessage.Show("You can only use items on your turn!", 2f);
            return false;
        }
        return true;
    }

    private static string ArchetypeLabel(ItemEnum.Archetype a) => a switch
    {
        ItemEnum.Archetype.Healing                 => "HEALING",
        ItemEnum.Archetype.Revival                 => "REVIVAL",
        ItemEnum.Archetype.BuffDebuff              => "BUFFS / DEBUFFS",
        ItemEnum.Archetype.APAffecting             => "ACTION POINTS",
        ItemEnum.Archetype.AcceptanceRateEnhancing => "ACCEPTANCE RATE",
        ItemEnum.Archetype.DialogAssist            => "DIALOG ASSIST",
        _                                          => a.ToString().ToUpper()
    };
}
