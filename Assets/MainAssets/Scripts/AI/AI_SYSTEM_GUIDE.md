# Enemy AI System — Designer Guide

## Overview

The enemy AI is a **scoring-based priority system**. At the start of every enemy turn, every possible action from every living enemy monster is scored. All actions are placed into one flat list, sorted highest to lowest. The game then executes them one by one until AP runs out.

A monster can act multiple times in a turn if its actions consistently outscore others. Several tuning systems exist to prevent any one monster from monopolising every turn.

---

## How the Score is Calculated

Every action gets a final score from three layers:

```
Final Score = Game State Score  (global baseline from AIGameStateScorePoints)
            + Personality Bonus (per-monster bonus from MonsterPersonality)
            + Random Jitter     (small random value, 0 to randomJitter)
```

The **Game State Score** evaluates what is happening in the battle right now (target HP, distance, frozen status, etc.).  
The **Personality Bonus** shifts how much a specific monster type cares about each factor.  
The **Random Jitter** adds a small element of unpredictability so the AI never feels perfectly mechanical.

---

## Required ScriptableObjects

### 1. AI Game State Score Points

This is the **global baseline** shared by all enemy monsters. You need exactly one.

**How to create:**
1. Right-click in the Project window
2. `Create → Evermore → AI → AI Game State Score Points`
3. Name it (e.g. `DefaultAIScorePoints`)

**How to assign:**
1. Select the `EnemyTurnController` in the scene
2. Drag the asset into `AI Configuration → Game State Score Points`

> If left empty, the system uses built-in default values for all fields.

---

### 2. Monster Personality *(optional, per monster type)*

Each unique monster type can have its own personality that biases the scoring. A monster with no personality assigned just uses the global baseline.

**How to create:**
1. Right-click in the Project window
2. `Create → Evermore → AI → Monster Personality`
3. Name it after the monster type (e.g. `Personality_Berserker`)

**How to assign:**
1. Open the `MonsterData` asset for that monster type
2. Find the `AI` section
3. Drag the personality asset into the `Personality` field

---

## AI Game State Score Points — Field Reference

### Thresholds

| Field | Default | Description |
|---|---|---|
| Focus Fire HP Threshold | 0.30 | Target HP % below which the focus-fire bonus activates |
| Low HP Caution Threshold | 0.25 | Self HP % below which attack caution penalty activates |
| Danger HP Threshold | 0.30 | Self HP % below which survival instinct can activate |
| Ally Low HP Threshold | 0.50 | Ally HP % below which the healing bonus activates |

### Estimation Constants

| Field | Default | Description |
|---|---|---|
| Reposition Penalty | 3 | Subtracted from Move+Attack actions to prefer attacking from the current tile when equal |
| Unknown Status Score | 5 | Score for applying a status effect not explicitly handled |
| Obstructed Damage Multiplier | 0.50 | Ranged attacks through obstacles are estimated at this fraction of damage |
| Damage Roll Midpoint | 0.925 | Midpoint of the 0.85–1.0 random roll used in damage estimation |

### Random Variation

| Field | Default | Description |
|---|---|---|
| Random Jitter | 10 | Maximum random bonus added to each non-pass action. 0 = fully deterministic |

### First-Actor Fatigue

Prevents the same monster from acting first every single turn.

| Field | Default | Description |
|---|---|---|
| First Actor Fatigue Limit | 2 | Consecutive turns as first actor before penalty starts |
| First Actor Fatigue Penalty | 80 | Score subtracted per extra consecutive turn. Stacks each turn |

**Example:** Limit = 2, Penalty = 80. A monster that goes first on turns 1, 2, 3, 4:
- Turn 3: −80 applied to all its actions
- Turn 4: −160 applied to all its actions

### Repetition Penalty

Prevents one monster from filling the top N slots of the queue every turn.

| Field | Default | Description |
|---|---|---|
| Repetition Penalty | 30 | Subtracted from a monster's 2nd, 3rd... actions in the queue. Multiplies: 2nd loses 1×, 3rd loses 2×, etc. |

**Example:** Penalty = 30. Monster A has 4 actions in a row in the initial sort:
- 1st action: 0 penalty
- 2nd action: −30
- 3rd action: −60
- 4th action: −90

### Attack Score Values

| Field | Default | Description |
|---|---|---|
| Kill Shot Bonus | 100 | Bonus when the action is expected to kill the target |
| Damage Efficiency Multiplier | 10 | Multiplier on expected damage per AP spent |
| Focus Fire Bonus | 30 | Bonus for targeting a monster below the focus-fire HP threshold |

### Status Effect Score Values

| Field | Default | Description |
|---|---|---|
| Freeze Bonus | 40 | Bonus for applying Freeze to a target |
| DoT Bonus | 20 | Bonus for applying Burn or Poison |
| Shock Bonus | 15 | Bonus for applying Shock (AP drain) |

### Frozen Target Score Values

| Field | Default | Description |
|---|---|---|
| Attack Frozen Bonus | 35 | Bonus for attacking a currently frozen target (they cannot dodge) |
| Freeze Bonus Turn Duration | 2 | How many turns after a freeze is applied the attack-frozen bonus remains active. After this, the AI stops prioritising the frozen target and moves on |

### Positioning Score Values

| Field | Default | Description |
|---|---|---|
| Aggression Bonus | 5 | Points per tile closed toward the nearest player when evaluating a move |
| Multi Target Bonus | 10 | Points per additional player monster in AoE range |
| Enemy Close Threshold | 2 | Maximum tile distance at which the close-range bonus applies |
| Enemy Close Bonus | 20 | Flat bonus for attacking a target within the close threshold |

### Caution Score Values

| Field | Default | Description |
|---|---|---|
| Obstruction Penalty | 15 | Penalty per obstruction on the path to a ranged attack target |
| Low HP Caution Penalty | 20 | Penalty applied to all attack actions when self HP is below the caution threshold |

### Survival Score Values

| Field | Default | Description |
|---|---|---|
| Survival Instinct Bonus | 25 | Points per tile moved away from each threatening player, when self is in danger |

### Ally Healing Score Values

| Field | Default | Description |
|---|---|---|
| Ally Heal Bonus | 40 | Points per injured ally (or self) below the threshold when evaluating a healing action. Stacks with multiple injured allies |

---

## Monster Personality — Field Reference

Every field in `MonsterPersonality` is a **bonus added on top of the global baseline**. All fields default to **0** (no change from baseline).

Use positive values to make the monster care more about something.  
Use negative values to make the monster care less or actively avoid something.

| Field | Effect |
|---|---|
| Kill Shot Bonus | Extra priority for finishing moves |
| Damage Efficiency Multiplier | Extra weight on raw damage output |
| Focus Fire Bonus | Extra incentive to target already-wounded enemies |
| Freeze Bonus | Extra desire to apply Freeze |
| DoT Bonus | Extra desire to apply Burn / Poison |
| Shock Bonus | Extra desire to apply Shock |
| Aggression Bonus | Extra drive to close distance. Negative = reluctant to advance |
| Multi Target Bonus | Extra value placed on hitting multiple targets |
| Obstruction Penalty | Extra caution about blocked ranged shots |
| Low HP Caution Penalty | Extra self-preservation. Negative = ignores danger (berserker) |
| Survival Instinct Bonus | Extra drive to retreat when threatened. Negative = stands ground |
| Enemy Close Bonus | Extra value for melee/close-range attacks |
| Attack Frozen Bonus | Extra desire to pile onto a frozen target |
| Ally Heal Bonus | Extra incentive to use healing moves when allies are hurt |

### Example Personalities

**Berserker** — attacks recklessly, ignores self-preservation:
- `killShotBonus`: +100
- `damageEfficiencyMultiplier`: +10
- `lowHPCautionPenalty`: −20 *(ignores danger)*
- `survivalInstinctBonus`: −25 *(never retreats)*

**Tactician** — prefers crowd control and positioning:
- `freezeBonus`: +60
- `multiTargetBonus`: +20
- `aggressionBonus`: −3 *(waits for the right moment)*

**Coward** — retreats when threatened:
- `survivalInstinctBonus`: +50
- `lowHPCautionPenalty`: +30
- `aggressionBonus`: −4

**Healer** — prioritises keeping allies alive:
- `allyHealBonus`: +80
- `aggressionBonus`: −5
- `damageEfficiencyMultiplier`: −5

**Assassin** — targets the weakest enemy:
- `focusFireBonus`: +80
- `killShotBonus`: +80
- `enemyCloseBonus`: +30

---

## Guaranteed Slot Features (on MonsterPersonality)

These override the score-based sort with positional constraints applied after all scoring is done.

### Guaranteed Window Appearance

Ensures a monster appears at least once in every block of X actions.

| Field | Description |
|---|---|
| Guaranteed Appearance Enabled | Checkbox to activate |
| Guaranteed Every X Actions | Window size. E.g. 3 = must appear in slots 1–3, then 4–6, then 7–9 |

**Use case:** A slow support monster that should always act once per round even if its actions score lower than the attackers.

### Guaranteed Exact Slot

Forces the monster's best action to a specific position in the queue.

| Field | Description |
|---|---|
| Guaranteed Exact Slot Enabled | Checkbox to activate |
| Guaranteed Exact Slot | 1-based slot position. E.g. 2 = always acts second |

**Use case:** A buffer monster that should always prepare the field before the main attackers go. Set to slot 1 or 2.

> **Note:** If multiple monsters have exact slots set, they are placed in ascending slot order. Conflicts are resolved by last-write-wins for a given slot.

---

## EnemyTurnController Inspector Reference

| Field | Description |
|---|---|
| Game State Score Points | Assign the `AIGameStateScorePoints` asset here |
| Skip Turn For Testing | Instantly ends the enemy turn. Useful while building the player side |
| Show Turn Debug Panel | Shows the AI turn queue overlay during play |
| Debug Panel Height | Height of the debug panel in UI pixels |
| Action Delay | Pause in seconds between each action so the player can follow what is happening |
| Move Speed | World units per second for enemy movement animations |

---

## The Turn Queue Pipeline

Each enemy turn follows this exact sequence:

```
1. Score all actions for all living enemy monsters
      ↓
2. Initial sort  (highest raw score first)
      ↓
3. Consecutive repetition penalty
   (walk the sorted list — same monster in a row gets increasing penalty)
      ↓
4. First-actor fatigue penalty
   (if same monster went first last N turns, penalise all its actions)
      ↓
5. Final sort  (re-sort after penalties)
      ↓
6. Guaranteed exact slots  (forced positions, post-sort)
      ↓
7. Guaranteed window appearances  (fill any empty windows, post-sort)
      ↓
8. Execute top-to-bottom until AP runs out
```

---

## Tips for Tuning

- **Start with the global baseline** before touching personalities. Get the default behaviour feeling right first.
- **Raise `randomJitter`** if battles feel too predictable. Lower it if the AI makes too many obviously bad choices.
- **`repetitionPenalty`** is your main lever for action spread. ~30 gives natural variety. ~60+ forces strict round-robin.
- **`firstActorFatigueLimit = 1`** means no monster can ever act first twice in a row. Use `2` or `3` for a softer feel.
- **`freezeBonusTurnDuration`** should roughly match how long your Freeze status lasts. If freeze lasts 3 turns, a duration of 2 means the AI focuses the frozen target for 2 turns then moves on.
- **Guaranteed slots** are a last resort for edge cases. Prefer tuning scores over forcing positions.
