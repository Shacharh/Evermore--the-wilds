using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles all player input on the grid.
/// AP integration: before any action, asks PlayerTurnController whether
/// the AP can be afforded and whether the monster has already acted.
/// </summary>
public class InputManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private RadialMenu radialMenuPrefab;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private PlayerTurnController playerTurnController; // <- NEW

    [Header("Input Settings")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private LayerMask tileLayerMask;

    [Header("Movement Settings")]
    [SerializeField] private Color movementRangeColor = new Color(0.3f, 0.6f, 1f, 0.5f);
    [SerializeField] private Color selectedMoveColor = new Color(0.2f, 1f, 0.3f, 0.7f);
    [SerializeField] private float movementSpeed = 5f;

    // -- Input Actions ---------------------------------------------------------

    private InputAction mousePositionAction;
    private InputAction leftClickAction;
    private InputAction rightClickAction;

    // -- Hover / Selection State -----------------------------------------------

    private Tile currentHoveredTile;
    private Tile selectedTile;
    private RadialMenu activeMenu;

    // Time-based click prevention (unchanged from original)
    private float menuOpenTime = -1f;
    private const float MenuClickDelay = 0.2f;

    // -- Input State -----------------------------------------------------------

    private enum InputState { Normal, MovementMode, Moving }
    private InputState currentState = InputState.Normal;

    private Monster movingMonster;
    private Tile movementOriginTile;
    private System.Collections.Generic.List<Tile> validMovementTiles;

    // -- Lifecycle -------------------------------------------------------------

    void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        // Auto-find PlayerTurnController if not assigned in Inspector
        if (playerTurnController == null)
            playerTurnController = FindFirstObjectByType<PlayerTurnController>();

        if (inputActions != null)
        {
            var playerMap = inputActions.FindActionMap("Player");
            if (playerMap != null)
            {
                mousePositionAction = playerMap.FindAction("MousePosition");
                leftClickAction = playerMap.FindAction("LeftClick");
                rightClickAction = playerMap.FindAction("RightClick");

                if (leftClickAction != null) leftClickAction.performed += OnLeftClick;
                if (rightClickAction != null) rightClickAction.performed += OnRightClick;

                inputActions.Enable();
            }
        }
    }

    private Vector3 lastCameraPos;

    void Update()
    {
        HandleTileHovering();

        // Track camera movement
        if (Camera.main != null)
        {
            if (Camera.main.transform.position != lastCameraPos)
            {
                Debug.LogError($"!!! CAMERA MOVED in Update() from {lastCameraPos} to {Camera.main.transform.position}");
                Debug.LogError($"Stack trace: {System.Environment.StackTrace}");
            }
            lastCameraPos = Camera.main.transform.position;
        }
    }

    // -- Hovering (unchanged) --------------------------------------------------

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
                    (currentState == InputState.MovementMode && IsValidMovementDestination(hitTile)))
                {
                    currentHoveredTile.SetHovered(true);
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
        }
    }

    // -- Left Click ------------------------------------------------------------

    void OnLeftClick(InputAction.CallbackContext context)
    {
        // Guard: menu just opened
        if (Time.time - menuOpenTime < MenuClickDelay) return;

        // Guard: clicked on UI
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
                Debug.Log("Monster is moving, please wait...");
                break;
        }
    }

    private bool IsPointerOverUIElement()
    {
        if (UnityEngine.EventSystems.EventSystem.current == null) return false;

        // Create pointer event data
        var pointerData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
        {
            position = mousePositionAction.ReadValue<Vector2>()
        };

        // Raycast against UI
        var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointerData, results);

        // FILTER: Only count objects that are ACTUAL UI (have a Canvas parent OR are on UI layer)
        bool hasRealUI = false;
        foreach (var result in results)
        {
            // Check if this object has a Canvas parent (real UI) OR is on the UI layer
            UnityEngine.Canvas parentCanvas = result.gameObject.GetComponentInParent<UnityEngine.Canvas>();
            bool isUILayer = result.gameObject.layer == LayerMask.NameToLayer("UI");

            if (parentCanvas != null || isUILayer)
            {
                hasRealUI = true;
                Debug.LogWarning($"Real UI detected: '{result.gameObject.name}' on Canvas: '{parentCanvas?.name}'");
                break;
            }
        }

        return hasRealUI;
    }

    //void OnLeftClick(InputAction.CallbackContext context)
    //{
    //    // Ignore UI during the showcase to prevent menu conflicts
    //    if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

    //    if (currentHoveredTile == null) return;

    //    HandleAlphaShowcaseLogic(currentHoveredTile);
    //}

    void HandleAlphaShowcaseLogic(Tile clickedTile)
    {
        // CASE 1: We click a monster (Initial Selection OR Changing Selection)
        if (clickedTile.Occupation == Tile.OccupationType.Monster)
        {
            // Clear old selection visuals if they exist
            if (selectedTile != null) selectedTile.SetSelected(false);

            selectedTile = clickedTile;
            movingMonster = clickedTile.GetMonster();
            selectedTile.SetSelected(true);

            Debug.Log($"Alpha: Selected Monster {movingMonster.name} at {clickedTile.GridPosition}");
            return;
        }

        // CASE 2: We have a monster selected and click an EMPTY tile (Movement)
        if (selectedTile != null && movingMonster != null && clickedTile.IsWalkable())
        {
            Debug.Log($"Alpha: Moving {movingMonster.name} to {clickedTile.GridPosition}");

            // Start the movement coroutine you already have!
            movementOriginTile = selectedTile;
            StartCoroutine(MoveMonsterToTile(clickedTile));

            // Note: MoveMonsterToTile already calls ExitMovementMode which clears variables
            return;
        }

        // CASE 3: Clicked something else or empty ground without a selection
        if (selectedTile != null)
        {
            selectedTile.SetSelected(false);
            selectedTile = null;
            movingMonster = null;
        }
    }

    // -- Normal Click ----------------------------------------------------------

    void HandleNormalClick(Tile clickedTile)
    {
        if (clickedTile == null) return;

        // -- NEW: block all selection if it's not the player's turn -------------
        if (TurnManager.Instance != null && !TurnManager.Instance.IsPlayerTurn)
        {
            Debug.Log("[InputManager] Not the player's turn.");
            return;
        }

        // Clicked the already-selected tile -- do nothing
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
                Debug.Log("Cannot cancel while monster is moving.");
                break;
        }
    }

    // -- Radial Menu -----------------------------------------------------------

    void OpenRadialMenu(Tile tile)
    {
        Debug.LogWarning($"=== ATTEMPTING TO OPEN MENU ON TILE {tile.GridPosition.x},{tile.GridPosition.y} ===");
        Debug.Log($">>> OPENING MENU - Frame {Time.frameCount}, Time {Time.time}");

        // DEBUG: Track EVERYTHING
        Transform camTarget = GameObject.Find("Cam_Target")?.transform;
        Transform mainCam = Camera.main?.transform;

        Debug.LogError($"[BEFORE ANYTHING] Main Camera pos: {mainCam?.position}, Cam_Target pos: {camTarget?.position}");

        if (radialMenuPrefab == null)
        {
            Debug.LogWarning("RadialMenu prefab not assigned!");
            return;
        }

        Debug.LogError($"[BEFORE DESTROY] Main Camera pos: {mainCam?.position}, Cam_Target pos: {camTarget?.position}");

        if (activeMenu != null)
        {
            Debug.Log("Destroying old menu before opening new one");
            Destroy(activeMenu.gameObject);
        }

        Debug.LogError($"[BEFORE INSTANTIATE] Main Camera pos: {mainCam?.position}, Cam_Target pos: {camTarget?.position}");

        Vector3 menuPosition = tile.transform.position + Vector3.up * 2.0f;
        activeMenu = Instantiate(radialMenuPrefab, menuPosition, Quaternion.identity);

        Debug.LogError($"[AFTER INSTANTIATE] Main Camera pos: {mainCam?.position}, Cam_Target pos: {camTarget?.position}");

        activeMenu.Initialize(tile, this);

        Debug.LogError($"[AFTER INITIALIZE] Main Camera pos: {mainCam?.position}, Cam_Target pos: {camTarget?.position}");

        menuOpenTime = Time.time;
    }

    public void CloseRadialMenu()
    {
        if (activeMenu != null) { Destroy(activeMenu.gameObject); activeMenu = null; }
        if (selectedTile != null) { selectedTile.SetSelected(false); selectedTile = null; }
        menuOpenTime = -1f;
    }

    public void HandleRadialAction(RadialActionType type, Tile tile)
    {
        Debug.Log($"Radial action selected: {type} for tile {tile.GridPosition}");

        switch (type)
        {
            case RadialActionType.Movement:
                HandleMovementAction(tile);
                break;

            case RadialActionType.Attack:
                HandleAbilitiesAction(tile);
                break;

            case RadialActionType.Info:
                HandleInfoAction(tile);
                break;

            default:
                Debug.LogWarning($"Unknown action type: {type}");
                break;
        }
    }

    // -- Movement Action -------------------------------------------------------

    void HandleMovementAction(Tile tile)
    {
        Monster monster = tile.GetMonster();
        if (monster == null) { Debug.LogError("No monster on tile!"); return; }

        // -- NEW: AP & acted checks before entering movement mode ---------------
        if (playerTurnController != null)
        {
            if (monster.HasActed)
            {
                Debug.Log($"[InputManager] {monster.name} has already acted this turn.");
                CloseRadialMenu();
                return;
            }

            if (!playerTurnController.CanAfford(monster.MoveCost))
            {
                Debug.Log($"[InputManager] Not enough AP to move {monster.name}. " +
                          $"Needs {monster.MoveCost}, has {playerTurnController.CurrentAP}.");
                CloseRadialMenu();
                return;
            }
        }

        CloseRadialMenu();
        EnterMovementMode(tile, monster);
    }

    void EnterMovementMode(Tile originTile, Monster monster)
    {
        currentState = InputState.MovementMode;
        movingMonster = monster;
        movementOriginTile = originTile;

        int range = Mathf.Max(1, Mathf.CeilToInt(monster.Speed / 20f));
        validMovementTiles = gridManager.GetTilesInRange(originTile, range, walkableOnly: true);
        gridManager.HighlightTiles(validMovementTiles, movementRangeColor, 0.15f);

        Debug.Log($"[InputManager] Movement mode. Speed {monster.Speed}, " +
                  $"range {range}, {validMovementTiles.Count} valid tiles.");
    }

    void ExitMovementMode()
    {
        gridManager.ClearAllHighlights();
        currentState = InputState.Normal;
        movingMonster = null;
        movementOriginTile = null;
        validMovementTiles = null;
    }

    bool IsValidMovementDestination(Tile tile)
        => validMovementTiles != null && validMovementTiles.Contains(tile);

    // -- Move Coroutine --------------------------------------------------------

    System.Collections.IEnumerator MoveMonsterToTile(Tile destinationTile)
    {
        if (movingMonster == null || movementOriginTile == null)
        {
            Debug.LogError("Invalid movement state!");
            yield break;
        }

        // -- NEW: Spend AP before moving ----------------------------------------
        if (playerTurnController != null)
        {
            if (!playerTurnController.TrySpendAPForMove(movingMonster))
            {
                // AP refused -- cancel movement
                ExitMovementMode();
                yield break;
            }
        }

        currentState = InputState.Moving;
        gridManager.ClearAllHighlights();
        destinationTile.Highlight(selectedMoveColor, 0.2f);

        // Slide monster to destination
        GameObject monsterObj = movingMonster.gameObject;
        Vector3 startPos = monsterObj.transform.position;
        Vector3 endPos = destinationTile.transform.position;
        float distance = Vector3.Distance(startPos, endPos);
        float duration = distance / movementSpeed;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            monsterObj.transform.position =
                Vector3.Lerp(startPos, endPos, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }

        monsterObj.transform.position = endPos;

        // Update tile occupancy
        movementOriginTile.ClearOccupation();
        destinationTile.SetOccupation(Tile.OccupationType.Monster, monsterObj);

        Debug.Log($"[InputManager] {movingMonster.name} moved to {destinationTile.GridPosition}. " +
                  $"AP remaining: {playerTurnController?.CurrentAP}");

        yield return new UnityEngine.WaitForSeconds(0.3f);
        destinationTile.ResetVisuals();

        ExitMovementMode();

        // -- NEW: after move, check if the turn should auto-end -----------------
        playerTurnController?.CheckAutoEndTurn();
    }

    // -- Abilities Action ------------------------------------------------------

    void HandleAbilitiesAction(Tile tile)
    {
        // TODO: Implement attack target selection.
        // When an attack is chosen, call:
        //   playerTurnController.TrySpendAPForAttack(monster)
        // before executing monster.ExecuteAttack(target, attackIndex, isDirect).
        Debug.Log($"[InputManager] Abilities requested for {tile.GridPosition}.");
    }

    void HandleInfoAction(Tile tile)
    {
        Monster monster = tile.GetMonster();
        if (monster == null)
        {
            Debug.LogWarning("No monster on this tile!");
            return;
        }

        Debug.Log($"=== MONSTER INFO ===\nName: {monster.name}\nPosition: {tile.GridPosition}\nSpeed: {monster.Speed}");
        // TODO: Show UI panel with monster stats here
    }

    void OnDestroy()
    {
        if (leftClickAction != null) leftClickAction.performed -= OnLeftClick;
        if (rightClickAction != null) rightClickAction.performed -= OnRightClick;
        if (inputActions != null) inputActions.Disable();
    }
}