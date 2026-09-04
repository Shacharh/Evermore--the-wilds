using UnityEngine;
using System.Collections.Generic;
// This file defines the data structures for menu options in the radial menu system.

// Enum to represent the type of action associated with a menu option.
public enum RadialActionType
{
    Movement,
    Attack,
    Info,
    UseAttack0,   // Player selected the first attack in the sub-menu
    UseAttack1,    // Player selected the second attack in the sub-menu
    UseAttack2,    // Player selected the third attack in the sub-menu
    Dialogue       // Initiate taming/dialogue with an enemy
}

// This class defines the data structure for menu options in the radial menu.
[System.Serializable]
public class MenuOptionData
{
    public RadialActionType actionType;  // This will be used to identify the action when the button is clicked
    public string label; // The text label to display on the button
    public Sprite icon; // Optional icon for the button
}

// This class holds the configuration for the radial menu, including the different options for various tile states.
[System.Serializable]
public class TileMenuConfig
{
    [Header("For Player's Monsters")]
    public List<MenuOptionData> playerMonsterOptions = new List<MenuOptionData>(); // This will hold the menu options for player's monsters, set in the inspector.
    
    [Header("For Enemy Monsters")]
    public List<MenuOptionData> enemyMonsterOptions = new List<MenuOptionData>(); // This will hold the menu options for enemy monsters, set in the inspector.
    
    [Header("For Empty Tiles")]
    public List<MenuOptionData> emptyTileOptions = new List<MenuOptionData>(); // This will hold the menu options for empty tiles, set in the inspector.
}

// This ScriptableObject will be used to store the radial menu configuration, which can be easily edited in the Unity Inspector.
[CreateAssetMenu(fileName = "RadialMenuConfig", menuName = "Game/Radial Menu Config")]// This allows us to create a new RadialMenuConfig asset from the Unity menu.
public class RadialMenuConfig : ScriptableObject
{
    public TileMenuConfig menuConfig = new TileMenuConfig(); // This will hold the actual menu configuration data that can be edited in the inspector.
}