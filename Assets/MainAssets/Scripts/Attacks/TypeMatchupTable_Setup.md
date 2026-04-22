# Type Matchup Table — Setup & Usage Guide

## What It Does

The `TypeMatchupTable` is a ScriptableObject that defines how effective each **attack type** is against each **defender monster type**.

When a monster uses an attack, the system looks up: *"how does a [attack element] attack affect a [defender element] monster?"*
The result is a **damage multiplier** applied on top of the normal damage formula.

The table automatically grows when you add new types to the `ElementType` enum — no code changes needed.

---

## Effectiveness Values

Each cell in the grid is a dropdown with a friendly label. Here is exactly what each one does to the final attack damage:

| Inspector Label | Multiplier | Effect on damage |
|---|---|---|
| `SuperEffective` | x2.0 | Doubles the damage |
| `Effective` | x1.5 | Adds 50% extra damage |
| `Normal` | x1.0 | No change |
| `Weak` | x0.5 | Cuts damage in half |
| `SuperWeak` | x0.25 | Cuts damage to a quarter |

The multiplier is applied **after** crits, range penalty, and obstruction penalty — so type advantage stacks on top of all other modifiers.

**Both buffs and debuffs are supported in the same table.** Setting a cell to `Weak` or `SuperWeak` means that attack type deals less damage to that defender type. There is no separate resistance system — the same cell handles both.

---

## Step 1 — Create the Asset

1. In the Unity **Project** window, right-click in any folder (recommended: `Assets/MainAssets/Data/`)
2. Select **Create > Evermore > Type Matchup Table**
3. Name it `TypeMatchupTable`

---

## Step 2 — Assign It to the Scene

1. Find the **GameInitializer** GameObject in your scene
2. In its Inspector, locate the **Type System** header
3. Drag your `TypeMatchupTable` asset into the **Type Matchup Table** field

---

## Step 3 — Fill In the Table

Open the `TypeMatchupTable` asset. You will see a **color-coded grid** in the Inspector.

- **Rows** = the **defender's monster type** (the monster being hit)
- **Columns** = the **attack's element type** (the incoming move)

Read each cell as: *"When a [Row] monster is hit by a [Column] attack, it deals [Value]."*

The background of each cell is color-coded:

| Color | Effectiveness |
|---|---|
| Bright green | Super Effective |
| Light green | Effective |
| (no color) | Normal |
| Orange | Weak |
| Red | Super Weak |

### Example

> Row: **Earth**, Column: **Fire** → set to `SuperEffective`

Fire attacks deal double damage to Earth monsters.

> Row: **Water**, Column: **Fire** → set to `Weak`

Fire attacks deal half damage to Water monsters.

---

## Opening the Standalone Editor Window

At the top of the inline Inspector is an **"Open Type Editor"** button. Clicking it opens a dedicated resizable window with:

- A toolbar showing the asset name
- A **Save** button that immediately writes the asset to disk
- The full scrollable grid with the same color coding
- The legend

Both the inline Inspector and the popup window edit the **same asset** in real time — changes in one are immediately reflected in the other.

---

## Adding a New Type

1. Add the new value to `ElementType` in `AttackEnum.cs`
2. Click on your `TypeMatchupTable` asset — `OnValidate` will automatically add a new row and column, pre-filled with `Normal`
3. Set the matchups for the new type in the grid

No other code changes are needed.

---

## How It Works in Code

`TypeMatchupTable.GetMultiplier(defenderType, attackType)` returns the float multiplier.

In `Monster.CalculateDamage`, after crits, range, and obstruction:

```
finalDamage = baseDamage × typeMult × randomRoll(0.85–1.0)
```

A debug log is printed whenever the multiplier is not 1.0:

```
[TypeMatchup] Fire attack on Earth monster → x2.00
```

---

## Notes

- If the `TypeMatchupTable` field on `GameInitializer` is left empty, all attacks fall back to **x1.0** — the game still works without type advantages.
- Monster element type is set on `MonsterData` under **Monster General > Element Type**.
- Attack element type is set on `AttackData` under **Element > Element**.
- The standalone editor window can be found under **Window > Type Matchup Editor** if you ever close it and need it back.
</thinking>
