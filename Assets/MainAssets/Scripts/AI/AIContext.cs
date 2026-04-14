using System.Collections.Generic;

/// <summary>
/// Snapshot of game state for one AI decision cycle.
/// Built once per scoring pass by EnemyTurnController and passed read-only
/// to MonsterAIBrain — all grid queries happen during Build, not during evaluation.
/// </summary>
public class AIContext
{
    /// <summary>The enemy monster making a decision this cycle.</summary>
    public Monster Self { get; private set; }

    /// <summary>The tile Self currently occupies (already late-bound before Build is called).</summary>
    public Tile SelfTile { get; private set; }

    /// <summary>AP available to spend this decision cycle.</summary>
    public int AvailableAP { get; private set; }

    /// <summary>All living player-side monsters (targets for attack, sources of danger).</summary>
    public List<Monster> PlayerTargets { get; private set; }

    /// <summary>All living enemy-side monsters other than Self (potential heal targets).</summary>
    public List<Monster> AllyMonsters { get; private set; }

    /// <summary>Grid reference for all spatial queries.</summary>
    public GridManager Grid { get; private set; }

    private AIContext() { }

    /// <summary>
    /// Builds a context snapshot for <paramref name="monster"/>.
    /// Caller is responsible for late-binding monster.CurrentTile before calling.
    /// </summary>
    public static AIContext Build(Monster monster, int availableAP,
        GridManager grid, List<Monster> playerTargets, List<Monster> allyMonsters)
    {
        return new AIContext
        {
            Self          = monster,
            SelfTile      = monster.CurrentTile,
            AvailableAP   = availableAP,
            PlayerTargets = playerTargets,
            AllyMonsters  = allyMonsters,
            Grid          = grid
        };
    }
}
