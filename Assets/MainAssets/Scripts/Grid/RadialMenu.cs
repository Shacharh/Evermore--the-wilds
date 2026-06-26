using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

/// <summary>
/// Context menu rendered through a UIDocument (UI Toolkit).
///
/// Structure lives in UXML / USS — C# only:
///   1. Repositions #radial-pivot each frame to follow the target.
///   2. Clones RadialMenuCard.uxml once per action into #menu-list.
///   3. Sets the card-label text and registers pointer events.
///
/// To edit the card visual design open RadialMenuCard.uxml in UI Builder.
/// To edit colours / sizing open RadialMenu.uss.
/// </summary>
public class RadialMenu : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private RadialMenuConfig  menuConfig;
    [SerializeField] private VisualTreeAsset   cardTemplate;   // assign RadialMenuCard.uxml

    // ── Internal state ─────────────────────────────────────────────────────────

    private Tile         targetTile;
    private InputManager inputManager;
    private Camera       mainCamera;
    private Vector3      menuWorldCenter;

    private UIDocument    _uiDoc;
    private VisualElement _pivot;
    private VisualElement _list;
    private float         _createdAt;
    private int           _cardCount;

    private const float ClickGuardSeconds = 0.15f;

    // Card dimensions — keep in sync with .menu-card in RadialMenu.uss
    private const float CardHeight = 60f;
    private const float CardGap    =  6f;

    /// <summary>True while the pointer is over any card — read by InputManager.</summary>
    public bool IsHoveringButton { get; private set; }

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    void Awake()
    {
        _uiDoc     = GetComponent<UIDocument>();
        _pivot     = _uiDoc.rootVisualElement.Q("radial-pivot");
        _list      = _uiDoc.rootVisualElement.Q("menu-list");
        _createdAt = Time.realtimeSinceStartup;
    }

    // ── Public Initialisation ──────────────────────────────────────────────────

    public void Initialize(Tile tile, InputManager manager)
    {
        targetTile      = tile;
        inputManager    = manager;
        mainCamera      = Camera.main;
        menuWorldCenter = tile.transform.position + Vector3.up * 2.0f;

        BuildTileCards();
        CentreList();
        UpdatePivotPosition();

        Debug.Log($"[RadialMenu] Opened for tile {tile.GridPosition}");
    }

    public void InitializeAsAttackMenu(Monster monster, InputManager manager)
    {
        targetTile      = null;
        inputManager    = manager;
        mainCamera      = Camera.main;
        menuWorldCenter = monster.transform.position + Vector3.up * 2.0f;

        var attacks = monster.GetAttacks();
        if (attacks == null || attacks.Count == 0)
        {
            Debug.LogWarning($"[RadialMenu] {monster.name} has no attacks.");
            return;
        }

        _cardCount = 0;
        for (int i = 0; i < attacks.Count; i++)
        {
            RadialActionType actionType = i == 0 ? RadialActionType.UseAttack0
                                        : i == 1 ? RadialActionType.UseAttack1
                                                 : RadialActionType.UseAttack2;
            AddCard(attacks[i].data.DisplayName, actionType, attacks[i].data);
        }

        CentreList();
        UpdatePivotPosition();
        Debug.Log($"[RadialMenu] Attack sub-menu ready ({_cardCount} card(s)).");
    }

    // ── Update ─────────────────────────────────────────────────────────────────

    void Update() => UpdatePivotPosition();

    private void UpdatePivotPosition()
    {
        if (mainCamera == null || _pivot == null) return;

        Vector3 sp = mainCamera.WorldToScreenPoint(menuWorldCenter);
        if (sp.z < 0f) { sp.x = Screen.width - sp.x; sp.y = Screen.height - sp.y; }

        var     panel = _uiDoc.rootVisualElement.panel;
        Vector2 pp    = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(sp.x, sp.y));

        var rect = _uiDoc.rootVisualElement.contentRect;
        if (rect.width > 0f && rect.height > 0f)
        {
            float listH   = _cardCount * CardHeight + Mathf.Max(0, _cardCount - 1) * CardGap;
            float marginX = 370f;                  // list left-offset + card width + padding
            float marginY = listH * 0.5f + 20f;
            pp.x = Mathf.Clamp(pp.x, 20f,     rect.width  - marginX);
            pp.y = Mathf.Clamp(pp.y, marginY,  rect.height - marginY);
        }

        _pivot.style.left = pp.x;
        _pivot.style.top  = pp.y;
    }

    // ── Tile card builder ──────────────────────────────────────────────────────

    private void BuildTileCards()
    {
        _cardCount = 0;

        List<MenuOptionData> options;
        if (menuConfig == null)
        {
            options = GetFallbackOptions();
        }
        else
        {
            Monster m = targetTile.GetMonster();
            if      (m != null && m.IsEnemy) options = menuConfig.menuConfig.enemyMonsterOptions;
            else if (m != null)              options = menuConfig.menuConfig.playerMonsterOptions;
            else                             options = menuConfig.menuConfig.emptyTileOptions;
        }

        var valid = (options ?? new List<MenuOptionData>())
            .FindAll(o => o != null && !string.IsNullOrWhiteSpace(o.label));

        if (valid.Count == 0) valid = GetFallbackOptions();

        foreach (var opt in valid)
            AddCard(opt.label, opt.actionType);
    }

    // ── Card cloning ───────────────────────────────────────────────────────────

    private void AddCard(string label, RadialActionType actionType, AttackData attackData = null)
    {
        if (cardTemplate == null)
        {
            Debug.LogError("[RadialMenu] cardTemplate not assigned — assign RadialMenuCard.uxml in the Inspector.");
            return;
        }

        // Clone the UXML template — structure and styling come from the asset
        var container = cardTemplate.CloneTree();

        // Set the label text — attack cards get the element type as a coloured second line.
        // Using the existing card-label avoids adding elements that fight the UXML layout.
        var lbl = container.Q<Label>("card-label");
        if (lbl != null)
        {
            if (attackData != null)
            {
                lbl.style.whiteSpace = WhiteSpace.Normal;
                string hex = ColorUtility.ToHtmlStringRGB(ElementColor(attackData.Element));
                lbl.text = label.ToUpper()
                           + "\n<size=9><color=#" + hex + ">"
                           + attackData.Element.ToString().ToUpper()
                           + "</color></size>";
            }
            else
            {
                lbl.text = label.ToUpper();
            }
        }

        // Wire events on the inner .menu-card element
        var card = container.Q(className: "menu-card");
        if (card == null) card = container; // fallback: use container root

        // Apply button sprite (overrides the card's USS background-color)
        var style = UIStyleConfig.Load();
        UIStyleConfig.ApplySprite(card, style?.buttonSprite, new Color(0.04f, 0.04f, 0.06f, 0.82f));

        // AP cost label — shown only on attack cards
        var apLbl = container.Q<Label>("card-ap");
        if (apLbl != null)
        {
            if (attackData != null)
            {
                apLbl.text = $"{attackData.ConsumeActionPoints} AP";
                apLbl.style.display = DisplayStyle.Flex;
            }
            else
            {
                apLbl.style.display = DisplayStyle.None;
            }
        }

        card.RegisterCallback<PointerEnterEvent>(_ =>
        {
            IsHoveringButton = true;
            if (attackData != null)
            {
                AttackInfoPanel.Show(attackData);
                inputManager.PreviewAttackRange(attackData);
            }
        });

        card.RegisterCallback<PointerLeaveEvent>(_ =>
        {
            IsHoveringButton = false;
            AttackInfoPanel.Hide();
            if (attackData != null)
                inputManager.ClearAttackRangePreview();
        });

        card.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (Time.realtimeSinceStartup - _createdAt < ClickGuardSeconds) return;
            if (evt.button != 0) return;
            evt.StopPropagation();
            OnActionSelected(actionType);
        });

        _list.Add(container);
        _cardCount++;
    }

    // ── Vertical centring ──────────────────────────────────────────────────────

    private void CentreList()
    {
        float totalH = _cardCount * CardHeight + Mathf.Max(0, _cardCount - 1) * CardGap;
        _list.style.top = -(totalH * 0.5f);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static List<MenuOptionData> GetFallbackOptions() => new List<MenuOptionData>
    {
        new MenuOptionData { label = "Move",   actionType = RadialActionType.Movement },
        new MenuOptionData { label = "Attack", actionType = RadialActionType.Attack   },
        new MenuOptionData { label = "Info",   actionType = RadialActionType.Info     }
    };

    private void OnActionSelected(RadialActionType type)
    {
        Debug.Log($"[RadialMenu] Action selected: {type}");
        inputManager.HandleRadialAction(type, targetTile);
        Close();
    }

    public void Close()
    {
        _list?.Clear();
        Destroy(gameObject);
    }

    private static Color ElementColor(AttackEnum.ElementType element) => element switch
    {
        AttackEnum.ElementType.Fire     => new Color(1.00f, 0.40f, 0.15f),
        AttackEnum.ElementType.Water    => new Color(0.25f, 0.65f, 1.00f),
        AttackEnum.ElementType.Wind     => new Color(0.55f, 0.90f, 0.75f),
        AttackEnum.ElementType.Earth    => new Color(0.70f, 0.50f, 0.20f),
        AttackEnum.ElementType.Poison   => new Color(0.65f, 0.25f, 0.85f),
        AttackEnum.ElementType.Electric => new Color(1.00f, 0.90f, 0.10f),
        AttackEnum.ElementType.Plant    => new Color(0.30f, 0.80f, 0.30f),
        AttackEnum.ElementType.Metal    => new Color(0.80f, 0.85f, 0.90f),
        _                               => Color.white
    };
}
