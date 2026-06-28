using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Handles all player input on the grid.
/// AP integration: before any action, asks PlayerTurnController whether
/// the AP can be afforded; HasActed is NOT checked — AP is the only gate.
/// </summary>
public class InputManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridManager          gridManager;
    [SerializeField] private RadialMenu           radialMenuPrefab;
    [SerializeField] private Camera               mainCamera;
    [SerializeField] private PlayerTurnController playerTurnController;
    [SerializeField] private CameraController     cameraController;

    [Header("Input Settings")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private LayerMask        tileLayerMask;
    [SerializeField] private LayerMask        monsterLayerMask;

    [Header("Movement Settings")]
    [SerializeField] private Color movementRangeColor = new Color(0.3f, 0.6f, 1f, 0.5f);
    [SerializeField] private Color selectedMoveColor  = new Color(0.2f, 1f, 0.3f, 0.7f);
    [SerializeField] private Color pathPreviewColor   = new Color(0.9f, 0.9f, 0.2f, 0.7f);
    [SerializeField] private float movementSpeed = 5f;

    [Header("Attack Settings")]
    [Tooltip("Dim highlight shown on ALL tiles in the attack's range/shape.")]
    [SerializeField] private Color attackRangeColor   = new Color(0.55f, 0.05f, 0.05f, 0.75f);
    [Tooltip("Bright pulse colour A for enemy tiles in attack range.")]
    [SerializeField] private Color attackPulseColorA  = new Color(1f, 0.10f, 0.10f, 0.95f);
    [Tooltip("Dim pulse colour B for enemy tiles in attack range.")]
    [SerializeField] private Color attackPulseColorB  = new Color(0.55f, 0.02f, 0.02f, 0.50f);
    [Tooltip("Pulse oscillations per second for attack-target tiles.")]
    [SerializeField] private float attackPulseSpeed   = 2.5f;
    [Tooltip("Horizontal jitter amplitude (world units) for enemy target tiles.")]
    [SerializeField] private float attackJitterAmplitude  = 0.05f;
    [Tooltip("Jitter frequency (cycles per second) for enemy target tiles.")]
    [SerializeField] private float attackJitterFrequency  = 10f;
    [Tooltip("Highlight color shown on AOE footprint tiles when hovering.")]
    [SerializeField] private Color aoeFootprintColor = new Color(1f, 0.5f, 0.05f, 0.75f);
    [Tooltip("Bright pulse colour A for ally-targeting attacks.")]
    [SerializeField] private Color allyPulseColorA = new Color(0.1f, 1f, 0.2f, 0.95f);
    [Tooltip("Dim pulse colour B for ally-targeting attacks.")]
    [SerializeField] private Color allyPulseColorB = new Color(0.02f, 0.55f, 0.05f, 0.50f);

    // -- Input Actions ---------------------------------------------------------

    private InputAction mousePositionAction;
    private InputAction leftClickAction;
    private InputAction rightClickAction;

    // -- Hover / Selection State -----------------------------------------------

    private Tile       currentHoveredTile;
    private Tile       selectedTile;
    private RadialMenu activeMenu;

    private float       menuOpenTime   = -1f;
    private const float MenuClickDelay = 0.2f;

    // -- Input State -----------------------------------------------------------

    private enum InputState { Normal, MovementMode, Moving, AttackSelection, TargetSelection }
    private InputState currentState = InputState.Normal;

    /// <summary>True once we have subscribed to playerTurnController.onTurnEnd.</summary>
    private bool _subscribedToTurnEnd = false;

    // Movement state
    private Monster                              movingMonster;
    private Tile                                 movementOriginTile;
    private System.Collections.Generic.List<Tile> validMovementTiles;
    /// <summary>Tiles covered per 1 AP for the currently-moving monster.</summary>
    private int                                  movingMonsterTilesPerAP;
    /// <summary>Currently previewed path tiles (highlighted yellow on hover).</summary>
    private System.Collections.Generic.List<Tile> previewedPathTiles;

    // Attack state
    private Monster                              attackingMonster;
    private AttackData                           selectedAttackData;
    private int                                  selectedAttackIndex;
    private System.Collections.Generic.List<Tile> validTargetTiles;    // all aim-zone tiles
    private System.Collections.Generic.List<Tile> currentAOEFootprint; // footprint on hovered tile
    private System.Collections.Generic.List<Tile> _previewRangeTiles;  // hover preview while in attack menu
    private RadialMenu                           activeAttackMenu;

    // -- Lifecycle -------------------------------------------------------------

    void Awake()
    {
        // The tile material is transparent. Original attackRangeColor alpha (0.35) made tiles
        // nearly invisible since alpha now controls actual opacity. Force a bright visible red
        // if the serialised value is still the old too-transparent one.
        if (attackRangeColor.a < 0.7f)
            attackRangeColor = new Color(0.9f, 0.05f, 0.05f, 0.85f);

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        if (playerTurnController == null)
            playerTurnController = FindFirstObjectByType<PlayerTurnController>();

        // Subscribe now if the controller is already available.
        if (playerTurnController != null)
        {
            playerTurnController.onTurnEnd.AddListener(OnPlayerTurnEnded);
            _subscribedToTurnEnd = true;
        }

        if (inputActions != null)
        {
            var playerMap = inputActions.FindActionMap("Player");
            if (playerMap != null)
            {
                mousePositionAction = playerMap.FindAction("MousePosition");
                leftClickAction     = playerMap.FindAction("LeftClick");
                rightClickAction    = playerMap.FindAction("RightClick");

                if (leftClickAction  != null) leftClickAction.performed  += OnLeftClick;
                if (rightClickAction != null) rightClickAction.performed += OnRightClick;

                inputActions.Enable();
            }
        }
    }

    void Update()
    {
        HandleTileHovering();
        HandleHotkeys();

        // Re-acquire camera if it was null during Awake (scene-reload timing race)
        if (mainCamera == null)
            mainCamera = Camera.main;

        // Auto-find PlayerTurnController if TurnManager created it after Awake
        if (playerTurnController == null)
            playerTurnController = FindFirstObjectByType<PlayerTurnController>();

        // Subscribe to turn-end as soon as the controller becomes available
        if (playerTurnController != null && !_subscribedToTurnEnd)
        {
            playerTurnController.onTurnEnd.AddListener(OnPlayerTurnEnded);
            _subscribedToTurnEnd = true;
        }
    }

    /// <summary>
    /// Processes all hotkeys each frame using the HotkeyManager so bindings
    /// are rebindable.  Falls back to raw keyboard if HotkeyManager is absent.
    /// </summary>
    private void HandleHotkeys()
    {
        var hk = HotkeyManager.Instance;

        // ── Always-active ──────────────────────────────────────────────────────
        bool endTurnPressed = hk != null
            ? hk.WasPressedThisFrame(HotkeyAction.EndTurn)
            : Keyboard.current?.spaceKey.wasPressedThisFrame ?? false;

        if (endTurnPressed && playerTurnController != null && playerTurnController.IsActive)
        {
            Debug.Log("[InputManager] EndTurn hotkey.");
            playerTurnController.OnEndTurnButtonPressed();
            return;   // don't process other hotkeys the same frame
        }

        bool cancelPressed = hk != null
            ? hk.WasPressedThisFrame(HotkeyAction.Cancel)
            : Keyboard.current?.backspaceKey.wasPressedThisFrame ?? false;

        if (cancelPressed)
        {
            Debug.Log("[InputManager] Cancel hotkey.");
            CancelCurrentAction();
            return;
        }

        // ── Friendly-monster-selected hotkeys (Normal state only) ──────────────
        if (currentState == InputState.Normal &&
            selectedTile  != null &&
            selectedTile.Occupation == Tile.OccupationType.Monster)
        {
            Monster mon = selectedTile.GetMonster();
            if (mon != null && !mon.IsEnemy)
            {
                Tile savedTile = selectedTile;  // CloseRadialMenu() will null selectedTile

                if (hk != null && hk.WasPressedThisFrame(HotkeyAction.Move))
                {
                    Debug.Log("[InputManager] Move hotkey.");
                    HandleMovementAction(savedTile);
                    return;
                }
                if (hk != null && hk.WasPressedThisFrame(HotkeyAction.Attack))
                {
                    Debug.Log("[InputManager] Attack hotkey.");
                    HandleAbilitiesAction(savedTile);
                    return;
                }
                if (hk != null && hk.WasPressedThisFrame(HotkeyAction.Info))
                {
                    Debug.Log("[InputManager] Info hotkey.");
                    HandleInfoAction(savedTile);
                    return;
                }
            }
        }

        // ── Attack-slot hotkeys (attack sub-menu open) ─────────────────────────
        if (currentState == InputState.AttackSelection && hk != null)
        {
            if      (hk.WasPressedThisFrame(HotkeyAction.Attack1)) HandleAttackSelected(0);
            else if (hk.WasPressedThisFrame(HotkeyAction.Attack2)) HandleAttackSelected(1);
            else if (hk.WasPressedThisFrame(HotkeyAction.Attack3)) HandleAttackSelected(2);
        }
    }

    // -- Hovering --------------------------------------------------------------

    void HandleTileHovering()
    {
        if (mousePositionAction == null || mainCamera == null) return;

        Vector2 mousePos = mousePositionAction.ReadValue<Vector2>();
        Ray ray = mainCamera.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, tileLayerMask))
        {
            Tile hitTile = hit.collider.GetComponent<Tile>();

            if (hitTile != null && hitTile != currentHoveredTile)
            {
                if (currentHoveredTile != null)
                    currentHoveredTile.SetHovered(false);

                currentHoveredTile = hitTile;

                if (currentState == InputState.Normal ||
                    (currentState == InputState.MovementMode   && IsValidMovementDestination(hitTile)) ||
                    (currentState == InputState.TargetSelection && IsValidTarget(hitTile)))
                {
                    currentHoveredTile.SetHovered(true);
                }

                // AOE / directional footprint preview on hover
                if (currentState == InputState.TargetSelection && IsValidTarget(hitTile))
                    UpdateAOEFootprint(hitTile);
                else if (currentState == InputState.TargetSelection)
                    ClearAOEFootprint();   // moved off aim zone — remove stale footprint

                // Show AP cost hint while in movement mode
                if (currentState == InputState.MovementMode && IsValidMovementDestination(hitTile)
                    && movementOriginTile != null)
                {
                    int pathLen = gridManager.GetPathLength(
                        movementOriginTile, hitTile, movingMonster?.IsFlying ?? false);
                    int apCost  = pathLen == int.MaxValue
                        ? 0
                        : Mathf.CeilToInt((float)pathLen / movingMonsterTilesPerAP);
                    MoveCostHint.Show($"Move cost: {apCost} AP");
                    ShowPathHighlight(hitTile);
                }
                else if (currentState == InputState.MovementMode)
                {
                    MoveCostHint.Hide();
                    ClearPathHighlight();
                }
            }
        }
        else
        {
            if (currentHoveredTile != null)
            {
                currentHoveredTile.SetHovered(false);
                currentHoveredTile = null;
            }
            if (currentState == InputState.MovementMode)
                MoveCostHint.Hide();
            if (currentState == InputState.TargetSelection)
                ClearAOEFootprint();
        }
    }

    // -- Left Click ------------------------------------------------------------

    void OnLeftClick(InputAction.CallbackContext context)
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        if (Time.time - menuOpenTime < MenuClickDelay) return;
        if (IsPointerOverUIElement()) return;

        // Try to click a monster model directly — if hit, redirect to its tile
        if (currentState == InputState.Normal || currentState == InputState.TargetSelection)
        {
            Vector2 mousePos = mousePositionAction.ReadValue<Vector2>();
            Ray ray = mainCamera.ScreenPointToRay(mousePos);
            if (Physics.Raycast(ray, out RaycastHit monsterHit, Mathf.Infinity, monsterLayerMask))
            {
                Monster hitMonster = monsterHit.collider.GetComponentInChildren<Monster>()
                                  ?? monsterHit.collider.GetComponentInParent<Monster>();
                if (hitMonster != null && hitMonster.CurrentTile != null)
                {
                    // Treat as if the player clicked the monster's tile
                    if (currentState == InputState.Normal)
                        HandleNormalClick(hitMonster.CurrentTile);
                    else if (currentState == InputState.TargetSelection)
                        HandleTargetClick(hitMonster.CurrentTile);
                    return;
                }
            }
        }

        if (currentHoveredTile == null) return;

        switch (currentState)
        {
            case InputState.Normal:
                HandleNormalClick(currentHoveredTile);
                break;

            case InputState.MovementMode:
                HandleMovementClick(currentHoveredTile);
                break;

            case InputState.Moving:
                Debug.Log("[InputManager] Monster is moving — please wait.");
                break;

            case InputState.AttackSelection:
                // Attack sub-menu handles its own clicks via VisualElement PointerUpEvent callbacks.
                break;

            case InputState.TargetSelection:
                HandleTargetClick(currentHoveredTile);
                break;
        }
    }

    private bool IsPointerOverUIElement()
    {
        // UI Toolkit radial menus don't go through EventSystem — check hover state directly.
        if (activeMenu       != null && activeMenu.IsHoveringButton)       return true;
        if (activeAttackMenu != null && activeAttackMenu.IsHoveringButton) return true;

        // Legacy uGUI elements (MonsterInfoPanel, AttackInfoPanel, HUD, etc.)
        if (UnityEngine.EventSystems.EventSystem.current == null) return false;

        var pointerData = new UnityEngine.EventSystems.PointerEventData(
            UnityEngine.EventSystems.EventSystem.current)
        {
            position = mousePositionAction.ReadValue<Vector2>()
        };

        var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointerData, results);

        foreach (var result in results)
        {
            var  parentCanvas = result.gameObject.GetComponentInParent<UnityEngine.Canvas>();
            bool isUILayer    = result.gameObject.layer == LayerMask.NameToLayer("UI");
            if (parentCanvas != null || isUILayer)
                return true;
        }

        return false;
    }

    // -- Normal Click ----------------------------------------------------------

    void HandleNormalClick(Tile clickedTile)
    {
        if (clickedTile == null) return;

        // Block selection during enemy turn
        if (TurnManager.Instance != null && !TurnManager.Instance.IsPlayerTurn)
        {
            Debug.Log("[InputManager] Not the player's turn.");
            return;
        }

        // Empty tiles have no actions — clear selection and stop
        if (clickedTile.Occupation == Tile.OccupationType.Empty)
        {
            if (activeMenu != null) CloseRadialMenu();
            if (selectedTile != null)
            {
                selectedTile.SetSelected(false);
                selectedTile.ResetVisuals();
                selectedTile = null;
            }
            return;
        }

        // Clicked the already-selected tile — do nothing
        if (selectedTile == clickedTile) return;

        // Close any open menu and clear old selection
        if (activeMenu != null) CloseRadialMenu();
        if (selectedTile != null) { selectedTile.SetSelected(false); selectedTile.ResetVisuals(); }

        selectedTile = clickedTile;
        selectedTile.SetSelected(true);
        OpenRadialMenu(selectedTile);
    }

    // -- Movement Click --------------------------------------------------------

    void HandleMovementClick(Tile clickedTile)
    {
        if (IsValidMovementDestination(clickedTile))
            StartCoroutine(MoveMonsterToTile(clickedTile));
        else
            Debug.Log("[InputManager] Invalid movement destination.");
    }

    // -- Right Click -----------------------------------------------------------

    void OnRightClick(InputAction.CallbackContext context) => CancelCurrentAction();

    void CancelCurrentAction()
    {
        switch (currentState)
        {
            // Normal: close any open menu and deselect the tile
            case InputState.Normal:
                if (activeMenu != null) CloseRadialMenu();
                if (selectedTile != null) { selectedTile.SetSelected(false); selectedTile = null; }
                cameraController?.ReleaseFocus();
                break;

            case InputState.MovementMode:
                ExitMovementMode();
                break;

            case InputState.Moving:
                Debug.Log("[InputManager] Cannot cancel while monster is moving.");
                break;

            // AttackSelection: go back to the top-level radial menu for this monster
            case InputState.AttackSelection:
            {
                ClearAttackRangePreview();
                Monster savedMonster = attackingMonster;
                CloseAttackSubMenu();
                currentState     = InputState.Normal;
                attackingMonster = null;

                if (savedMonster?.CurrentTile != null)
                {
                    selectedTile = savedMonster.CurrentTile;
                    selectedTile.SetSelected(true);
                    OpenRadialMenu(selectedTile);
                }
                break;
            }

            // TargetSelection: go back to the attack sub-menu
            case InputState.TargetSelection:
            {
                Monster savedMonster = attackingMonster;
                ExitTargetSelection();   // clears attackingMonster

                if (savedMonster?.CurrentTile != null)
                    OpenAttackSubMenu(savedMonster.CurrentTile, savedMonster);
                break;
            }
        }
    }

    // -- Radial Menu -----------------------------------------------------------

    void OpenRadialMenu(Tile tile)
    {
        if (radialMenuPrefab == null)
        {
            Debug.LogWarning("[InputManager] RadialMenu prefab not assigned!");
            return;
        }

        if (activeMenu != null)
            Destroy(activeMenu.gameObject);

        Vector3 menuPosition = tile.transform.position + Vector3.up * 2.0f;
        activeMenu = Instantiate(radialMenuPrefab, menuPosition, Quaternion.identity);
        activeMenu.Initialize(tile, this);
        menuOpenTime = Time.time;

        Monster menuMonster = tile.GetMonster();
        if (menuMonster != null)
            cameraController?.FocusOnMonster(menuMonster.transform.position);

        Debug.Log($"[InputManager] Opened radial menu on tile {tile.GridPosition}");
    }

    public void CloseRadialMenu()
    {
        if (activeMenu != null) { Destroy(activeMenu.gameObject); activeMenu = null; }
        if (selectedTile != null) { selectedTile.SetSelected(false); selectedTile = null; }
        menuOpenTime = -1f;
    }

    public void HandleRadialAction(RadialActionType type, Tile tile)
    {
        Debug.Log($"[InputManager] Radial action: {type} on tile {tile?.GridPosition}");

        switch (type)
        {
            case RadialActionType.Movement:   HandleMovementAction(tile);  break;
            case RadialActionType.Attack:     HandleAbilitiesAction(tile); break;
            case RadialActionType.Info:       HandleInfoAction(tile);      break;
            case RadialActionType.UseAttack0: HandleAttackSelected(0);     break;
            case RadialActionType.UseAttack1: HandleAttackSelected(1);     break;
            case RadialActionType.UseAttack2: HandleAttackSelected(2);     break;
            default:
                Debug.LogWarning($"[InputManager] Unknown action type: {type}");
                break;
        }
    }

    // -- Movement Action -------------------------------------------------------

    void HandleMovementAction(Tile tile)
    {
        Monster monster = tile.GetMonster();
        if (monster == null) { Debug.LogError("[InputManager] No monster on tile!"); return; }

        if (monster.IsEnemy)
        {
            Debug.Log("[InputManager] Cannot move an enemy monster.");
            CloseRadialMenu();
            return;
        }

        if (playerTurnController != null && !playerTurnController.CanAfford(1))
        {
            BattleMessage.Show($"Not enough AP to move!", 2f);
            CloseRadialMenu();
            return;
        }

        CloseRadialMenu();
        EnterMovementMode(tile, monster);
    }

    void EnterMovementMode(Tile originTile, Monster monster)
    {
        cameraController?.ReleaseFocus();
        currentState       = InputState.MovementMode;
        movingMonster      = monster;
        movementOriginTile = originTile;

        movingMonsterTilesPerAP = monster.TilesPerAP;

        // Total reachable range = tiles-per-AP × remaining AP
        int currentAP = playerTurnController != null ? playerTurnController.CurrentAP : 1;
        int range     = movingMonsterTilesPerAP * currentAP;

        validMovementTiles = gridManager.GetTilesInRange(
            originTile, range, walkableOnly: true, isFlying: monster.IsFlying);
        gridManager.HighlightTiles(validMovementTiles, movementRangeColor, 0.15f);

        Debug.Log($"[InputManager] Movement mode — Speed {monster.Speed}, " +
                  $"{movingMonsterTilesPerAP} tile(s)/AP, range {range}, " +
                  $"{validMovementTiles.Count} valid tile(s).");
    }

    void ExitMovementMode()
    {
        MoveCostHint.Hide();
        ClearPathHighlight();
        gridManager.ClearAllHighlights();
        currentState            = InputState.Normal;
        movingMonster           = null;
        movementOriginTile      = null;
        validMovementTiles      = null;
        movingMonsterTilesPerAP = 1;
        cameraController?.ReleaseFocus();
    }

    // -- Path Preview Helpers --------------------------------------------------

    /// <summary>
    /// Highlights the BFS path from the movement origin to <paramref name="destination"/>
    /// in the path-preview color. Clears any previously shown path first.
    /// </summary>
    private void ShowPathHighlight(Tile destination)
    {
        ClearPathHighlight();
        if (movementOriginTile == null || movingMonster == null) return;

        var path = gridManager.FindPath(
            movementOriginTile, destination, movingMonster.IsFlying);
        if (path == null || path.Count == 0) return;

        previewedPathTiles = path;
        foreach (Tile t in previewedPathTiles)
            t.Highlight(pathPreviewColor, 0.25f);
    }

    /// <summary>Restores movement-range highlights on any previously previewed path tiles.</summary>
    private void ClearPathHighlight()
    {
        if (previewedPathTiles == null) return;
        foreach (Tile t in previewedPathTiles)
        {
            if (validMovementTiles != null && validMovementTiles.Contains(t))
                t.Highlight(movementRangeColor, 0.15f);
            else
                t.ResetVisuals();
        }
        previewedPathTiles = null;
    }

    // -- AOE Footprint Helpers -------------------------------------------------

    /// <summary>
    /// Updates the orange AOE footprint highlight.
    /// <para>
    /// Directional shapes (line/column/cone): <paramref name="center"/> is used as the
    /// aim direction tile — the footprint radiates from the attacker's tile toward it.
    /// </para>
    /// <para>
    /// Standard AOE shapes: the footprint is centred on <paramref name="center"/>.
    /// </para>
    /// Does nothing for non-AOE non-directional (single-target) shapes.
    /// </summary>
    private void UpdateAOEFootprint(Tile center)
    {
        if (selectedAttackData == null) return;

        bool directional = IsDirectionalShape(selectedAttackData.TargetShape);
        bool aoe         = selectedAttackData.RangeTargetShapeSize > 1;

        if (!directional && !aoe) return;   // single-target: no footprint needed

        ClearAOEFootprint();

        if (directional)
        {
            // Footprint from the attacker's tile, aimed toward the hovered tile.
            Tile origin = attackingMonster?.CurrentTile;
            if (origin == null) return;

            currentAOEFootprint = gridManager.GetTilesInAttackShape(
                origin,
                selectedAttackData.Range,
                selectedAttackData.TargetShape,
                center);            // center = hovered direction tile
        }
        else
        {
            // Standard AOE: footprint centred on the hovered tile.
            currentAOEFootprint = gridManager.GetTilesInAttackShape(
                center,
                selectedAttackData.RangeTargetShapeSize,
                selectedAttackData.TargetShape);
        }

        foreach (Tile t in currentAOEFootprint)
            t.Highlight(aoeFootprintColor, 0.3f);
    }

    /// <summary>Removes the AOE footprint highlight, restoring aim-zone dim color.</summary>
    private void ClearAOEFootprint()
    {
        if (currentAOEFootprint == null) return;
        foreach (Tile t in currentAOEFootprint)
        {
            if (validTargetTiles != null && validTargetTiles.Contains(t))
                t.Highlight(attackRangeColor, 0.05f);
            else
                t.ResetVisuals();
        }
        currentAOEFootprint = null;
    }

    bool IsValidMovementDestination(Tile tile)
        // Extra IsWalkable() guard: stale validMovementTiles could contain tiles
        // that became occupied AFTER the movement range was computed.
        => validMovementTiles != null && validMovementTiles.Contains(tile) && tile.IsWalkable();

    // -- Move Coroutine --------------------------------------------------------

    System.Collections.IEnumerator MoveMonsterToTile(Tile destinationTile)
    {
        if (movingMonster == null || movementOriginTile == null)
        {
            Debug.LogError("[InputManager] Invalid movement state!");
            yield break;
        }

        // Find the actual walkable path (respects walls, flying flag)
        List<Tile> path = gridManager.FindPath(
            movementOriginTile, destinationTile, movingMonster.IsFlying);

        if (path == null || path.Count == 0)
        {
            Debug.LogWarning("[InputManager] No walkable path to destination.");
            ExitMovementMode();
            yield break;
        }

        if (playerTurnController != null)
        {
            int apCost = Mathf.CeilToInt((float)path.Count / movingMonsterTilesPerAP);
            if (!playerTurnController.TrySpendAPForDistanceMove(movingMonster, apCost))
            {
                ExitMovementMode();
                yield break;
            }
        }

        MoveCostHint.Hide();
        ClearPathHighlight();

        currentState = InputState.Moving;
        gridManager.ClearAllHighlights();
        destinationTile.Highlight(selectedMoveColor, 0.2f);

        GameObject monsterObj = movingMonster.gameObject;

        cameraController?.FocusOnMonster(monsterObj.transform.position);

        // Start walk animation
        movingMonster.TriggerMovementAnimationStart();

        // Walk along each waypoint in the path
        Tile previousTile = movementOriginTile;
        foreach (Tile waypoint in path)
        {
            previousTile.ClearOccupation();
            waypoint.SetOccupation(Tile.OccupationType.Monster, monsterObj);

            Vector3 startPos = monsterObj.transform.position;
            Vector3 endPos   = waypoint.transform.position;

            Vector3 moveDir = new Vector3(endPos.x - startPos.x, 0f, endPos.z - startPos.z);
            if (moveDir != Vector3.zero)
                monsterObj.transform.root.rotation = Quaternion.LookRotation(moveDir);

            float distance = Vector3.Distance(startPos, endPos);
            float t        = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * movementSpeed / Mathf.Max(distance, 0.01f);
                monsterObj.transform.position =
                    Vector3.Lerp(startPos, endPos, Mathf.SmoothStep(0f, 1f, t));
                cameraController?.UpdateFocusTarget(monsterObj.transform.position);
                yield return null;
            }

            monsterObj.transform.position = endPos;
            movingMonster.CurrentTile     = waypoint;
            previousTile                  = waypoint;
        }

        movingMonster.TriggerMovementAnimationEnd();
        cameraController?.ReleaseFocus();

        Debug.Log($"[InputManager] {movingMonster.name} moved to {destinationTile.GridPosition}. " +
                  $"AP remaining: {playerTurnController?.CurrentAP}");

        yield return new UnityEngine.WaitForSeconds(0.3f);
        destinationTile.ResetVisuals();
        ExitMovementMode();

        playerTurnController?.CheckAutoEndTurn();
    }

    // -- Abilities Action ------------------------------------------------------

    void HandleAbilitiesAction(Tile tile)
    {
        Monster monster = tile.GetMonster();
        if (monster == null) { Debug.LogError("[InputManager] No monster on tile!"); return; }

        if (monster.IsEnemy)
        {
            Debug.Log("[InputManager] Cannot command an enemy monster.");
            CloseRadialMenu();
            return;
        }

        var attacks = monster.GetAttacks();
        if (attacks == null || attacks.Count == 0)
        {
            BattleMessage.Show($"{monster.name} has no attacks!", 2f);
            CloseRadialMenu();
            return;
        }

        if (playerTurnController != null)
        {
            int cheapest = int.MaxValue;
            foreach (var a in attacks)
                if (a?.data != null) cheapest = Mathf.Min(cheapest, a.data.ConsumeActionPoints);

            if (!playerTurnController.CanAfford(cheapest))
            {
                BattleMessage.Show($"Not enough AP to attack! (need {cheapest})", 2f);
                CloseRadialMenu();
                return;
            }
        }

        CloseRadialMenu();
        OpenAttackSubMenu(tile, monster);
    }

    void OpenAttackSubMenu(Tile tile, Monster monster)
    {
        attackingMonster = monster;

        Vector3 menuPos = tile.transform.position + Vector3.up * 2.0f;
        activeAttackMenu = Instantiate(radialMenuPrefab, menuPos, Quaternion.identity);
        activeAttackMenu.InitializeAsAttackMenu(monster, this);

        // Keep (or re-establish) focus on the attacking monster
        cameraController?.FocusOnMonster(monster.transform.position);

        currentState = InputState.AttackSelection;
        menuOpenTime = Time.time;
        Debug.Log($"[InputManager] Attack sub-menu opened for {monster.name}.");
    }

    void CloseAttackSubMenu()
    {
        if (activeAttackMenu != null)
        {
            Destroy(activeAttackMenu.gameObject);
            activeAttackMenu = null;
        }
    }

    // ── Attack Range Preview (hover in attack sub-menu) ───────────────────────

    /// <summary>
    /// Highlights the attack's aim-zone tiles while the player hovers its button
    /// in the attack sub-menu. Does NOT enter TargetSelection state.
    /// </summary>
    public void PreviewAttackRange(AttackData data)
    {
        if (attackingMonster == null || data == null) return;
        if (currentState != InputState.AttackSelection) return;

        Tile originTile = attackingMonster.CurrentTile;
        if (originTile == null) return;

        ClearAttackRangePreview();

        bool isDirectional = IsDirectionalShape(data.TargetShape);
        bool isAOE         = data.RangeTargetShapeSize > 1;

        List<Tile> shapeTiles;
        if (isDirectional)
            shapeTiles = gridManager.GetStraightTilesInRange(originTile, data.Range);
        else if (isAOE)
            shapeTiles = gridManager.GetTilesInRange(originTile, data.Range, walkableOnly: false);
        else
            shapeTiles = gridManager.GetTilesInAttackShape(originTile, data.Range, data.TargetShape);

        _previewRangeTiles = shapeTiles;
        gridManager.HighlightTiles(shapeTiles, attackRangeColor, 0.05f);
    }

    /// <summary>Restores tiles that were highlighted by a hover preview.</summary>
    public void ClearAttackRangePreview()
    {
        if (_previewRangeTiles == null) return;
        foreach (var t in _previewRangeTiles)
            t.ResetVisuals();
        _previewRangeTiles = null;
    }

    void HandleAttackSelected(int attackIndex)
    {
        ClearAttackRangePreview(); // dismiss hover preview before the real range is shown
        CloseAttackSubMenu();

        if (attackingMonster == null) { currentState = InputState.Normal; return; }

        var attacks = attackingMonster.GetAttacks();
        if (attackIndex >= attacks.Count)
        {
            Debug.LogWarning($"[InputManager] Attack index {attackIndex} out of range.");
            currentState    = InputState.Normal;
            attackingMonster = null;
            return;
        }

        selectedAttackIndex = attackIndex;
        selectedAttackData  = attacks[attackIndex].data;

        // ── AP affordability check ─────────────────────────────────────────────
        // Fail fast here (before highlighting any tiles) if the selected attack
        // costs more AP than the player currently has.
        if (playerTurnController != null &&
            !playerTurnController.CanAfford(selectedAttackData.ConsumeActionPoints))
        {
            BattleMessage.Show(
                $"Not enough AP for {selectedAttackData.DisplayName}! " +
                $"(need {selectedAttackData.ConsumeActionPoints}, " +
                $"have {playerTurnController.CurrentAP})",
                2.5f);
            currentState     = InputState.Normal;
            attackingMonster = null;
            if (selectedTile != null) { selectedTile.SetSelected(false); selectedTile = null; }
            return;
        }

        Tile originTile = attackingMonster.CurrentTile
                       ?? gridManager.GetTileAtWorldPosition(attackingMonster.transform.root.position);
        if (originTile == null)
        {
            Debug.LogError("[InputManager] Cannot find attacker's tile.");
            currentState    = InputState.Normal;
            attackingMonster = null;
            return;
        }

        // ── Self-only: no targeting UI — execute immediately on the caster ──
        if (selectedAttackData.TargetTeam == AttackEnum.AttackTargetTeam.self)
        {
            if (playerTurnController != null &&
                !playerTurnController.TrySpendAPForAttack(attackingMonster, selectedAttackData))
            {
                Debug.Log("[InputManager] Self attack cancelled — not enough AP.");
                currentState     = InputState.Normal;
                attackingMonster = null;
                if (selectedTile != null) { selectedTile.SetSelected(false); selectedTile = null; }
                return;
            }

            attackingMonster.ExecuteAttack(new List<Monster> { attackingMonster }, selectedAttackIndex, selectedAttackData.IsDirect);
            Debug.Log($"[InputManager] {attackingMonster.name} used '{selectedAttackData.DisplayName}' on itself.");

            AttackInfoPanel.Hide();
            currentState       = InputState.Normal;
            attackingMonster   = null;
            selectedAttackData = null;
            if (selectedTile != null) { selectedTile.SetSelected(false); selectedTile = null; }
            StartCoroutine(DelayedAutoEndTurn());
            return;
        }

        bool isAOE        = selectedAttackData.RangeTargetShapeSize > 1;
        bool targetAlly   = selectedAttackData.TargetTeam == AttackEnum.AttackTargetTeam.ally;
        bool isDirectional = IsDirectionalShape(selectedAttackData.TargetShape);

        // ── 1. Aim-zone tiles ──────────────────────────────────────────────────────
        //   Directional  → all 8 straight-line rays so the player picks a direction.
        //   AOE          → plain radius circle (player picks the AOE centre tile).
        //   Single-target → the attack's own shape (player picks a specific target).
        List<Tile> allShapeTiles;
        if (isDirectional)
            allShapeTiles = gridManager.GetStraightTilesInRange(originTile, selectedAttackData.Range);
        else if (isAOE)
            allShapeTiles = gridManager.GetTilesInRange(originTile, selectedAttackData.Range, walkableOnly: false);
        else
            allShapeTiles = gridManager.GetTilesInAttackShape(
                originTile, selectedAttackData.Range, selectedAttackData.TargetShape);

        // ── 2. Dim-highlight every aim-zone tile so the range is visible ───────────
        gridManager.HighlightTiles(allShapeTiles, attackRangeColor, 0.05f);

        // Pick pulse colours based on whether we're targeting allies or enemies.
        Color pulseA = targetAlly ? allyPulseColorA : attackPulseColorA;
        Color pulseB = targetAlly ? allyPulseColorB : attackPulseColorB;

        if (isDirectional)
        {
            // Any tile along a straight line is a valid click — it sets the aim direction.
            // The footprint preview in UpdateAOEFootprint handles hover feedback.
            validTargetTiles = allShapeTiles;
        }
        else if (isAOE)
        {
            // For AOE: any tile in the aim zone is a valid click target.
            // (footprint preview is shown on hover in HandleTileHovering)
            validTargetTiles = allShapeTiles;

            // Pulse monsters of the correct team so the player can see who might be hit.
            foreach (var t in validTargetTiles)
            {
                Monster m = t.GetMonster();
                bool isValidTeam = targetAlly
                    ? m != null && m.IsEnemy == attackingMonster.IsEnemy
                    : m != null && m.IsEnemy != attackingMonster.IsEnemy;
                if (isValidTeam)
                {
                    t.StartPulse(pulseA, pulseB, attackPulseSpeed);
                    t.StartJitter(attackJitterAmplitude, attackJitterFrequency);
                }
            }
        }
        else
        {
            // ── Single target: only tiles with a valid monster of the correct team ──
            validTargetTiles = allShapeTiles.FindAll(t =>
            {
                if (t.Occupation != Tile.OccupationType.Monster) return false;
                Monster m = t.GetMonster();
                if (m == null) return false;
                return targetAlly
                    ? m.IsEnemy == attackingMonster.IsEnemy
                    : m.IsEnemy != attackingMonster.IsEnemy;
            });

            foreach (var t in validTargetTiles)
            {
                t.StartPulse(pulseA, pulseB, attackPulseSpeed);
                t.StartJitter(attackJitterAmplitude, attackJitterFrequency);
            }

            if (validTargetTiles.Count == 0)
            {
                Debug.Log($"[InputManager] No targets for '{selectedAttackData.DisplayName}'.");
                BattleMessage.Show($"No targets in range for {selectedAttackData.DisplayName}!", 2.5f);
            }
        }

        cameraController?.ReleaseFocus();
        currentState = InputState.TargetSelection;
        Debug.Log($"[InputManager] Targeting '{selectedAttackData.DisplayName}' " +
                  $"({(isDirectional ? "directional" : isAOE ? "AOE" : "single")}) — " +
                  $"{validTargetTiles.Count} aim tile(s) highlighted.");
    }

    void HandleTargetClick(Tile clickedTile)
    {
        if (!IsValidTarget(clickedTile))
        {
            Debug.Log("[InputManager] Clicked tile is not a valid target.");
            return;
        }
        ExecutePlayerAttack(clickedTile);
    }

    bool IsValidTarget(Tile tile) => validTargetTiles != null && validTargetTiles.Contains(tile);

    /// <summary>
    /// True when the selected attack shape requires the player to pick a direction
    /// (line, column, cone) rather than a specific target tile or AOE centre.
    /// </summary>
    private bool IsDirectionalShape(AttackEnum.AttackTargetShape shape) =>
        shape == AttackEnum.AttackTargetShape.line   ||
        shape == AttackEnum.AttackTargetShape.column ||
        shape == AttackEnum.AttackTargetShape.cone;

    void ExecutePlayerAttack(Tile targetTile)
    {
        if (attackingMonster == null || selectedAttackData == null)
        {
            Debug.LogError("[InputManager] Invalid attack state!");
            ExitTargetSelection();
            return;
        }

        bool isDirectional = IsDirectionalShape(selectedAttackData.TargetShape);
        bool isAOE         = selectedAttackData.RangeTargetShapeSize > 1;
        bool targetAlly    = selectedAttackData.TargetTeam == AttackEnum.AttackTargetTeam.ally;

        // ── Compute the footprint of affected tiles ────────────────────────────────
        List<Tile> footprint;

        if (isDirectional)
        {
            // targetTile gives the aim direction from the attacker.
            // The full ray/column/cone radiates from the attacker's origin.
            Tile origin = attackingMonster.CurrentTile
                       ?? gridManager.GetTileAtWorldPosition(attackingMonster.transform.root.position);
            footprint = gridManager.GetTilesInAttackShape(
                origin,
                selectedAttackData.Range,
                selectedAttackData.TargetShape,
                targetTile);   // directionTile = the clicked tile
        }
        else if (isAOE)
        {
            // Standard AOE: footprint centred on the clicked tile.
            footprint = gridManager.GetTilesInAttackShape(
                targetTile,
                selectedAttackData.RangeTargetShapeSize,
                selectedAttackData.TargetShape);
        }
        else
        {
            // Single-target: only the clicked tile itself.
            footprint = new List<Tile> { targetTile };
        }

        // ── Collect valid-team monsters within the footprint ───────────────────────
        var targets = new List<Monster>();
        foreach (Tile ft in footprint)
        {
            Monster m = ft.GetMonster();
            if (m == null) continue;
            bool validTeam = targetAlly
                ? m.IsEnemy == attackingMonster.IsEnemy
                : m.IsEnemy != attackingMonster.IsEnemy;
            if (validTeam) targets.Add(m);
        }

        // For non-directional attacks, bail out if the footprint is empty.
        // Directional attacks always fire (and consume AP) even into empty space.
        if (targets.Count == 0 && !isDirectional)
        {
            Debug.Log("[InputManager] No targets in footprint — cancelling.");
            ExitTargetSelection();
            return;
        }

        // ── Spend AP ──────────────────────────────────────────────────────────────
        if (playerTurnController != null &&
            !playerTurnController.TrySpendAPForAttack(attackingMonster, selectedAttackData))
        {
            Debug.Log("[InputManager] Attack cancelled — not enough AP.");
            ExitTargetSelection();
            return;
        }

        // ── Face the aimed direction ───────────────────────────────────────────────
        Vector3 aimDir = new Vector3(
            targetTile.transform.position.x - attackingMonster.transform.position.x, 0f,
            targetTile.transform.position.z - attackingMonster.transform.position.z);
        if (aimDir != Vector3.zero)
            attackingMonster.transform.root.rotation = Quaternion.LookRotation(aimDir);

        // ── Execute the attack on each valid target ────────────────────────────────
        if (targets.Count == 0)
        {
            // Directional attack fired into empty space — AP spent, no damage dealt.
            Debug.Log($"[InputManager] '{selectedAttackData.DisplayName}' fired but no targets hit.");
        }
        else
        {
            attackingMonster.ExecuteAttack(targets, selectedAttackIndex, selectedAttackData.IsDirect);
            Debug.Log($"[InputManager] '{selectedAttackData.DisplayName}' hit {targets.Count} target(s).");
        }

        AttackInfoPanel.Hide();
        cameraController?.ReleaseFocus();
        ExitTargetSelection();
        StartCoroutine(DelayedAutoEndTurn());
    }

    void ExitTargetSelection()
    {
        AttackInfoPanel.Hide();   // Bug fix: panel was staying visible on cancel
        ClearAOEFootprint();

        // Stop pulse + jitter on any target tiles
        if (validTargetTiles != null)
            foreach (var t in validTargetTiles)
            {
                t.StopPulse();
                t.StopJitter();
            }

        gridManager.ClearAllHighlights();
        currentState        = InputState.Normal;
        attackingMonster    = null;
        selectedAttackData  = null;
        validTargetTiles    = null;
        currentAOEFootprint = null;
        if (selectedTile != null) { selectedTile.SetSelected(false); selectedTile = null; }
    }

    // -- Info Action -----------------------------------------------------------

    void HandleInfoAction(Tile tile)
    {
        Monster monster = tile.GetMonster();
        if (monster == null)
        {
            Debug.LogWarning("[InputManager] No monster on this tile!");
            return;
        }

        // Close any already-open info panel so there's never more than one.
        MonsterInfoPanel.Instance?.Hide();
        MonsterInfoPanel.Instance?.Show(monster);

        // Deselect the tile so the player can click the same monster again immediately.
        // (Without this, HandleNormalClick's "already-selected" guard blocks re-selection.)
        if (selectedTile != null)
        {
            selectedTile.SetSelected(false);
            selectedTile = null;
        }
        // The RadialMenu will Destroy itself after this method returns (via RadialMenu.Close()).
        // Null the reference now so HandleNormalClick doesn't try to destroy it a second time.
        activeMenu = null;

        cameraController?.ReleaseFocus();
        Debug.Log($"[InputManager] Info for {monster.name} — " +
                  $"HP {monster.CurrentHP}/{monster.MaxHP}");
    }

    // Waits for all player attack animations to fire their hit-events before
    // ending the turn, so no damage can bleed into the enemy's turn.
    private System.Collections.IEnumerator DelayedAutoEndTurn()
    {
        if (playerTurnController != null)
            yield return StartCoroutine(playerTurnController.WaitForPendingAnimations());
        playerTurnController?.CheckAutoEndTurn();
    }

    void OnDestroy()
    {
        if (leftClickAction  != null) leftClickAction.performed  -= OnLeftClick;
        if (rightClickAction != null) rightClickAction.performed -= OnRightClick;
        if (inputActions     != null) inputActions.Disable();
        if (playerTurnController != null && _subscribedToTurnEnd)
            playerTurnController.onTurnEnd.RemoveListener(OnPlayerTurnEnded);
    }

    // -- Turn End Handler ------------------------------------------------------

    /// <summary>
    /// Called when the player ends their turn.
    /// Closes any open menu or active input mode so nothing bleeds into the
    /// enemy's turn (e.g. a stale radial menu or highlighted attack range).
    /// </summary>
    void OnPlayerTurnEnded()
    {
        switch (currentState)
        {
            case InputState.MovementMode:
                ExitMovementMode();
                break;

            case InputState.AttackSelection:
                ClearAttackRangePreview();
                CloseAttackSubMenu();
                currentState     = InputState.Normal;
                attackingMonster = null;
                break;

            case InputState.TargetSelection:
                ExitTargetSelection();
                break;
        }

        CloseRadialMenu();   // safe even when activeMenu is null
        cameraController?.ReleaseFocus();
        Debug.Log("[InputManager] Player turn ended — all menus closed.");
    }
}
