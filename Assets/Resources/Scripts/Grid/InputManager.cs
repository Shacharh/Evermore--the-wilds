using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private RadialMenu radialMenuPrefab;
    [SerializeField] private Camera mainCamera;

    [Header("Input Settings")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private LayerMask tileLayerMask;

    [Header("Movement Settings")]
    [SerializeField] private Color movementRangeColor = new Color(0.3f, 0.6f, 1f, 0.5f);
    [SerializeField] private Color selectedMoveColor = new Color(0.2f, 1f, 0.3f, 0.7f);
    [SerializeField] private float movementSpeed = 5f;

    private InputAction mousePositionAction;
    private InputAction leftClickAction;
    private InputAction rightClickAction;

    private Tile currentHoveredTile;
    private Tile selectedTile;
    private RadialMenu activeMenu;

    // Time-based click prevention
    private float menuOpenTime = -1f;
    private const float MenuClickDelay = 0.2f;//0.1f;

    private enum InputState
    {
        Normal,
        MovementMode,
        Moving
    }

    private InputState currentState = InputState.Normal;
    private Monster movingMonster;
    private Tile movementOriginTile;
    private System.Collections.Generic.List<Tile> validMovementTiles;

    void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        if (inputActions != null)
        {
            var playerMap = inputActions.FindActionMap("Player");
            if (playerMap != null)
            {
                mousePositionAction = playerMap.FindAction("MousePosition");
                leftClickAction = playerMap.FindAction("LeftClick");
                rightClickAction = playerMap.FindAction("RightClick");

                if (leftClickAction != null)
                    leftClickAction.performed += OnLeftClick;

                if (rightClickAction != null)
                    rightClickAction.performed += OnRightClick;

                inputActions.Enable();
            }
        }
    }

    void Update()
    {
        HandleTileHovering();
    }

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
                {
                    currentHoveredTile.SetHovered(false);
                }

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


    void OnLeftClick(InputAction.CallbackContext context)
    {
        Debug.Log($"=== CLICK FRAME {Time.frameCount} TIME {Time.time} ===");
        Debug.Log($"Menu open time: {menuOpenTime}, Delay check: {Time.time - menuOpenTime}");

        // CRITICAL FIX: Prevent clicks immediately after opening menu
        if (Time.time - menuOpenTime < MenuClickDelay)
        {
            Debug.Log($"BLOCKED - Menu just opened (delay: {Time.time - menuOpenTime:F3}s < {MenuClickDelay}s)");
            return;
        }

        // NEW INPUT SYSTEM FIX: Check if pointer is over UI
        bool isOverUI = IsPointerOverUIElement();
        Debug.Log($"Is over UI: {isOverUI}");

        if (isOverUI)
        {
            Debug.Log("BLOCKED - Click is over UI element");
            return;
        }

        Debug.Log($"Hovered tile: {(currentHoveredTile != null ? currentHoveredTile.GridPosition.ToString() : "NULL")}");

        if (currentHoveredTile == null)
        {
            Debug.Log("BLOCKED - No tile hovered");
            return;
        }

        Debug.Log($"PROCESSING CLICK - State: {currentState}");

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
    if (UnityEngine.EventSystems.EventSystem.current == null)
        return false;

    // Create pointer event data
    var pointerData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
    {
        position = mousePositionAction.ReadValue<Vector2>()
    };

    // Raycast against UI
    var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
    UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointerData, results);

    return results.Count > 0;
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

    void HandleNormalClick(Tile clickedTile)
    {
        Debug.Log("Current total frame count: " + Time.frameCount);
        if (clickedTile == null) return;

        // 1. If clicking the same tile that's already selected, do nothing
        if (selectedTile == clickedTile)
        {
            Debug.Log("Same tile clicked, ignoring");
            return;
        }

        // 2. If clicking a different tile, close old menu and clear old selection
        if (activeMenu != null)
        {
            CloseRadialMenu();
        }

        if (selectedTile != null)
        {
            selectedTile.SetSelected(false);
            selectedTile.ResetVisuals();
        }

        // 3. Set new selection and open menu
        selectedTile = clickedTile;
        selectedTile.SetSelected(true);
        OpenRadialMenu(selectedTile);
    }
    void HandleMovementClick(Tile clickedTile)
    {
        if (IsValidMovementDestination(clickedTile))
        {
            Debug.Log($"Moving monster to {clickedTile.GridPosition}");
            StartCoroutine(MoveMonsterToTile(clickedTile));
        }
        else
        {
            Debug.Log("Invalid movement destination!");
        }
    }

    void OnRightClick(InputAction.CallbackContext context)
    {
        CancelCurrentAction();
    }

    void CancelCurrentAction()
    {
        switch (currentState)
        {
            case InputState.Normal:
                if (activeMenu != null)
                {
                    CloseRadialMenu();
                }

                if (selectedTile != null)
                {
                    selectedTile.SetSelected(false);
                    selectedTile = null;
                }
                break;

            case InputState.MovementMode:
                ExitMovementMode();
                break;

            case InputState.Moving:
                Debug.Log("Cannot cancel while monster is moving");
                break;
        }
    }

    void OpenRadialMenu(Tile tile)
    {
        Debug.Log($">>> OPENING MENU - Frame {Time.frameCount}, Time {Time.time}");

        if (radialMenuPrefab == null)
        {
            Debug.LogWarning("RadialMenu prefab not assigned!");
            return;
        }

        // Destroy existing menu if any
        if (activeMenu != null)
        {
            Debug.Log("Destroying old menu before opening new one");
            Destroy(activeMenu.gameObject);
        }

        Vector3 menuPosition = tile.transform.position + Vector3.up * 2.0f;
        activeMenu = Instantiate(radialMenuPrefab, menuPosition, Quaternion.identity);
        activeMenu.Initialize(tile, this);

        // Record when menu opens
        menuOpenTime = Time.time;

        Debug.Log($"<<< MENU OPENED - Frame {Time.frameCount}, menuOpenTime set to {menuOpenTime}");
    }

    public void CloseRadialMenu()
    {
        Debug.Log($"!!! CLOSING MENU - Frame {Time.frameCount}, Time {Time.time}");
        Debug.Log($"Stack trace: {System.Environment.StackTrace}");

        if (activeMenu != null)
        {
            Destroy(activeMenu.gameObject);
            activeMenu = null;
        }

        if (selectedTile != null)
        {
            selectedTile.SetSelected(false);
            selectedTile = null;
        }

        // Reset menu time
        menuOpenTime = -1f;
    }

    public void OnMenuActionSelected(string action, Tile tile)
    {
        Debug.Log($"Action '{action}' selected for tile {tile.GridPosition}");

        switch (action)
        {
            case "Deselect":
                CloseRadialMenu();
                break;

            case "Movement":
                HandleMovementAction(tile);
                break;

            case "Abilities":
                HandleAbilitiesAction(tile);
                if (activeMenu != null) activeMenu.ShowAbilitiesSubMenu();
                break;

            default:
                Debug.LogWarning($"Unknown action: {action}");
                break;
        }
    }

    void HandleMovementAction(Tile tile)
    {
        Debug.Log($"Movement selected for monster at {tile.GridPosition}");

        Monster monster = tile.GetMonster();
        if (monster == null)
        {
            Debug.LogError("No monster found on tile!");
            return;
        }

        CloseRadialMenu();
        EnterMovementMode(tile, monster);
    }

    void EnterMovementMode(Tile originTile, Monster monster)
    {
        currentState = InputState.MovementMode;
        movingMonster = monster;
        movementOriginTile = originTile;

        int movementRange = Mathf.CeilToInt(monster.Speed / 20f);
        movementRange = Mathf.Max(1, movementRange);

        validMovementTiles = gridManager.GetTilesInRange(originTile, movementRange, walkableOnly: true);
        gridManager.HighlightTiles(validMovementTiles, movementRangeColor, 0.15f);

        Debug.Log($"Movement mode. Monster Speed: {monster.Speed}, Range: {movementRange}, {validMovementTiles.Count} tiles highlighted.");
    }

    void ExitMovementMode()
    {
        Debug.Log("Exiting movement mode");

        gridManager.ClearAllHighlights();

        currentState = InputState.Normal;
        movingMonster = null;
        movementOriginTile = null;
        validMovementTiles = null;
    }

    bool IsValidMovementDestination(Tile tile)
    {
        if (validMovementTiles == null) return false;
        return validMovementTiles.Contains(tile);
    }

    System.Collections.IEnumerator MoveMonsterToTile(Tile destinationTile)
    {
        if (movingMonster == null || movementOriginTile == null)
        {
            Debug.LogError("Invalid movement state!");
            yield break;
        }

        currentState = InputState.Moving;
        gridManager.ClearAllHighlights();
        destinationTile.Highlight(selectedMoveColor, 0.2f);

        GameObject monsterObj = movingMonster.gameObject;
        Vector3 startPos = monsterObj.transform.position;
        Vector3 endPos = destinationTile.transform.position;

        float distance = Vector3.Distance(startPos, endPos);
        float duration = distance / movementSpeed;
        float elapsed = 0f;

        Debug.Log($"Moving monster from {startPos} to {endPos}, distance: {distance}, duration: {duration}s");

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float smoothT = Mathf.SmoothStep(0, 1, t);
            monsterObj.transform.position = Vector3.Lerp(startPos, endPos, smoothT);

            yield return null;
        }

        monsterObj.transform.position = endPos;

        movementOriginTile.ClearOccupation();
        destinationTile.SetOccupation(Tile.OccupationType.Monster, monsterObj);

        Debug.Log($"Monster moved from {movementOriginTile.GridPosition} to {destinationTile.GridPosition}. 1 AP consumed (TODO: implement AP system)");

        yield return new UnityEngine.WaitForSeconds(0.3f);
        destinationTile.ResetVisuals();

        ExitMovementMode();
    }

    void HandleAbilitiesAction(Tile tile)
    {
        Debug.Log($"Abilities menu requested for monster at {tile.GridPosition}");
    }

    void OnDestroy()
    {
        if (leftClickAction != null)
            leftClickAction.performed -= OnLeftClick;

        if (rightClickAction != null)
            rightClickAction.performed -= OnRightClick;

        if (inputActions != null)
            inputActions.Disable();
    }
}//old vertion