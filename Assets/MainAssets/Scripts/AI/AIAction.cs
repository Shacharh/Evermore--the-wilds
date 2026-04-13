/// <summary>
/// Base class for all AI actions. Score is set by MonsterAIBrain during evaluation.
/// The highest-scoring action across all candidates is executed by EnemyTurnController.
/// </summary>
public abstract class AIAction
{
    /// <summary>Score assigned by MonsterAIBrain. Higher = preferred.</summary>
    public float Score { get; set; }
}

/// <summary>Attack a target from the monster's current tile.</summary>
public class AttackAction : AIAction
{
    /// <summary>Index into monster.GetAttacks().</summary>
    public int AttackIndex;
    public Monster Target;
}

/// <summary>Move to a destination tile for repositioning (no attack this action).</summary>
public class MoveAction : AIAction
{
    public Tile Destination;
}

/// <summary>
/// Move to a tile then immediately attack from the new position.
/// Total AP cost = (distance × moveCost + shockCost) + (attackCost + shockCost).
/// </summary>
public class MoveAndAttackAction : AIAction
{
    public Tile MoveTo;
    /// <summary>Index into monster.GetAttacks().</summary>
    public int AttackIndex;
    public Monster Target;
}

/// <summary>
/// Do nothing. Always scores 0 — wins only when no other action is beneficial.
/// </summary>
public class PassAction : AIAction { }
