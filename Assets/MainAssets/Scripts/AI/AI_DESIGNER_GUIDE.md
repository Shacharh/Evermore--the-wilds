# Enemy AI Designer Guide

This guide explains how to tune enemy AI behaviour using the two ScriptableObjects
in the AI system — no coding required.

---

## How the Scoring Works (Simple Version)

Every turn, each enemy monster scores every action it could take
(attack, move, move-then-attack). The highest score wins.

The final score for any action is:

```
Final Score = Game State Base Score  (from AIGameStateScorePoints)
            + Monster Bonus Score    (from MonsterPersonality)
```

**AIGameStateScorePoints** sets the baseline for every monster in the game.
**MonsterPersonality** adds extra points on top for a specific monster type.

---

## The Two ScriptableObjects

### 1. AIGameStateScorePoints

**One asset for the whole game.** Controls global scoring rules and thresholds.

Create it via:
> Right-click in Project → Create → Evermore → AI → **AI Game State Score Points**

Assign it via:
> Select **EnemyTurnController** in the scene → AI Configuration → **Game State Score Points**

This asset has two sections:

#### Thresholds & Estimation Constants
These are conditions and calculation settings — they apply to all monsters equally
and cannot be overridden per monster.

| Field | What it controls |
|---|---|
| `Focus Fire HP Threshold` | How low a target's HP must be before the focus-fire bonus triggers (default 30%) |
| `Low HP Caution Threshold` | How low self HP must be before the caution penalty triggers (default 25%) |
| `Danger HP Threshold` | How low self HP must be before survival instinct can activate (default 30%) |
| `Reposition Penalty` | Small score cost subtracted when moving before attacking, so monsters prefer attacking in place when equal |
| `Unknown Status Score` | Fallback score for any status effect not explicitly listed |
| `Obstructed Damage Multiplier` | How much obstructions reduce the damage estimate for ranged attacks |
| `Damage Roll Midpoint` | The assumed average of the random damage roll used in estimation (default 0.925) |

#### Base Score Values
These are the actual points given to every monster. A monster with no personality
assigned uses only these values.

| Field | When points are given |
|---|---|
| `Kill Shot Bonus` | The action would kill the target |
| `Damage Efficiency Multiplier` | Scales expected damage per AP — rewards efficient attacks |
| `Focus Fire Bonus` | Target is below the focus-fire HP threshold |
| `Freeze Bonus` | Attack applies Freeze |
| `Dot Bonus` | Attack applies Burn or Poison |
| `Shock Bonus` | Attack applies Shock |
| `Aggression Bonus` | Per tile closed toward the nearest enemy when moving |
| `Multi Target Bonus` | Per extra player caught in AoE range from a tile |
| `Obstruction Penalty` | Per obstruction tile on the path to a ranged target (subtracted) |
| `Low HP Caution Penalty` | Subtracted from attack actions when self HP is critically low |
| `Survival Instinct Bonus` | Per tile moved away from each player who can one-shot this monster |

---

### 2. MonsterPersonality

**One asset per monster type.** Adds bonus points on top of the global base scores
for a specific monster, giving it a preferred fighting style.

Create it via:
> Right-click in Project → Create → Evermore → AI → **Monster Personality**

Assign it via:
> Open any **MonsterData** asset → AI → **Personality**

MonsterPersonality has the exact same score fields as AIGameStateScorePoints
(minus the thresholds). All fields default to **0** — meaning no bonus, just the
global base behaviour.

To make a monster prefer a certain behaviour, raise its bonus for that score.

#### Example Personality Presets

**Berserker** — All-out attack, ignores self-preservation
```
Kill Shot Bonus:          +100   (obsessed with killing)
Damage Efficiency:        +10    (wants maximum raw damage)
Low HP Caution Penalty:   -20    (reduce the penalty so it keeps attacking when low)
Survival Instinct Bonus:  -25    (cancel out survival instinct completely)
```

**Tactician** — Prefers AoE and crowd control
```
Freeze Bonus:             +60    (highly values freezing dangerous targets)
Multi Target Bonus:       +20    (strongly prefers hitting multiple enemies)
Aggression Bonus:         -3     (slightly less eager to rush forward)
```

**Coward** — Avoids danger, stays at range
```
Survival Instinct Bonus:  +50    (extremely motivated to flee when threatened)
Low HP Caution Penalty:   +30    (even more cautious when hurt)
Obstruction Penalty:      +20    (avoids risky ranged shots)
Aggression Bonus:         -4     (less eager to close distance)
```

**Support** — Prioritises applying status effects
```
Freeze Bonus:             +40
Dot Bonus:                +40
Shock Bonus:              +30
Kill Shot Bonus:          -50    (does not prioritise finishing kills)
```

---

## What Happens If Nothing Is Assigned

### No MonsterPersonality on a MonsterData asset
The monster receives **zero bonus points**. It plays entirely off the global
AIGameStateScorePoints base scores — balanced behaviour with no special preference.
This is fine for generic enemies.

### No AIGameStateScorePoints on EnemyTurnController
The system falls back to **built-in code defaults**, which are identical to the
default field values shown in the AIGameStateScorePoints Inspector. The AI will
still work correctly — you just cannot tune it from the Inspector until you create
and assign the asset.

### No personality AND no AIGameStateScorePoints
Both fallbacks activate. The AI runs on built-in defaults with no personality bonus.
Monsters will still move toward players, attack when in range, use status effects,
and flee when in mortal danger — just without any fine-tuned behaviour.

---

## Quick Setup Checklist

- [ ] Create one **AIGameStateScorePoints** asset and assign it to **EnemyTurnController**
- [ ] For each enemy type that needs distinct behaviour, create a **MonsterPersonality** asset
- [ ] Open each **MonsterData** asset and drag the matching personality into the **AI → Personality** slot
- [ ] Leave personality unassigned for generic enemies — they will use global base scores

---

## Tips

- Start with the **AIGameStateScorePoints** values and get the base behaviour feeling right
  before adding personalities. Personalities are for differentiation, not fixing broken base AI.
- Personality fields are **additive** — a value of `0` means no change from the global base.
  You do not need to fill every field on every personality.
- Raising a bonus high (e.g. Kill Shot Bonus +200) makes a monster almost always
  prioritise that behaviour when the opportunity exists.
- Lowering a penalty (negative value) makes a monster ignore a risk. Use with care.
- The **Danger HP Threshold** (in AIGameStateScorePoints) is global — all monsters
  begin evaluating survival at the same HP percentage. Individual monsters cannot
  change when survival triggers, only how strongly they respond to it
  (via `Survival Instinct Bonus` in their personality).
