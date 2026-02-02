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
    
    private InputAction mousePositionAction;
    private InputAction leftClickAction;
    private InputAction rightClickAction;
    
    private Tile currentHoveredTile;
    private Tile selectedTile;
    private RadialMenu activeMenu;
    
    void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
        
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();
        
        // Set up input actions
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
                // Unhover previous tile
                if (currentHoveredTile != null)
                {
                    currentHoveredTile.SetHovered(false);
                }
                
                // Hover new tile
                currentHoveredTile = hitTile;
                currentHoveredTile.SetHovered(true);
            }
        }
        else
        {
            // Mouse not over any tile
            if (currentHoveredTile != null)
            {
                currentHoveredTile.SetHovered(false);
                currentHoveredTile = null;
            }
        }
    }
    
    void OnLeftClick(InputAction.CallbackContext context)
    {
        if (currentHoveredTile == null) return;
        
        // Close existing menu if clicking elsewhere
        if (activeMenu != null && selectedTile != currentHoveredTile)
        {
            CloseRadialMenu();
        }
        
        // Select the tile
        if (selectedTile != null && selectedTile != currentHoveredTile)
        {
            selectedTile.SetSelected(false);
            selectedTile.ResetVisuals(); // Force it back to original color
        }
        
        selectedTile = currentHoveredTile;
        selectedTile.SetSelected(true);
        
        // Open radial menu
        OpenRadialMenu(selectedTile);
    }
    
    void OnRightClick(InputAction.CallbackContext context)
    {
        // Right-click to deselect/cancel
        if (activeMenu != null)
        {
            CloseRadialMenu();
        }
        
        if (selectedTile != null)
        {
            selectedTile.SetSelected(false);
            selectedTile = null;
        }
    }
    
    void OpenRadialMenu(Tile tile)
    {
        if (radialMenuPrefab == null)
        {
            Debug.LogWarning("RadialMenu prefab not assigned!");
            return;
        }
        
        // Close existing menu
        if (activeMenu != null)
        {
            Destroy(activeMenu.gameObject);
        }
        
        // Calculate world position above the tile
        Vector3 menuPosition = tile.transform.position + Vector3.up * 2.0f;
        
        // Instantiate menu
        activeMenu = Instantiate(radialMenuPrefab, menuPosition, Quaternion.identity);
        activeMenu.Initialize(tile, this);
    }
    
    public void CloseRadialMenu()
    {
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
                break;
            
            default:
                Debug.LogWarning($"Unknown action: {action}");
                break;
        }
    }
    
    void HandleMovementAction(Tile tile)
    {
        Debug.Log($"Movement selected for monster at {tile.GridPosition}");
        // TODO: Show movement range, highlight tiles, calculate AP cost
        CloseRadialMenu();
    }
    
    void HandleAbilitiesAction(Tile tile)
    {
        Debug.Log($"Abilities menu requested for monster at {tile.GridPosition}");
        // TODO: Open abilities submenu
        CloseRadialMenu();
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
}