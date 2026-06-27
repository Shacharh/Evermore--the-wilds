using UnityEngine;

/// <summary>
/// Player-side TurnController.
/// Exposes TrySpendAPForMove / TrySpendAPForAttack so that InputManager
/// can validate and charge AP without needing to know the full turn system.
/// This script does NOT handle input itself — InputManager already does that.
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
        Debug.Log("[PlayerTurnController] Player turn — awaiting input.");
    }

    protected override void OnTurnEnded()
    {
        IsActive = false;
        ControlsHint.Dismiss();   // hide the controls overlay after the first turn ends
    }

    // -- AP Actions (called by InputManager) -----------------------------------

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

    /// <summary>
    /// Validates and charges a variable AP cost for a multi-tile move.
    /// The cost is pre-calculated by InputManager as ceil(distance / tilesPerAP).
    /// </summary>
    public bool TrySpendAPForDistanceMove(Monster monster, int apCost)
        => TrySpendAP(monster, apCost, $"move ({apCost} AP)");

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

        // ── Freeze: monster cannot act ────────────────────────────────────────
        if (monster.IsFrozen)
        {
            string name = monster.Data?.displayName ?? monster.gameObject.name;
            BattleMessage.Show($"{name} is frozen and cannot act!", 2f);
            Debug.Log($"[PlayerTurnController] {monster.gameObject.name} is frozen — action blocked.");
            return false;
        }

        // ── Shock: increase AP cost ───────────────────────────────────────────
        cost += monster.ShockAPCostIncrease;

        // NOTE: HasActed is intentionally NOT checked here.
        // Monsters may act multiple times per turn as long as there is enough AP.

        if (!CanAfford(cost))
        {
            Debug.Log($"[PlayerTurnController] Not enough AP to {actionName}. " +
                      $"Has {CurrentAP}, needs {cost}.");
            return false;
        }

        if (!SpendAP(cost)) return false;

        monster.MarkActed(); // kept for logging / enemy-AI bookkeeping; no longer blocks re-use
        return true;
    }

    // -- Auto-Discovery --------------------------------------------------------

    /// <summary>
    /// Always re-discovers player monsters from the scene, replacing any
    /// Inspector-assigned list. This ensures the roster uses the spawner's
    /// runtime instances (which have CurrentTile set).
    /// </summary>
    public override void AutoDiscoverMonsters()
    {
        monsters.Clear();
        DiscoverMonsters();
    }

    /// <summary>Scans the scene for all non-enemy monsters and adds them to this roster.</summary>
    protected override void DiscoverMonsters()
    {
        var all = FindObjectsByType<Monster>(FindObjectsSortMode.None);
        foreach (var m in all)
            if (!m.IsEnemy)
                monsters.Add(m);
        Debug.Log($"[PlayerTurnController] Auto-discovered {monsters.Count} player monster(s).");
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
