using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class RadialMenu : MonoBehaviour
{
    [Header("Menu Settings")]
    [SerializeField] private float menuRadius = 1.5f;
    [SerializeField] private GameObject buttonPrefab;
    [SerializeField] private Transform buttonsContainer;
    
    private Tile targetTile;
    private InputManager inputManager;
    private List<RadialMenuButton> buttons = new List<RadialMenuButton>();
    private Camera mainCamera;
    
    void Awake()
    {
        mainCamera = Camera.main;
    }
    
    public void Initialize(Tile tile, InputManager manager)
    {
        targetTile = tile;
        inputManager = manager;
        
        CreateMenuButtons();
        PositionMenu();
    }
    
    void CreateMenuButtons()
    {
        // Determine available actions based on tile occupation
        List<MenuAction> actions = GetAvailableActions();
        
        if (actions.Count == 0)
        {
            Debug.LogWarning("No available actions for this tile!");
            return;
        }
        
        // Calculate angle between buttons
        float angleStep = 360f / actions.Count;
        
        for (int i = 0; i < actions.Count; i++)
        {
            MenuAction action = actions[i];
            float angle = i * angleStep * Mathf.Deg2Rad;
            
            // Calculate position in circle
            Vector3 buttonPosition = new Vector3(
                Mathf.Cos(angle) * menuRadius,
                0.1f,
                Mathf.Sin(angle) * menuRadius
            );
            
            // Create button
            GameObject buttonObj = Instantiate(buttonPrefab, transform);
            buttonObj.transform.localPosition = buttonPosition;
            
            // Set up button
            RadialMenuButton button = buttonObj.GetComponent<RadialMenuButton>();
            if (button != null)
            {
                button.Initialize(action.label, action.iconSprite, this);
                buttons.Add(button);
            }
        }
    }

    /*
    List<MenuAction> GetAvailableActions()
    {
        List<MenuAction> actions = new List<MenuAction>();
        actions.Add(new MenuAction("Deselect", null));

        if (targetTile.Occupation == Tile.OccupationType.Monster)
        {
            actions.Add(new MenuAction("Movement", null));

            // Let's see if the monster has attacks!
            Monster monster = targetTile.GetMonster();
            if (monster != null && monster.GetAttacks().Count > 0)
            {
                actions.Add(new MenuAction("Abilities", null));
            }
        }
        return actions;
    }
    */
    List<MenuAction> GetAvailableActions()
    {
        List<MenuAction> actions = new List<MenuAction>();

        // Always add Deselect so the menu isn't empty
        actions.Add(new MenuAction("Deselect", null));

        if (targetTile.Occupation == Tile.OccupationType.Monster)
        {
            actions.Add(new MenuAction("Movement", null));

            Monster monster = targetTile.GetMonster();
            // Even if attacks failed to load, let's show the button for testing
            actions.Add(new MenuAction("Abilities", null));
        }

        return actions;
    }

    void PositionMenu()
    {
        // Keep menu facing camera
        if (mainCamera != null)
        {
            transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,
                           mainCamera.transform.rotation * Vector3.up);
        }
    }
    
    void Update()
    {
        // Keep menu facing camera
        PositionMenu();
    }
    
    public void OnButtonClicked(string actionLabel)
    {
        if (inputManager != null)
        {
            inputManager.OnMenuActionSelected(actionLabel, targetTile);
        }
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
}