using UnityEngine;
using UnityEngine.InputSystem;

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

    [Header("Input Settings")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private LayerMask        tileLayerMask;

    [Header("Movement Settings")]
    [SerializeField] private Color movementRangeColor = new Color(0.3f, 0.6f, 1f, 0.5f);
    [SerializeField] private Color selectedMoveColor  = new Color(0.2f, 1f, 0.3f, 0.7f);
    [SerializeField] private float movementSpeed = 5f;

    [Header("Attack Settings")]
    [Tooltip("Dim highlight shown on ALL tiles in the attack's range/shape.")]
    [SerializeField] private Color attackRangeColor   = new Color(0.55f, 0.05f, 0.05f, 0.35f);
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

    // Attack state
    private Monster                              attackingMonster;
    private AttackData                           selectedAttackData;
    private int                                  selectedAttackIndex;
    private System.Collections.Generic.List<Tile> validTargetTiles;
    private RadialMenu                           activeAttackMenu;

    // -- Lifecycle -------------------------------------------------------------

    void Awake()
    {
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

                // Show AP cost hint while in movement mode
                if (currentState == InputState.MovementMode && IsValidMovementDestination(hitTile)
                    && movementOriginTile != null)
                {
                    int dist   = gridManager.GetDistanceBetweenTiles(movementOriginTile, hitTile);
                    int apCost = Mathf.CeilToInt((float)dist / movingMonsterTilesPerAP);
                    MoveCostHint.Show($"Move cost: {apCost} AP");
                }
                else if (currentState == InputState.MovementMode)
                {
                    MoveCostHint.Hide();
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
        }
    }

    // -- Left Click ------------------------------------------------------------

    void OnLeftClick(InputAction.CallbackContext context)
    {
        if (Time.time - menuOpenTime < MenuClickDelay) return;
        if (IsPointerOverUIElement()) return;
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
                // Attack sub-menu handles its own clicks via RadialMenuButton.
                break;

            case InputState.TargetSelection:
                HandleTargetClick(currentHoveredTile);
                break;
        }
    }

    private bool IsPointerOverUIElement()
    {
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
            case InputState.Normal:
                if (activeMenu != null) CloseRadialMenu();
                if (selectedTile != null) { selectedTile.SetSelected(false); selectedTile = null; }
                break;

            case InputState.MovementMode:
                ExitMovementMode();
                break;

            case InputState.Moving:
                Debug.Log("[InputManager] Cannot cancel while monster is moving.");
                break;

            case InputState.AttackSelection:
                CloseAttackSubMenu();
                currentState     = InputState.Normal;
                attackingMonster = null;
                if (selectedTile != null) { selectedTile.SetSelected(false); selectedTile = null; }
                break;

            case InputState.TargetSelection:
                ExitTargetSelection();
                break;
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

        if (playerTurnController != null && !playerTurnController.CanAfford(monster.MoveCost))
        {
            BattleMessage.Show($"Not enough AP to move! (need {monster.MoveCost})", 2f);
            CloseRadialMenu();
            return;
        }

        CloseRadialMenu();
        EnterMovementMode(tile, monster);
    }

    void EnterMovementMode(Tile originTile, Monster monster)
    {
        currentState       = InputState.MovementMode;
        movingMonster      = monster;
        movementOriginTile = originTile;

        // Speed / 10 tiles per AP (min 1 so a Speed of 0 never gets stuck).
        movingMonsterTilesPerAP = Mathf.Max(1, monster.Speed / 10);

        // Total reachable range = tiles-per-AP × remaining AP
        int currentAP = playerTurnController != null ? playerTurnController.CurrentAP : 1;
        int range     = movingMonsterTilesPerAP * currentAP;

        validMovementTiles = gridManager.GetTilesInRange(originTile, range, walkableOnly: true);
        gridManager.HighlightTiles(validMovementTiles, movementRangeColor, 0.15f);

        Debug.Log($"[InputManager] Movement mode — Speed {monster.Speed}, " +
                  $"{movingMonsterTilesPerAP} tile(s)/AP, range {range}, " +
                  $"{validMovementTiles.Count} valid tile(s).");
    }

    void ExitMovementMode()
    {
        MoveCostHint.Hide();
        gridManager.ClearAllHighlights();
        currentState            = InputState.Normal;
        movingMonster           = null;
        movementOriginTile      = null;
        validMovementTiles      = null;
        movingMonsterTilesPerAP = 1;
    }

    bool IsValidMovementDestination(Tile tile)
        => validMovementTiles != null && validMovementTiles.Contains(tile);

    // -- Move Coroutine --------------------------------------------------------

    System.Collections.IEnumerator MoveMonsterToTile(Tile destinationTile)
    {
        if (movingMonster == null || movementOriginTile == null)
        {
            Debug.LogError("[InputManager] Invalid movement state!");
            yield break;
        }

        if (playerTurnController != null)
        {
            // Compute the actual AP cost based on distance and tiles-per-AP for this monster.
            int dist   = gridManager.GetDistanceBetweenTiles(movementOriginTile, destinationTile);
            int apCost = Mathf.CeilToInt((float)dist / movingMonsterTilesPerAP);

            if (!playerTurnController.TrySpendAPForDistanceMove(movingMonster, apCost))
            {
                ExitMovementMode();
                yield break;
            }
        }

        MoveCostHint.Hide();   // hide hint as soon as movement commits

        currentState = InputState.Moving;
        gridManager.ClearAllHighlights();
        destinationTile.Highlight(selectedMoveColor, 0.2f);

        GameObject monsterObj = movingMonster.gameObject;
        Vector3 startPos = monsterObj.transform.position;
        Vector3 endPos   = destinationTile.transform.position;

        // Face the movement direction before sliding
        Vector3 moveDir = new Vector3(endPos.x - startPos.x, 0f, endPos.z - startPos.z);
        if (moveDir != Vector3.zero)
            monsterObj.transform.root.rotation = Quaternion.LookRotation(moveDir);

        float distance   = Vector3.Distance(startPos, endPos);
        float duration   = distance / movementSpeed;
        float elapsed    = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            monsterObj.transform.position =
                Vector3.Lerp(startPos, endPos, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }

        monsterObj.transform.position = endPos;

        movementOriginTile.ClearOccupation();
        destinationTile.SetOccupation(Tile.OccupationType.Monster, monsterObj);
        movingMonster.CurrentTile = destinationTile;

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

    void HandleAttackSelected(int attackIndex)
    {
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

        // ── 1. Get ALL tiles in the attack's shape/range ────────────────────
        var allShapeTiles = gridManager.GetTilesInAttackShape(
            originTile, selectedAttackData.Range, selectedAttackData.TargetShape);

        // ── 2. Dim-highlight every tile in range so the shape is visible ────
        gridManager.HighlightTiles(allShapeTiles, attackRangeColor, 0.05f);

        // ── 3. Find tiles with valid opposing-monster targets ────────────────
        validTargetTiles = allShapeTiles.FindAll(t =>
            t.Occupation == Tile.OccupationType.Monster &&
            t.GetMonster() != null &&
            t.GetMonster().IsEnemy != attackingMonster.IsEnemy);

        // ── 4. Pulse + jitter enemy tiles so they stand out ─────────────────
        foreach (var t in validTargetTiles)
        {
            t.StartPulse(attackPulseColorA, attackPulseColorB, attackPulseSpeed);
            t.StartJitter(attackJitterAmplitude, attackJitterFrequency);
        }

        // ── 5. Notify player if there are no targets ─────────────────────────
        if (validTargetTiles.Count == 0)
        {
            Debug.Log($"[InputManager] No targets for '{selectedAttackData.DisplayName}'.");
            BattleMessage.Show($"No targets in range for {selectedAttackData.DisplayName}!", 2.5f);
            // Stay in TargetSelection — the shape is still highlighted.
            // Player can press right-click to cancel.
        }

        currentState = InputState.TargetSelection;
        Debug.Log($"[InputManager] Targeting '{selectedAttackData.DisplayName}' — " +
                  $"{validTargetTiles.Count} target(s), {allShapeTiles.Count} tile(s) highlighted.");
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

    void ExecutePlayerAttack(Tile targetTile)
    {
        Monster target = targetTile.GetMonster();
        if (target == null || attackingMonster == null || selectedAttackData == null)
        {
            Debug.LogError("[InputManager] Invalid attack state!");
            ExitTargetSelection();
            return;
        }

        if (playerTurnController != null &&
            !playerTurnController.TrySpendAPForAttack(attackingMonster, selectedAttackData))
        {
            Debug.Log("[InputManager] Attack cancelled — not enough AP.");
            ExitTargetSelection();
            return;
        }

        // Face the attack target before executing
        Vector3 attackDir = new Vector3(
            targetTile.transform.position.x - attackingMonster.transform.position.x, 0f,
            targetTile.transform.position.z - attackingMonster.transform.position.z);
        if (attackDir != Vector3.zero)
            attackingMonster.transform.root.rotation = Quaternion.LookRotation(attackDir);

        attackingMonster.ExecuteAttack(target, selectedAttackIndex, selectedAttackData.IsDirect);

        // Hide the attack info panel — once the attack fires there is no reason
        // for it to keep showing the move details on screen.
        AttackInfoPanel.Hide();

        Debug.Log($"[InputManager] {attackingMonster.name} used '{selectedAttackData.DisplayName}' " +
                  $"on {target.name}.");

        ExitTargetSelection();
        playerTurnController?.CheckAutoEndTurn();
    }

    void ExitTargetSelection()
    {
        // Stop pulse + jitter on enemy target tiles before clearing all highlights
        if (validTargetTiles != null)
            foreach (var t in validTargetTiles)
            {
                t.StopPulse();
                t.StopJitter();
            }

        gridManager.ClearAllHighlights();   // resets dim colour on all range tiles too
        currentState       = InputState.Normal;
        attackingMonster   = null;
        selectedAttackData = null;
        validTargetTiles   = null;
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

        Debug.Log($"[InputManager] Info for {monster.name} — " +
                  $"HP {monster.CurrentHP}/{monster.MaxHP}");
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
                CloseAttackSubMenu();
                currentState     = InputState.Normal;
                attackingMonster = null;
                break;

            case InputState.TargetSelection:
                ExitTargetSelection();
                break;
        }

        CloseRadialMenu();   // safe even when activeMenu is null
        Debug.Log("[InputManager] Player turn ended — all menus closed.");
    }
}
