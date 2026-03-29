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
    [Tooltip("Maximum AP this side can ever hold.")]
    [SerializeField] protected int maxAP = 12;

    [Tooltip("AP granted on the very first turn of the battle.")]
    [SerializeField] protected int startingAP = 4;

    [Tooltip("AP ADDED to the current pool at the start of every subsequent turn. " +
             "Unused AP carries over — e.g. end turn with 3 AP + 4 per-turn = 7 AP next turn.")]
    [SerializeField] protected int apPerTurn = 4;

    public int MaxAP     => maxAP;
    public int StartingAP => startingAP;
    public int APPerTurn  => apPerTurn;
    public int CurrentAP  { get; private set; }

    /// <summary>True until the first StartTurn() call; used to apply startingAP instead of additive recharge.</summary>
    private bool _isFirstTurn = true;

    // -- Monster Roster --------------------------------------------------------

    [Header("Monsters")]
    [Tooltip("Assign all monsters that belong to this side.")]
    [SerializeField] protected List<Monster> monsters = new List<Monster>();

    public IReadOnlyList<Monster> Monsters => monsters;

    // -- Events ----------------------------------------------------------------

    // Events are explicitly initialised so they are never null on runtime-created
    // objects (AddComponent path). Without the initialisers, AddListener() would
    // throw a NullReferenceException and the AP display would silently stay blank.
    /// <summary>Fires every time AP changes. Int param = new AP value.</summary>
    public UnityEvent<int> onAPChanged = new UnityEvent<int>();
    public UnityEvent      onTurnStart = new UnityEvent();
    public UnityEvent      onTurnEnd   = new UnityEvent();

    // -- Turn Lifecycle (called by TurnManager) --------------------------------

    public virtual void StartTurn()
    {
        if (_isFirstTurn)
        {
            // First turn: grant the configured starting AP (independent of apPerTurn)
            SetAP(Mathf.Min(startingAP, maxAP));
            _isFirstTurn = false;
            Debug.Log($"[{GetType().Name}] First turn — {CurrentAP} starting AP (startingAP={startingAP}, max={maxAP}).");
        }
        else
        {
            // Subsequent turns: ADD apPerTurn to whatever AP remains (carry-over design)
            SetAP(Mathf.Min(CurrentAP + apPerTurn, maxAP));
            Debug.Log($"[{GetType().Name}] Turn started — +{apPerTurn} AP added → {CurrentAP} AP (max {maxAP}).");
        }

        foreach (Monster m in monsters)
            m.ResetForNewTurn();

        onTurnStart?.Invoke();
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
        return true;
    }

    public bool CanAfford(int cost) => CurrentAP >= cost;

    // -- Auto-end Conditions ---------------------------------------------------

    /// <summary>
    /// Call this after any action that might deplete AP.
    /// Ends the turn only when AP reaches 0 — monsters may act multiple times
    /// per turn as long as there is AP remaining.
    /// </summary>
    public void CheckAutoEndTurn()
    {
        if (CurrentAP <= 0)
        {
            Debug.Log($"[{GetType().Name}] Auto-ending turn (AP depleted).");
            TurnManager.Instance?.ForceEndTurn();
        }
    }

    /// <summary>
    /// Removes a monster from this side's roster (called on monster death).
    /// </summary>
    public void RemoveMonster(Monster m)
    {
        if (monsters.Remove(m))
            Debug.Log($"[{GetType().Name}] {m?.name} removed from roster ({monsters.Count} remaining).");
    }

    // -- Monster Queries -------------------------------------------------------

    public List<Monster> GetUnactedMonsters()
        => monsters.Where(m => !m.HasActed).ToList();

    public bool AnyMonsterCanAct()
        => monsters.Any(m => !m.HasActed);

    // -- Force-end Helper ------------------------------------------------------

    public void ForceEndTurn()
        => TurnManager.Instance?.ForceEndTurn();

    // -- Auto-Discovery --------------------------------------------------------

    /// <summary>
    /// Called by TurnManager after all monsters have been spawned.
    /// Only auto-fills the roster when no monsters were manually assigned
    /// in the Inspector — manual rosters are always respected.
    /// </summary>
    public virtual void AutoDiscoverMonsters()
    {
        if (monsters.Count > 0) return; // inspector-assigned — keep them
        DiscoverMonsters();
    }

    /// <summary>
    /// Override in PlayerTurnController / EnemyTurnController to populate
    /// the monsters list by scanning the scene for the correct side.
    /// </summary>
    protected virtual void DiscoverMonsters() { }

    // -- Internals -------------------------------------------------------------

    private void SetAP(int value)
    {
        CurrentAP = Mathf.Clamp(value, 0, maxAP);
        onAPChanged?.Invoke(CurrentAP);
    }
}