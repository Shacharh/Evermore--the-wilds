using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Base class for PlayerTurnController and EnemyTurnController.
/// Owns the shared AP pool for one side and tracks which monsters have acted.
/// </summary>
public abstract class TurnController : MonoBehaviour
{
    // -- AP --------------------------------------------------------------------

    [Header("Action Points")]
    [SerializeField] protected int maxAP = 6;

    public int MaxAP => maxAP;
    public int CurrentAP { get; private set; }

    // -- Monster Roster --------------------------------------------------------

    [Header("Monsters")]
    [Tooltip("Assign all monsters that belong to this side.")]
    [SerializeField] protected List<Monster> monsters = new List<Monster>();

    public IReadOnlyList<Monster> Monsters => monsters;

    // -- Events ----------------------------------------------------------------

    /// <summary>Fires every time AP changes. Int param = new AP value.</summary>
    public UnityEvent<int> onAPChanged;
    public UnityEvent onTurnStart;
    public UnityEvent onTurnEnd;

    // -- Turn Lifecycle (called by TurnManager) --------------------------------

    public virtual void StartTurn()
    {
        SetAP(maxAP);
        foreach (Monster m in monsters)
            m.ResetForNewTurn();

        onTurnStart?.Invoke();
        Debug.Log($"[{GetType().Name}] Turn started -- {maxAP} AP, {monsters.Count} monsters.");
        OnTurnStarted();
    }

    public virtual void EndTurn()
    {
        onTurnEnd?.Invoke();
        Debug.Log($"[{GetType().Name}] Turn ended. Remaining AP: {CurrentAP}.");
        OnTurnEnded();
    }

    // -- Override Hooks --------------------------------------------------------

    protected virtual void OnTurnStarted() { }
    protected virtual void OnTurnEnded() { }

    // -- AP Management ---------------------------------------------------------

    /// <summary>
    /// Try to spend <paramref name="amount"/> AP.
    /// Returns false if the pool can't afford it.
    /// Automatically ends the turn when AP reaches 0 or all monsters have acted.
    /// </summary>
    public bool SpendAP(int amount)
    {
        if (amount <= 0) return false;

        if (CurrentAP < amount)
        {
            Debug.Log($"[{GetType().Name}] Not enough AP. Has {CurrentAP}, needs {amount}.");
            return false;
        }

        SetAP(CurrentAP - amount);
        Debug.Log($"[{GetType().Name}] Spent {amount} AP -- {CurrentAP} remaining.");
        CheckAutoEndTurn();
        return true;
    }

    public bool CanAfford(int cost) => CurrentAP >= cost;

    // -- Auto-end Conditions ---------------------------------------------------

    /// <summary>
    /// Call this after any action that might deplete AP or exhaust all monsters.
    /// Ends the turn if AP = 0 OR every monster has acted.
    /// </summary>
    public void CheckAutoEndTurn()
    {
        bool apEmpty = CurrentAP <= 0;
        bool allActed = monsters.Count > 0 && monsters.All(m => m.HasActed);

        if (apEmpty || allActed)
        {
            string reason = apEmpty ? "AP depleted" : "all monsters acted";
            Debug.Log($"[{GetType().Name}] Auto-ending turn ({reason}).");
            TurnManager.Instance?.ForceEndTurn();
        }
    }

    // -- Monster Queries -------------------------------------------------------

    public List<Monster> GetUnactedMonsters()
        => monsters.Where(m => !m.HasActed).ToList();

    public bool AnyMonsterCanAct()
        => monsters.Any(m => !m.HasActed);

    // -- Force-end Helper ------------------------------------------------------

    public void ForceEndTurn()
        => TurnManager.Instance?.ForceEndTurn();

    // -- Internals -------------------------------------------------------------

    private void SetAP(int value)
    {
        CurrentAP = Mathf.Clamp(value, 0, maxAP);
        onAPChanged?.Invoke(CurrentAP);
    }
}