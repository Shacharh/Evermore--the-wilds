using UnityEngine;

/// <summary>
/// Player-side TurnController.
/// Exposes TrySpendAPForMove / TrySpendAPForAttack so that InputManager
/// can validate and charge AP without needing to know the full turn system.
/// This script does NOT handle input itself -- InputManager already does that.
/// </summary>
public class PlayerTurnController : TurnController
{
    // -- State -----------------------------------------------------------------

    /// <summary>True while it is the player's turn. InputManager checks this.</summary>
    public bool IsActive { get; private set; }

    // -- Turn Lifecycle --------------------------------------------------------

    protected override void OnTurnStarted()
    {
        IsActive = true;
        Debug.Log("[PlayerTurnController] Player turn -- awaiting input.");
    }

    protected override void OnTurnEnded()
    {
        IsActive = false;
    }

    // -- AP Actions (called by InputManager) -----------------------------------

    /// <summary>
    /// Validates and charges the AP cost for a move.
    /// Cost comes from Monster.MoveCost (set per-monster in the Inspector).
    /// </summary>
    public bool TrySpendAPForMove(Monster monster)
    {
        return TrySpendAP(monster, monster.MoveCost, "move");
    }

    /// <summary>
    /// Validates and charges the AP cost for an attack.
    /// Cost comes from AttackData.ConsumeActionPoints so each attack can have
    /// its own cost configured directly on the ScriptableObject.
    /// </summary>
    public bool TrySpendAPForAttack(Monster monster, AttackData attack)
    {
        if (attack == null)
        {
            Debug.LogError("[PlayerTurnController] TrySpendAPForAttack called with null AttackData.");
            return false;
        }

        return TrySpendAP(monster, attack.ConsumeActionPoints,
                          $"'{attack.DisplayName}' (cost {attack.ConsumeActionPoints} AP)");
    }

    // -- Helpers ---------------------------------------------------------------

    private bool TrySpendAP(Monster monster, int cost, string actionName)
    {
        if (!IsActive)
        {
            Debug.Log("[PlayerTurnController] Not the player's turn.");
            return false;
        }

        if (monster == null)
        {
            Debug.LogError("[PlayerTurnController] TrySpendAP called with null monster.");
            return false;
        }

        if (monster.HasActed)
        {
            Debug.Log($"[PlayerTurnController] {monster.name} has already acted this turn.");
            return false;
        }

        if (!CanAfford(cost))
        {
            Debug.Log($"[PlayerTurnController] Not enough AP to {actionName}. " +
                      $"Has {CurrentAP}, needs {cost}.");
            return false;
        }

        if (!SpendAP(cost)) return false; // SpendAP also calls CheckAutoEndTurn

        monster.MarkActed();
        return true;
    }

    // -- UI Button Hook --------------------------------------------------------

    /// <summary>Wire this to an "End Turn" UI Button in the Inspector.</summary>
    public void OnEndTurnButtonPressed()
    {
        if (!IsActive) return;
        Debug.Log("[PlayerTurnController] Player manually ended turn.");
        ForceEndTurn();
    }
}