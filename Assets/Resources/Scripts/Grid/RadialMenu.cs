using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// Radial context menu rendered as a ScreenSpaceOverlay canvas.
///
/// ScreenSpaceOverlay means:
///   • Always on top — never blocked by terrain, monsters, or any 3D object.
///   • Static — no billboard rotation; buttons appear flat like HUD elements.
///   • Position tracks the tile / monster in screen-space each frame.
///
/// The menu contains a "MenuPivot" child RectTransform that is repositioned
/// every frame to match the target's screen position. All buttons are children
/// of MenuPivot and keep their circle offsets unchanged.
/// </summary>
public class RadialMenu : MonoBehaviour
{
    [Header("Menu Settings")]
    [Tooltip("Radius of the button circle in screen pixels.")]
    [SerializeField] private float menuRadius = 150f;
    [SerializeField] private GameObject buttonPrefab;

    [Header("Configuration")]
    [SerializeField] private RadialMenuConfig menuConfig;

    // -- Internal state --------------------------------------------------------

    private Tile         targetTile;
    private InputManager inputManager;
    private Camera       mainCamera;
    private Canvas       canvas;
    private RectTransform rectTransform;

    /// <summary>Child pivot that moves to the tile's screen position each frame.</summary>
    private RectTransform menuPivot;

    /// <summary>World-space point the menu is anchored to (tile pos + offset).</summary>
    private Vector3 menuWorldCenter;

    private List<RadialMenuButton> buttons = new List<RadialMenuButton>();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        canvas        = GetComponent<Canvas>();
        rectTransform = GetComponent<RectTransform>();

        SetupCanvas();
        EnsureEventSystem();
    }

    // ── Canvas Setup ──────────────────────────────────────────────────────────

    private void SetupCanvas()
    {
        // ScreenSpaceOverlay: always renders on top of all 3D geometry.
        // No world camera needed. No billboard rotation needed.
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        canvas.transform.localScale = Vector3.one;

        // Stretch the canvas rect to fill the entire screen
        rectTransform.anchorMin        = Vector2.zero;
        rectTransform.anchorMax        = Vector2.one;
        rectTransform.sizeDelta        = Vector2.zero;
        rectTransform.anchoredPosition = Vector2.zero;

        // GraphicRaycaster handles button clicks in screen-space
        if (GetComponent<GraphicRaycaster>() == null)
        {
            var gr = gameObject.AddComponent<GraphicRaycaster>();
            gr.blockingObjects = GraphicRaycaster.BlockingObjects.None;
        }

        // CanvasScaler: scales every button/text relative to a 1920×1080 reference
        // resolution, so the menu looks the same on any screen size.
        // Without this, ScreenSpaceOverlay renders buttons at raw pixel size
        // (e.g. 120 px wide on a 1920-wide screen looks tiny).
        var scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode    = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;   // balanced width+height scaling

        // MenuPivot — the single child whose anchoredPosition tracks the tile
        var pivotGO = new GameObject("MenuPivot");
        pivotGO.transform.SetParent(transform, false);
        menuPivot              = pivotGO.AddComponent<RectTransform>();
        menuPivot.anchorMin    = new Vector2(0.5f, 0.5f);
        menuPivot.anchorMax    = new Vector2(0.5f, 0.5f);
        menuPivot.sizeDelta    = Vector2.zero;
        menuPivot.pivot        = new Vector2(0.5f, 0.5f);
        menuPivot.anchoredPosition = Vector2.zero;
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
        }
    }

    // ── Public Initialisation ─────────────────────────────────────────────────

    /// <summary>Opens the normal tile-based radial menu (Move / Attack / Info).</summary>
    public void Initialize(Tile tile, InputManager manager)
    {
        targetTile      = tile;
        inputManager    = manager;
        mainCamera      = Camera.main;
        menuWorldCenter = tile.transform.position + Vector3.up * 2.0f;

        ClearButtons();
        CreateMenuButtons();
        UpdatePivotPosition();   // snap to correct screen pos on first frame

        Debug.Log($"[RadialMenu] Opened for tile {tile.GridPosition}");
    }

    /// <summary>Opens an attack sub-menu listing the monster's learned attacks.</summary>
    public void InitializeAsAttackMenu(Monster monster, InputManager manager)
    {
        targetTile      = null;
        inputManager    = manager;
        mainCamera      = Camera.main;
        menuWorldCenter = monster.transform.position + Vector3.up * 2.0f;

        ClearButtons();

        var attacks = monster.GetAttacks();
        if (attacks == null || attacks.Count == 0)
        {
            Debug.LogWarning($"[RadialMenu] {monster.name} has no attacks to show.");
            return;
        }

        float angleStep = 360f / attacks.Count;

        for (int i = 0; i < attacks.Count; i++)
        {
            RadialActionType actionType = (i == 0)
                ? RadialActionType.UseAttack0
                : RadialActionType.UseAttack1;

            float   angle  = i * angleStep * Mathf.Deg2Rad;
            Vector2 offset = new Vector2(Mathf.Cos(angle) * menuRadius,
                                         Mathf.Sin(angle) * menuRadius);

            GameObject      buttonObj = Instantiate(buttonPrefab, menuPivot);

            // Same localScale reset as CreateMenuButtons() — see comment there.
            buttonObj.transform.localScale = Vector3.one;

            RadialMenuButton btn      = buttonObj.GetComponent<RadialMenuButton>();

            var btnRect = buttonObj.GetComponent<RectTransform>();
            btnRect.anchoredPosition = offset;
            btnRect.sizeDelta        = new Vector2(260f, 100f);

            btn.Setup(attacks[i].data.DisplayName, null, actionType, OnActionSelected);
            buttons.Add(btn);

            Debug.Log($"[RadialMenu] Attack btn {i}: '{attacks[i].data.DisplayName}' → {actionType}");
        }

        UpdatePivotPosition();
        Debug.Log($"[RadialMenu] Attack sub-menu ready ({buttons.Count} button(s)).");
    }

    // ── Update: track tile's screen position ──────────────────────────────────

    void Update()
    {
        UpdatePivotPosition();
    }

    private void UpdatePivotPosition()
    {
        if (mainCamera == null || menuPivot == null) return;

        Vector3 screenPos = mainCamera.WorldToScreenPoint(menuWorldCenter);

        // Hide the pivot (and all buttons) if the anchor is behind the camera
        if (screenPos.z < 0f)
        {
            menuPivot.gameObject.SetActive(false);
            return;
        }
        menuPivot.gameObject.SetActive(true);

        // Convert the screen-space pixel coordinate into the canvas's LOCAL space.
        // This MUST use RectTransformUtility — NOT the manual "screenPos - screenCenter"
        // formula — because CanvasScaler (ScaleWithScreenSize) changes the canvas's
        // internal unit scale, so raw screen pixels no longer map 1:1 to canvas units.
        // Pass null as the camera parameter for ScreenSpaceOverlay canvases.
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform, screenPos, null, out Vector2 localPoint);
        menuPivot.anchoredPosition = localPoint;
    }

    // ── Button Creation ───────────────────────────────────────────────────────

    void CreateMenuButtons()
    {
        List<MenuOptionData> optionsToShow;

        if (menuConfig == null)
        {
            Debug.LogWarning("[RadialMenu] menuConfig not assigned — using fallback options.");
            optionsToShow = GetFallbackOptions();
        }
        else
        {
            Monster m = targetTile.GetMonster();
            if      (m != null && m.IsEnemy) optionsToShow = menuConfig.menuConfig.enemyMonsterOptions;
            else if (m != null)              optionsToShow = menuConfig.menuConfig.playerMonsterOptions;
            else                             optionsToShow = menuConfig.menuConfig.emptyTileOptions;
        }

        if (optionsToShow == null || optionsToShow.Count == 0)
        {
            Debug.LogWarning($"[RadialMenu] No options for tile {targetTile?.GridPosition}. Using fallback.");
            optionsToShow = GetFallbackOptions();
        }

        float angleStep = 360f / optionsToShow.Count;

        for (int i = 0; i < optionsToShow.Count; i++)
        {
            float   angle  = i * angleStep * Mathf.Deg2Rad;
            Vector2 offset = new Vector2(Mathf.Cos(angle) * menuRadius,
                                         Mathf.Sin(angle) * menuRadius);

            GameObject      buttonObj = Instantiate(buttonPrefab, menuPivot);

            // Reset localScale to (1,1,1) BEFORE Start() runs on the next frame.
            // The prefab may have a tiny localScale left over from WorldSpace days.
            // RadialMenuButton.Awake() captures localScale as originalScale (wrong),
            // but RadialMenuButton.Start() also captures it (correct after this reset).
            // Update() then lerps toward the Start()-captured scale, so the button
            // stays at full size instead of shrinking back to the prefab's micro-scale.
            buttonObj.transform.localScale = Vector3.one;

            RadialMenuButton btn      = buttonObj.GetComponent<RadialMenuButton>();

            // Position the button and force a readable screen-space size.
            // The prefab's saved sizeDelta was designed for WorldSpace canvas
            // (scale 0.05) and is far too small in ScreenSpaceOverlay pixels.
            var btnRect = buttonObj.GetComponent<RectTransform>();
            btnRect.anchoredPosition = offset;
            btnRect.sizeDelta        = new Vector2(260f, 100f);

            btn.Setup(optionsToShow[i].label, optionsToShow[i].icon,
                      optionsToShow[i].actionType, OnActionSelected);
            buttons.Add(btn);
        }

        Debug.Log($"[RadialMenu] Created {buttons.Count} button(s).");
    }

    private List<MenuOptionData> GetFallbackOptions() => new List<MenuOptionData>
    {
        new MenuOptionData { label = "Move",   actionType = RadialActionType.Movement },
        new MenuOptionData { label = "Attack", actionType = RadialActionType.Attack   },
        new MenuOptionData { label = "Info",   actionType = RadialActionType.Info     }
    };

    // ── Action Callback ───────────────────────────────────────────────────────

    private void OnActionSelected(RadialActionType type)
    {
        Debug.Log($"[RadialMenu] Action selected: {type}");
        inputManager.HandleRadialAction(type, targetTile);
        Close();
    }

    // ── Close / Clear ─────────────────────────────────────────────────────────

    public void Close()
    {
        ClearButtons();
        Destroy(gameObject);
    }

    private void ClearButtons()
    {
        foreach (var btn in buttons)
            if (btn != null) Destroy(btn.gameObject);
        buttons.Clear();
    }
}
