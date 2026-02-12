using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class RadialMenu : MonoBehaviour
{
    [Header("Menu Settings")]
    [SerializeField] private float menuRadius = 150f; // UI units, not world units
    [SerializeField] private GameObject buttonPrefab;

    private Tile targetTile;
    private InputManager inputManager;
    private List<RadialMenuButton> buttons = new List<RadialMenuButton>();
    private Camera mainCamera;
    private Canvas canvas;

    void Awake()
    {
        mainCamera = Camera.main;
        SetupCanvas();
    }

    void SetupCanvas()
    {
        // Add Canvas if it doesn't exist
        canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }

        // CRITICAL: Set to World Space
        canvas.renderMode = RenderMode.WorldSpace;

        // Add GraphicRaycaster for button clicks
        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        // Setup RectTransform for the canvas
        RectTransform rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            rectTransform = gameObject.AddComponent<RectTransform>();
        }

        // Set canvas size (this will be the menu's boundary)
        rectTransform.sizeDelta = new Vector2(500, 500);

        // Scale the entire canvas for world space (adjust this value to make menu bigger/smaller)
        transform.localScale = Vector3.one * 0.005f;

        Debug.Log("RadialMenu Canvas setup complete");
    }

    public void Initialize(Tile tile, InputManager manager)
    {
        targetTile = tile;
        inputManager = manager;

        CreateMenuButtons();
        PositionMenu();

        Debug.Log($"RadialMenu initialized for tile at {tile.GridPosition}");
    }

    void CreateMenuButtons()
    {
        List<MenuAction> actions = GetAvailableActions();

        if (actions.Count == 0)
        {
            Debug.LogWarning("No actions available for RadialMenu!");
            return;
        }

        float angleStep = 360f / actions.Count;

        for (int i = 0; i < actions.Count; i++)
        {
            MenuAction action = actions[i];
            // Start from top (90 degrees) and go clockwise
            float angle = (90f - i * angleStep) * Mathf.Deg2Rad;

            // Calculate position on a circle (in UI space, not world space)
            float xPos = Mathf.Cos(angle) * menuRadius;
            float yPos = Mathf.Sin(angle) * menuRadius;

            // Instantiate the button
            GameObject buttonObj = Instantiate(buttonPrefab, transform);

            // Reset the button's scale since we're scaling the entire canvas
            buttonObj.transform.localScale = Vector3.one;

            // Position the button using RectTransform
            RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
            if (buttonRect != null)
            {
                // Center the button at the calculated position
                buttonRect.anchoredPosition = new Vector2(xPos, yPos);
                Debug.Log($"Button {i} positioned at {xPos}, {yPos}");
            }

            // Initialize the button with the action
            RadialMenuButton button = buttonObj.GetComponent<RadialMenuButton>();
            if (button != null)
            {
                button.Initialize(action.label, action.iconSprite, this);
                buttons.Add(button);
                Debug.Log($"Button initialized: {action.label}");
            }
            else
            {
                Debug.LogError("RadialMenuButton component not found on button prefab!");
            }
        }

        Debug.Log($"Created {buttons.Count} menu buttons");
    }

    List<MenuAction> GetAvailableActions()
    {
        List<MenuAction> actions = new List<MenuAction>();
        actions.Add(new MenuAction("Deselect", null));

        if (targetTile != null && targetTile.Occupation == Tile.OccupationType.Monster)
        {
            actions.Add(new MenuAction("Movement", null));

            Monster monster = targetTile.GetMonster();
            if (monster != null && monster.GetAttacks() != null && monster.GetAttacks().Count > 0)
            {
                actions.Add(new MenuAction("Abilities", null));
            }
        }

        Debug.Log($"Available actions: {actions.Count}");
        return actions;
    }

    void PositionMenu()
    {
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
            // Position above the tile (adjust height as needed)
            Vector3 tilePosition = targetTile.transform.position;
            transform.position = tilePosition + Vector3.up * 2.0f;

            // Debug.Log($"Menu positioned at {transform.position}"); // Removed: was logging every frame
        }

        // BILLBOARD: Make the menu always face the camera
        Vector3 directionToCamera = mainCamera.transform.position - transform.position;

        // Option 1: Full billboard (menu tilts with camera)
        // transform.rotation = Quaternion.LookRotation(-directionToCamera);

        // Option 2: Y-axis billboard only (menu stays upright but rotates to face camera)
        directionToCamera.y = 0; // Keep menu upright
        if (directionToCamera.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(-directionToCamera);
        }
    }

    void Update()
    {
        // Billboard effect: make menu face camera (position set once in Initialize)
        if (Camera.main != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
        }
    }

    void LateUpdate()
    {
        // Additional billboard update in LateUpdate for smoother following
        if (mainCamera != null)
        {
            Vector3 directionToCamera = mainCamera.transform.position - transform.position;
            directionToCamera.y = 0;
            if (directionToCamera.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(-directionToCamera);
            }
        }
    }

    public void OnButtonClicked(string actionLabel)
    {
        Debug.Log($"RadialMenu: Button clicked - {actionLabel}");

        if (inputManager != null)
        {
            inputManager.OnMenuActionSelected(actionLabel, targetTile);
        }
        else
        {
            Debug.LogError("InputManager is null!");
        }
    }

    void OnDestroy()
    {
        // Clean up buttons
        foreach (var button in buttons)
        {
            if (button != null && button.gameObject != null)
            {
                Destroy(button.gameObject);
            }
        }
        buttons.Clear();
    }

    private struct MenuAction
    {
        public string label;
        public Sprite iconSprite;

        public MenuAction(string label, Sprite icon)
        {
            this.label = label;
            this.iconSprite = icon;
        }
    }

    public void ShowAbilitiesSubMenu()
    {
        // 1. Clear existing buttons (Movement, Abilities, etc.)
        foreach (var btn in buttons) Destroy(btn.gameObject);
        buttons.Clear();

        // 2. Get the actual learned attacks from the monster
        Monster monster = targetTile.GetMonster();
        if (monster == null) return;

        var attacks = monster.GetAttacks(); // This returns IReadOnlyList<MonsterAttack>

        // 3. Create new buttons for each attack
        float angleStep = 360f / attacks.Count;
        for (int i = 0; i < attacks.Count; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(Mathf.Cos(angle) * menuRadius, Mathf.Sin(angle) * menuRadius, 0);

            GameObject btnObj = Instantiate(buttonPrefab, transform);
            btnObj.transform.localPosition = pos;

            RadialMenuButton btn = btnObj.GetComponent<RadialMenuButton>();
            // Initialize with the Attack Display Name from AttackData
            btn.Initialize(attacks[i].data.DisplayName, null, this);
            buttons.Add(btn);
        }
    }
}//old vertion