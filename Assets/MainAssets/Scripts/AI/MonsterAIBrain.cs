using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scores every possible action for a given enemy monster and returns the best one.
///
/// Scoring formula:
///   finalScore = AIGameStateScorePoints (base from game state)
///              + MonsterPersonality     (bonus per monster type, 0 if none assigned)
///
/// Action types evaluated:
///   AttackAction         — attack a target from current tile
///   MoveAndAttackAction  — reposition then attack
///   MoveAction           — reposition only (next-turn setup or survival)
///   PassAction           — do nothing (score 0, always the fallback)
/// </summary>
public class MonsterAIBrain
{
    private readonly AIGameStateScorePoints _sp; // shorthand: score points

    // Fallback created once if no asset is assigned in the Inspector.
    private static AIGameStateScorePoints _defaultSP;
    private static AIGameStateScorePoints DefaultSP
    {
        get
        {
            if (_defaultSP == null)
                _defaultSP = ScriptableObject.CreateInstance<AIGameStateScorePoints>();
            return _defaultSP;
        }
    }

    /// <param name="scorePoints">
    /// Global score constants from EnemyTurnController. Null uses built-in defaults.
    /// </param>
    public MonsterAIBrain(AIGameStateScorePoints scorePoints = null)
    {
        _sp = scorePoints != null ? scorePoints : DefaultSP;
    }

    // ── Public Entry Point ────────────────────────────────────────────────────

    /// <summary>
    /// Returns the highest-scoring action available to ctx.Self.
    /// Never returns null — PassAction (score 0) is the guaranteed fallback.
    /// </summary>
    public AIAction PickBestAction(AIContext ctx)
    {
        AIAction best = new PassAction { Score = 0f };
        foreach (var action in GenerateCandidates(ctx))
            if (action.Score > best.Score)
                best = action;
        return best;
    }

    // ── Candidate Generation ──────────────────────────────────────────────────

    private List<AIAction> GenerateCandidates(AIContext ctx)
    {
        var list = new List<AIAction> { new PassAction { Score = 0f } };

        if (ctx.SelfTile == null) return list;

        var attacks = ctx.Self.GetAttacks();
        if (attacks == null || attacks.Count == 0) return list;

        int shockCost       = ctx.Self.ShockAPCostIncrease;
        int moveCostPerTile = ctx.Self.MoveCost;

        int apForMovement = ctx.AvailableAP - shockCost;
        int maxMoveRange  = (apForMovement > 0 && moveCostPerTile > 0)
            ? apForMovement / moveCostPerTile : 0;

        List<Tile> reachableTiles = maxMoveRange > 0
            ? ctx.Grid.GetTilesInRange(ctx.SelfTile, maxMoveRange, walkableOnly: true)
            : new List<Tile>();

        bool          isInDanger = IsInDanger(ctx);
        List<Monster> threats    = isInDanger ? GetThreats(ctx) : new List<Monster>();

        // ── Attack and move+attack ────────────────────────────────────────────
        for (int i = 0; i < attacks.Count; i++)
        {
            MonsterAttack ma = attacks[i];
            if (ma?.data == null) continue;

            AttackData attack     = ma.data;
            int        attackCost = attack.ConsumeActionPoints + shockCost;
            if (attackCost > ctx.AvailableAP) continue;

            foreach (var target in GetTargetsInRange(ctx.SelfTile, attack, ctx))
            {
                float score = ScoreAttack(ctx, attack, target, ctx.SelfTile);
                list.Add(new AttackAction { AttackIndex = i, Target = target, Score = score });
            }

            foreach (var destTile in reachableTiles)
            {
                int dist       = ctx.Grid.GetDistanceBetweenTiles(ctx.SelfTile, destTile);
                int moveAPCost = dist * moveCostPerTile + shockCost;
                if (moveAPCost + attackCost > ctx.AvailableAP) continue;

                foreach (var target in GetTargetsInRange(destTile, attack, ctx))
                {
                    float score = ScoreAttack(ctx, attack, target, destTile)
                                - _sp.repositionPenalty;
                    list.Add(new MoveAndAttackAction
                    {
                        MoveTo      = destTile,
                        AttackIndex = i,
                        Target      = target,
                        Score       = score
                    });
                }
            }
        }

        // ── Pure move ────────────────────────────────────────────────────────
        foreach (var tile in reachableTiles)
        {
            float score = ScoreMove(ctx, tile, threats, isInDanger);
            if (score > 0f)
                list.Add(new MoveAction { Destination = tile, Score = score });
        }

        return list;
    }

    // ── Scoring ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Attack score = base (from AIGameStateScorePoints) + bonus (from MonsterPersonality).
    /// Personality is null when none is assigned — bonus contribution is 0.
    /// </summary>
    private float ScoreAttack(AIContext ctx, AttackData attack,
        Monster target, Tile fromTile)
    {
        MonsterPersonality p = GetPersonality(ctx.Self); // null = no bonus
        float score = 0f;

        // ── Damage efficiency: base + bonus multiplier ───────────────────────
        float expectedDamage = EstimateDamage(ctx.Self, attack, target, ctx.Grid, fromTile);
        int   apCost         = Mathf.Max(1, attack.ConsumeActionPoints + ctx.Self.ShockAPCostIncrease);
        float effMultiplier  = _sp.damageEfficiencyMultiplier + (p != null ? p.damageEfficiencyMultiplier : 0f);
        score += (expectedDamage / apCost) * effMultiplier;

        // ── Kill shot: base + bonus ──────────────────────────────────────────
        if (expectedDamage >= target.CurrentHP)
            score += _sp.killShotBonus + (p != null ? p.killShotBonus : 0f);

        // ── Focus fire: threshold from game state, base + bonus score ────────
        float targetHPPercent = (float)target.CurrentHP / Mathf.Max(1, target.MaxHP);
        if (targetHPPercent < _sp.focusFireHPThreshold)
            score += _sp.focusFireBonus + (p != null ? p.focusFireBonus : 0f);

        // ── AoE multi-target: base + bonus per extra target ──────────────────
        if (attack.Target == AttackEnum.AttackTarget.AOE)
        {
            int extraTargets = CountPlayersInAttackRangeFrom(fromTile, attack, ctx) - 1;
            if (extraTargets > 0)
            {
                float perTarget = _sp.multiTargetBonus + (p != null ? p.multiTargetBonus : 0f);
                score += extraTargets * perTarget;
            }
        }

        // ── Status effects: base + bonus per effect ──────────────────────────
        foreach (var effect in attack.Effects)
        {
            if (effect.category == AttackEnum.AttackCategory.status
                && effect.statusEffect != null)
                score += ScoreStatusEffect(effect.statusEffect.ID, p);
        }

        // ── Obstruction penalty: base + bonus (both are subtractions) ────────
        if (!attack.IsDirect && fromTile != null && target.CurrentTile != null)
        {
            int   obstructions  = ctx.Grid.ObstructionsBetween(fromTile, target.CurrentTile);
            float penaltyPerObs = _sp.obstructionPenalty + (p != null ? p.obstructionPenalty : 0f);
            score -= obstructions * penaltyPerObs;
        }

        // ── Low HP caution: threshold from game state, base + bonus penalty ──
        float selfHPPercent = (float)ctx.Self.CurrentHP / Mathf.Max(1, ctx.Self.MaxHP);
        if (selfHPPercent < _sp.lowHPCautionThreshold)
            score -= _sp.lowHPCautionPenalty + (p != null ? p.lowHPCautionPenalty : 0f);

        return score;
    }

    /// <summary>
    /// Move score = base (from AIGameStateScorePoints) + bonus (from MonsterPersonality).
    /// </summary>
    private float ScoreMove(AIContext ctx, Tile destination,
        List<Monster> threats, bool isInDanger)
    {
        if (destination == null || ctx.SelfTile == null) return 0f;

        MonsterPersonality p = GetPersonality(ctx.Self);
        float score = 0f;

        // ── Aggression: base + bonus per tile closed ─────────────────────────
        Tile nearestPlayerTile = GetNearestPlayerTile(ctx);
        if (nearestPlayerTile != null)
        {
            int   currentDist  = ctx.Grid.GetDistanceBetweenTiles(ctx.SelfTile, nearestPlayerTile);
            int   newDist      = ctx.Grid.GetDistanceBetweenTiles(destination, nearestPlayerTile);
            float aggrPerTile  = _sp.aggressionBonus + (p != null ? p.aggressionBonus : 0f);
            score += (currentDist - newDist) * aggrPerTile;
        }

        // ── Multi-target: base + bonus per reachable player ──────────────────
        float multiPerTarget = _sp.multiTargetBonus + (p != null ? p.multiTargetBonus : 0f);
        score += CountMaxPlayersReachableFrom(destination, ctx) * multiPerTarget;

        // ── Survival instinct: base + bonus per tile escaped per threat ───────
        if (isInDanger)
        {
            float survivalPerTile = _sp.survivalInstinctBonus + (p != null ? p.survivalInstinctBonus : 0f);
            foreach (var threat in threats)
            {
                if (threat.CurrentTile == null) continue;
                int currentDistToThreat = ctx.Grid.GetDistanceBetweenTiles(
                    ctx.SelfTile, threat.CurrentTile);
                int newDistToThreat = ctx.Grid.GetDistanceBetweenTiles(
                    destination, threat.CurrentTile);
                int tilesMoved = newDistToThreat - currentDistToThreat;
                if (tilesMoved > 0)
                    score += tilesMoved * survivalPerTile;
            }
        }

        return score;
    }

    private float ScoreStatusEffect(AttackEnum.StatusEffect id, MonsterPersonality p)
    {
        switch (id)
        {
            case AttackEnum.StatusEffect.Freeze:
                return _sp.freezeBonus + (p != null ? p.freezeBonus : 0f);
            case AttackEnum.StatusEffect.Burn:
            case AttackEnum.StatusEffect.Poison:
                return _sp.dotBonus + (p != null ? p.dotBonus : 0f);
            case AttackEnum.StatusEffect.Shock:
                return _sp.shockBonus + (p != null ? p.shockBonus : 0f);
            default:
                return _sp.unknownStatusScore;
        }
    }

    // ── Damage Estimation ─────────────────────────────────────────────────────

    public float EstimateDamage(Monster attacker, AttackData attack,
        Monster target, GridManager grid, Tile fromTile = null)
    {
        if (attacker == null || attack == null || target == null) return 0f;

        float effectValue = 0f;
        foreach (var effect in attack.Effects)
        {
            if (effect.category == AttackEnum.AttackCategory.damage)
            {
                effectValue = effect.value;
                break;
            }
        }
        if (effectValue <= 0f) return 0f;

        float scaledDodge = target.Dodge * target.DodgeEffectiveness;
        float hitChance   = attack.GuaranteedHit
            ? 1f
            : Mathf.Clamp(attack.Accuracy - scaledDodge, 5f, 100f) / 100f;

        float obstructionMult = 1f;
        Tile  attackerTile    = fromTile ?? attacker.CurrentTile;
        if (!attack.IsDirect && grid != null
            && attackerTile != null && target.CurrentTile != null)
        {
            if (grid.ObstructionsBetween(attackerTile, target.CurrentTile) > 0)
                obstructionMult = _sp.obstructedDamageMultiplier;
        }

        float levelFactor = (2f * attacker.Level) / 5f + 2f;
        float atkDefRatio = (float)attacker.Attack / Mathf.Max(1, target.Defense);
        float baseDamage  = ((levelFactor * effectValue * atkDefRatio) / 50f) + 2f;

        float critChance  = attacker.CritRate / 100f;
        float critModFrac = attacker.CritMod  / 100f;
        float expectedDmg = baseDamage * (1f + critChance * critModFrac);

        if (!attack.IsDirect)
            expectedDmg *= attack.InDirectHitPercent;

        expectedDmg *= obstructionMult;
        expectedDmg *= _sp.damageRollMidpoint;

        return hitChance * expectedDmg;
    }

    // ── Targeting Helpers ─────────────────────────────────────────────────────

    private List<Monster> GetTargetsInRange(Tile origin, AttackData attack, AIContext ctx)
    {
        var result  = new List<Monster>();
        if (origin == null) return result;
        var tileSet = new HashSet<Tile>(
            ctx.Grid.GetTilesInAttackShape(origin, attack.Range, attack.TargetShape));
        foreach (var player in ctx.PlayerTargets)
            if (player.CurrentTile != null && tileSet.Contains(player.CurrentTile))
                result.Add(player);
        return result;
    }

    private int CountPlayersInAttackRangeFrom(Tile origin, AttackData attack, AIContext ctx)
    {
        if (origin == null) return 0;
        var tileSet = new HashSet<Tile>(
            ctx.Grid.GetTilesInAttackShape(origin, attack.Range, attack.TargetShape));
        int count = 0;
        foreach (var player in ctx.PlayerTargets)
            if (player.CurrentTile != null && tileSet.Contains(player.CurrentTile))
                count++;
        return count;
    }

    private int CountMaxPlayersReachableFrom(Tile origin, AIContext ctx)
    {
        var attacks = ctx.Self.GetAttacks();
        if (attacks == null || attacks.Count == 0) return 0;
        int max = 0;
        foreach (var ma in attacks)
        {
            if (ma?.data == null) continue;
            int c = CountPlayersInAttackRangeFrom(origin, ma.data, ctx);
            if (c > max) max = c;
        }
        return max;
    }

    private Tile GetNearestPlayerTile(AIContext ctx)
    {
        Tile nearest  = null;
        int  bestDist = int.MaxValue;
        foreach (var player in ctx.PlayerTargets)
        {
            if (player.CurrentTile == null) continue;
            int d = ctx.Grid.GetDistanceBetweenTiles(ctx.SelfTile, player.CurrentTile);
            if (d < bestDist) { bestDist = d; nearest = player.CurrentTile; }
        }
        return nearest;
    }

    // ── Danger Assessment ─────────────────────────────────────────────────────

    private bool IsInDanger(AIContext ctx)
    {
        float selfHPPercent = (float)ctx.Self.CurrentHP / Mathf.Max(1, ctx.Self.MaxHP);
        if (selfHPPercent >= _sp.dangerHPThreshold) return false;
        return GetThreats(ctx).Count > 0;
    }

    private List<Monster> GetThreats(AIContext ctx)
    {
        var threats = new List<Monster>();
        foreach (var player in ctx.PlayerTargets)
        {
            if (player.CurrentTile == null) continue;
            var attacks = player.GetAttacks();
            if (attacks == null) continue;
            foreach (var ma in attacks)
            {
                if (ma?.data == null) continue;
                var shapeTiles = ctx.Grid.GetTilesInAttackShape(
                    player.CurrentTile, ma.data.Range, ma.data.TargetShape);
                if (!shapeTiles.Contains(ctx.SelfTile)) continue;
                float dmg = EstimateDamage(player, ma.data, ctx.Self, ctx.Grid, player.CurrentTile);
                if (dmg >= ctx.Self.CurrentHP) { threats.Add(player); break; }
            }
        }
        return threats;
    }

    // ── Personality Accessor ──────────────────────────────────────────────────

    /// <summary>Returns the monster's assigned personality, or null if none is assigned.</summary>
    private MonsterPersonality GetPersonality(Monster monster)
    {
        return monster.Data != null ? monster.Data.personality : null;
    }
}
