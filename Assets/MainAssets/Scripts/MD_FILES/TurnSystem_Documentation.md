# Turn-Based System -- Setup & Reference Guide

## Overview

The turn system is split across 7 scripts. The diagram below shows how they relate:

```
TurnManager  (singleton -- drives the loop)
    |
    +-- PlayerTurnController  (shared AP pool for the player side)
    |       \-- [list of player Monster components]
    |
    \-- EnemyTurnController   (shared AP pool for the enemy side)
            \-- [list of enemy Monster components]

InputManager  (reads TurnManager.IsPlayerTurn before processing clicks)
              (calls PlayerTurnController.TrySpendAPForMove / TrySpendAPForAttack)

Monster       (HasActed flag, MoveCost from MonsterData, attack costs from AttackData)
MonsterData   (ScriptableObject -- defines moveCost alongside all base stats)
```

---

## Quick-start: Scene Setup Checklist

1. Create a **GameManager** empty object -> add `TurnManager`
2. Create a **PlayerSide** empty object -> add `PlayerTurnController`
3. Create an **EnemySide** empty object -> add `EnemyTurnController`
4. Assign your existing `InputManager` a reference to `PlayerTurnController`
5. Create **MonsterData** ScriptableObjects for each species
6. Place Monster GameObjects in the scene -> assign their `MonsterData`
7. Drag all player monsters into `PlayerTurnController -> Monsters` list
8. Drag all enemy monsters into `EnemyTurnController -> Monsters` list
9. Drag `PlayerTurnController` and `EnemyTurnController` into `TurnManager`

---

## Script Reference

---

### 1. `MonsterData.cs` -- ScriptableObject

**What it is:** A data asset that defines one monster species. Shared between multiple
Monster instances (like a blueprint). Includes base stats, move pool, and the AP cost to move.

**How to create one:**
- In the Project window: right-click -> **Create -> Monster -> Create Monster**
- Fill in the fields in the Inspector

**Key fields:**

| Field                       | Description                                                                   |
|-----------------------------|-------------------------------------------------------------------------------|
| `monsterId`                 | Unique ID string -- do not change after release                               |
| `displayName`               | Name shown in UI                                                              |
| `baseHP / baseAttack / ...` | Base stats scaled by level formula                                            |
| `moveCost`                  | AP cost for this species to move one tile (minimum 1, enforced by OnValidate) |
| `movePool`                  | List of AttackEntry -- which attacks this species can learn and at what level |

**One ScriptableObject per species.** All three Slime instances in your scene
share one SlimeData asset.

---

### 2. `Monster.cs` -- Component (on each Monster GameObject)

**What it is:** Lives on every Monster prefab. Manages HP, level, IVs, learned attacks,
active effects/statuses, and turn state (HasActed).

**How to set up:**
- Attach to your Monster prefab/GameObject
- Assign `MonsterData` in the Inspector
- Tick `enemyMonster` if it belongs to the enemy side
- Starting attacks are handled by `MonsterSetup` (existing script)

**Key fields and properties:**

| Member              | Description                                                               |
|---------------------|---------------------------------------------------------------------------|
| `enemyMonster`      | Checked by IsEnemy -- marks which side this monster belongs to            |
| `IsEnemy`           | true if enemy-owned (read-only, driven by enemyMonster)                   |
| `MoveCost`          | Reads data.moveCost -- no separate field needed on the component          |
| `HasActed`          | true after this monster acts; reset each turn by TurnController           |
| `MarkActed()`       | Called by the turn controllers after spending AP                          |
| `ResetForNewTurn()` | Called by TurnController.StartTurn() -- resets HasActed and ticks effects |

**You do not call `MarkActed()` or `ResetForNewTurn()` yourself** --
the turn controllers handle both automatically.

---

### 3. `TurnManager.cs` -- Component (on GameManager)

**What it is:** The singleton state machine that owns the Player <-> Enemy loop.
Calls StartTurn() and EndTurn() on the two controllers and tracks the round number.

**How to set up:**
- Create an empty GameObject, name it `GameManager`
- Add `TurnManager`
- Drag your `PlayerTurnController` object into **Player Controller**
- Drag your `EnemyTurnController` object into **Enemy Controller**

**Inspector fields:**

| Field                  | Description                                                              |
|------------------------|--------------------------------------------------------------------------|
| `Player Controller`    | Reference to the PlayerTurnController in the scene                       |
| `Enemy Controller`     | Reference to the EnemyTurnController in the scene                        |
| `Transition Delay`     | Seconds to pause between turns (0.5 by default -- good for animations)   |
| `On Player Turn Start` | UnityEvent -- wire up UI changes, SFX, etc.                              |
| `On Enemy Turn Start`  | UnityEvent -- same for enemy side                                        |
| `On New Round`         | UnityEvent(int) -- fires each full cycle, passes the new round number    |

**Key public API:**

```csharp
TurnManager.Instance.IsPlayerTurn   // bool -- safe to check from anywhere
TurnManager.Instance.IsEnemyTurn    // bool
TurnManager.Instance.TurnNumber     // int -- current round
TurnManager.Instance.ForceEndTurn() // manually end the active turn
```

**The player's turn always goes first.** The loop is:

```
Player Turn -> Enemy Turn -> Player Turn -> Enemy Turn -> ...
                             ^ TurnNumber increments after the enemy turn ends
```

---

### 4. `TurnController.cs` -- Abstract Base Class (do NOT add to scene directly)

**What it is:** Shared logic inherited by both PlayerTurnController and
EnemyTurnController. Owns the shared AP pool for one side and manages the monster roster.

**You never add this to a GameObject directly.** Add PlayerTurnController
or EnemyTurnController instead.

**What it provides:**

| Member                  | Description                                                          |
|-------------------------|----------------------------------------------------------------------|
| `maxAP`                 | Serialized -- set in Inspector on each controller                    |
| `CurrentAP`             | Read-only current AP value                                           |
| `Monsters`              | The list of monsters on this side                                    |
| `SpendAP(int amount)`   | Deducts AP; calls CheckAutoEndTurn() automatically                   |
| `CanAfford(int cost)`   | Returns true if CurrentAP >= cost                                    |
| `CheckAutoEndTurn()`    | Ends the turn if AP = 0 OR all monsters have HasActed = true         |
| `GetUnactedMonsters()`  | Returns monsters that have not acted yet this turn                   |
| `onAPChanged`           | UnityEvent(int) -- fires every time AP changes; wire to a UI bar    |
| `onTurnStart/onTurnEnd` | UnityEvents for this side's turn boundaries                          |

**Turn auto-ends when EITHER:**
- CurrentAP <= 0 (pool exhausted), OR
- Every monster in the list has HasActed = true

---

### 5. `PlayerTurnController.cs` -- Component (on PlayerSide object)

**What it is:** The player-side TurnController. Exposes two validation methods that
InputManager calls before executing any action. It does NOT handle input itself --
that stays in InputManager.

**How to set up:**
- Create an empty GameObject, name it `PlayerSide`
- Add `PlayerTurnController`
- Set **Max AP** (e.g. 6)
- Drag all player Monster GameObjects into the **Monsters** list
- Optionally wire **On AP Changed** to a UI AP bar
- Optionally wire an "End Turn" Button's OnClick to `OnEndTurnButtonPressed()`

**Key methods called by InputManager:**

```csharp
// Before moving a monster -- returns false and logs if:
// not the player's turn, monster already acted, or pool can't afford moveCost
playerTurnController.TrySpendAPForMove(monster);

// Before using an attack -- cost comes from attack.ConsumeActionPoints
playerTurnController.TrySpendAPForAttack(monster, attackData);
```

Both methods: validate -> spend AP -> call monster.MarkActed() -> call CheckAutoEndTurn().

**IsActive** -- true only during the player's turn. InputManager checks this
to block input during the enemy turn.

---

### 6. `EnemyTurnController.cs` -- Component (on EnemySide object)

**What it is:** The enemy-side TurnController. Runs an AI coroutine automatically when
its turn starts. Loops through all unacted enemy monsters, decides move or attack,
then spends shared AP accordingly.

**How to set up:**
- Create an empty GameObject, name it `EnemySide`
- Add `EnemyTurnController`
- Set **Max AP** (can differ from the player's pool)
- Drag all enemy Monster GameObjects into the **Monsters** list
- Assign **Grid Manager** (auto-found if left empty)
- Tune **Action Delay** -- seconds between each monster's action (0.9s default)
- Tune **Move Speed** -- slide speed in world units per second

**How the AI decides each turn:**
1. Find an adjacent player monster -> attack using the highest-cost affordable attack
2. If no adjacent target -> move one step toward the nearest player monster
3. If cannot afford anything -> mark as acted and skip

**To write your own AI**, subclass EnemyTurnController and override:

```csharp
protected override IEnumerator DecideAndAct(Monster monster)
{
    // your BehaviourTree / GOAP / etc. here
    yield return null;
}
```

---

### 7. `InputManager.cs` -- Component (existing, updated)

**What changed:** Four AP integration points were added. Everything else is identical
to your original.

**New Inspector slot:**
- **Player Turn Controller** -- drag in your PlayerTurnController object
  (auto-found at runtime if left empty)

**Where AP is checked:**

| Location                   | What happens                                                          |
|----------------------------|-----------------------------------------------------------------------|
| `HandleNormalClick`        | Blocks all tile selection when TurnManager.IsPlayerTurn == false      |
| `HandleMovementAction`     | Checks monster.HasActed and CanAfford(moveCost) before movement mode  |
| `MoveMonsterToTile`        | Calls TrySpendAPForMove(monster) -- cancels slide if refused          |
| End of `MoveMonsterToTile` | Calls CheckAutoEndTurn() -- auto-ends turn if conditions are met      |

**For abilities** -- when you build attack target selection, call:

```csharp
if (playerTurnController.TrySpendAPForAttack(monster, chosenAttack))
    monster.ExecuteAttack(target, attackIndex, isDirect);
```

---

## Full Scene Hierarchy Example

```
Scene
+-- GameManager          [TurnManager]
+-- PlayerSide           [PlayerTurnController]  maxAP = 6
+-- EnemySide            [EnemyTurnController]   maxAP = 4
+-- InputHandler         [InputManager]
+-- Grid                 [GridManager] [GridLineRenderer]
+-- Camera               [CameraController]
|
+-- Player Monsters/
|   +-- Slime_01         [Monster] [MonsterSetup]   enemyMonster = false
|   \-- Slime_02         [Monster] [MonsterSetup]   enemyMonster = false
|
\-- Enemy Monsters/
    +-- Goblin_01        [Monster] [MonsterSetup]   enemyMonster = true
    \-- Goblin_02        [Monster] [MonsterSetup]   enemyMonster = true
```

---

## Data Asset Example

```
Project/
\-- Data/
    +-- Monsters/
    |   +-- SlimeData.asset       <- MonsterData (moveCost = 1)
    |   \-- GoblinData.asset      <- MonsterData (moveCost = 2)
    \-- Attacks/
        +-- Tackle.asset          <- AttackData (consumeActionPoints = 1)
        \-- HeavySlam.asset       <- AttackData (consumeActionPoints = 3)
```

---

## AP Flow Diagram

```
Player clicks tile -> InputManager.HandleNormalClick
    \-- TurnManager.IsPlayerTurn? NO  -> blocked
                                  YES -> open RadialMenu

Player selects "Movement" -> InputManager.HandleMovementAction
    \-- monster.HasActed?    YES -> blocked, close menu
        CanAfford(moveCost)? NO  -> blocked, close menu
                             YES -> EnterMovementMode, highlight tiles

Player clicks destination -> MoveMonsterToTile (coroutine)
    \-- TrySpendAPForMove(monster)
            +-- SpendAP(moveCost)     ->  CurrentAP decreases
            +-- monster.MarkActed()   ->  HasActed = true
            \-- CheckAutoEndTurn()
                    +-- AP = 0?       -> ForceEndTurn()
                    \-- all acted?    -> ForceEndTurn()
                                             \-- TurnManager.Transition()
                                                     \-- EnemyTurnController.StartTurn()
```
