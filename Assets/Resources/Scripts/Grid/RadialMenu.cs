using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class RadialMenu : MonoBehaviour
{
    [Header("Menu Settings")]
    [SerializeField] private float menuRadius = 150f;
    [SerializeField] private GameObject buttonPrefab;

    [Header("Configuration")]
    [SerializeField] private RadialMenuConfig menuConfig;

    private Tile targetTile;
    private InputManager inputManager;
    private List<RadialMenuButton> buttons = new List<RadialMenuButton>();
    private Camera mainCamera;
    private Canvas canvas;
    private RectTransform rectTransform;

    void Awake()
    {
        SetupCanvas();
        SetupEventSystem();
    }

    private void SetupEventSystem()
    {
        // Ensure EventSystem exists in the scene
        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject eventSystemGO = new GameObject("EventSystem");
            eventSystemGO.AddComponent<EventSystem>();
            eventSystemGO.AddComponent<StandaloneInputModule>();
            Debug.Log("Created new EventSystem");
        }
        else
        {
            Debug.Log("EventSystem already exists");
        }
    }

    void SetupCanvas()
    {
        // Get or add Canvas component
        if (canvas == null)
            canvas = GetComponent<Canvas>();

        // CRITICAL: Set Canvas to WorldSpace for 3D visibility
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;

        // Scale the canvas for world space (smaller value = bigger in world)
        canvas.transform.localScale = Vector3.one * 0.05f;

        // Set sorting order to appear above everything
        canvas.sortingOrder = 1000;

        // Add GraphicRaycaster for UI interaction
        if (GetComponent<GraphicRaycaster>() == null)
        {
            GraphicRaycaster raycaster = gameObject.AddComponent<GraphicRaycaster>();
            raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
        }

        // Get RectTransform
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        // Set RectTransform size to ensure it covers the buttons
        if (rectTransform != null)
        {
            rectTransform.sizeDelta = new Vector2(500, 500);
        }

        Debug.Log("RadialMenu Canvas setup complete - WorldSpace mode enabled");
    }

    public void Initialize(Tile tile, InputManager manager)
    {
        targetTile = tile;
        inputManager = manager;
        mainCamera = Camera.main;

        ClearButtons();
        CreateMenuButtons();
        PositionMenu(); // CRITICAL: Position the menu in world space

        Debug.Log($"RadialMenu initialized for tile at {tile.GridPosition}");
    }

    void CreateMenuButtons()
    {
        List<MenuOptionData> optionsToShow = new List<MenuOptionData>();

        // NULL CHECK: If menuConfig isn't assigned, create fallback options
        if (menuConfig == null)
        {
            Debug.LogWarning("RadialMenuConfig is not assigned! Using fallback options.");
            optionsToShow = GetFallbackOptions();
        }
        else
        {
            // Determine which options to show based on tile state
            if (targetTile.Occupation == Tile.OccupationType.Empty)
            {
                optionsToShow = menuConfig.menuConfig.emptyTileOptions;
            }
            else if (targetTile.Occupation == Tile.OccupationType.Monster)
            {
                if (targetTile.Type == Tile.TileType.PlayerSide)
                {
                    optionsToShow = menuConfig.menuConfig.playerMonsterOptions;
                }
                else if (targetTile.Type == Tile.TileType.EnemySide)
                {
                    optionsToShow = menuConfig.menuConfig.enemyMonsterOptions;
                }
            }
        }

        // Final null check
        if (optionsToShow == null || optionsToShow.Count == 0)
        {
            Debug.LogWarning($"No menu options available for tile at {targetTile.GridPosition}. Using fallback.");
            optionsToShow = GetFallbackOptions();
        }

        Debug.Log($"Available actions: {optionsToShow.Count}");

        float angleStep = 360f / optionsToShow.Count;

        for (int i = 0; i < optionsToShow.Count; i++)
        {
            GameObject buttonObj = Instantiate(buttonPrefab, transform);
            RadialMenuButton btn = buttonObj.GetComponent<RadialMenuButton>();

            // Position in circle
            float angle = i * angleStep * Mathf.Deg2Rad;
            RectTransform btnRect = buttonObj.GetComponent<RectTransform>();
            btnRect.anchoredPosition = new Vector2(
                Mathf.Cos(angle) * menuRadius,
                Mathf.Sin(angle) * menuRadius
            );

            Debug.Log($"Button {i} positioned at {btnRect.anchoredPosition}");

            // Setup button with action type and callback
            btn.Setup(optionsToShow[i].label, optionsToShow[i].icon,
                      optionsToShow[i].actionType, OnActionSelected);

            buttons.Add(btn);

            Debug.Log($"Button initialized: {optionsToShow[i].label}");
        }

        Debug.Log($"Created {buttons.Count} menu buttons");
    }

    // Fallback options if menuConfig is not assigned
    private List<MenuOptionData> GetFallbackOptions()
    {
        List<MenuOptionData> fallback = new List<MenuOptionData>
        {
            new MenuOptionData { label = "Movement", actionType = RadialActionType.Movement },
            new MenuOptionData { label = "Attack", actionType = RadialActionType.Attack },
            new MenuOptionData { label = "Info", actionType = RadialActionType.Info }
        };
        return fallback;
    }

    private void OnActionSelected(RadialActionType type)
    {
        Debug.Log($"Action selected: {type}");
        inputManager.HandleRadialAction(type, targetTile);
        Close();
    }

    public void Close()
    {
        ClearButtons();
        Destroy(gameObject);
    }

    void ClearButtons()
    {
        foreach (var btn in buttons)
        {
            if (btn != null)
                Destroy(btn.gameObject);
        }
        buttons.Clear();
    }

    void PositionMenu()
    {
        // ADD THIS LINE AT THE TOP:
        Debug.LogWarning($"=== POSITIONING MENU - Canvas mode: {canvas.renderMode}, Scale: {canvas.transform.localScale} ===");

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("Main camera not found!");
                return;
            }
        }

        if (targetTile != null)
        {
            // Position above the tile
            Vector3 tilePosition = targetTile.transform.position;
            transform.position = tilePosition + Vector3.up * 2.0f;
        }

        // Billboard: Make menu face camera
        Vector3 directionToCamera = mainCamera.transform.position - transform.position;
        directionToCamera.y = 0; // Keep menu upright

        if (directionToCamera.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(-directionToCamera);
        }

        Debug.Log($"Menu positioned at {transform.position}");
    }

    void Update()
    {
        // Billboard effect: make menu continuously face camera
        if (Camera.main != null && targetTile != null)
        {
            Vector3 directionToCamera = Camera.main.transform.position - transform.position;
            directionToCamera.y = 0;

            if (directionToCamera.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(-directionToCamera);
            }
        }
    }
}