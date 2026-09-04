# Item & Inventory System — Developer & Designer Guide

## Overview
Items are ScriptableObject assets held by the player (not monsters). The inventory UI opens mid-battle and items are used immediately. Each item has an archetype that determines what it does.

---

## Creating an Item

1. Right-click in the Project window → **Create > Evermore > Item**
2. Fill in the Inspector:

| Field | Description |
|---|---|
| `Display Name` | Shown in the inventory panel |
| `Description` | Short tooltip text under the name |
| `Archetype` | What type of item this is (see archetypes below) |
| `AP Cost` | AP spent by the player to use the item (0 = free) |

The editor shows only the fields relevant to the chosen archetype — unrelated fields are hidden.

---

## Item Archetypes

### Healing
Restores HP to one or more monsters.

| Field | Description |
|---|---|
| `Heal Mode` | `Targeted` (click a monster), `AreaHeal` (AoE), `PartyHeal` (all allies instantly) |
| `Heal Amount` | Flat HP restored |
| `Heal Percent` | Additional heal as a fraction of the target's MaxHP |
| `Clears Status Effects` | If ticked, also removes all active statuses from the target |

- `PartyHeal` resolves immediately on use — no targeting required.
- `Targeted` and `AreaHeal` hand off to `InputManager` for target selection.

### Revival
Stub — requires the Reaction System (not yet implemented). Shows a message if used.

### Buff / Debuff
Applies a status effect to a target. Requires a `StatusEffectData` assigned to the item. Hands off to `InputManager` for targeting.

### AP Affecting
Modifies the player's current AP.

| Field | Description |
|---|---|
| `AP Delta` | Positive = gain AP, negative = spend AP |

### Acceptance Rate Enhancing
Used before initiating a taming dialogue. Adds a flat bonus to the next acceptance roll.

| Field | Description |
|---|---|
| `Acceptance Bonus` | Float 0–1 added to the roll (e.g. 0.2 = +20%) |
| `Applies To Entire Attempt` | Reserved for future use |

Using this item outside of taming still works — the bonus is stored on `TamingSystem` and consumed on the next dialogue attempt.

### Dialog Assist
Used **inside** an active taming dialogue to give the player an advantage on the current question. Cannot be used from the inventory panel — a message is shown instead. These items appear as the **ASSIST** button during taming.

| Field | Description |
|---|---|
| `Assist Type` | `HintReveal`, `EliminateOption`, or `AllowRetry` (see below) |
| `Uses Per Session` | How many times this item type can be used in a single dialogue session |

**Assist types:**
- **HintReveal** — italicises the correct answer so the player can identify it
- **EliminateOption** — removes one wrong answer from the display
- **AllowRetry** — if the player's next pick is wrong, they get one more attempt before it counts

---

## Setting Up the Inventory in a Scene

The `InventoryUI` creates itself automatically at runtime — no scene placement needed.

What you **do** need in the scene:

1. **PlayerInventory component** — add this to any persistent GameObject (e.g. a `GameManager` object).
2. In the Inspector, expand **Starting Items** and add entries:
   - Drag in an `ItemData` asset
   - Set `Quantity` (minimum 1)
3. The INVENTORY button in the HUD opens the panel. It is only accessible during the player's turn.

---

## Using Items at Runtime

- Open the inventory with the **INVENTORY** button (bottom-left of HUD, next to END TURN).
- Click **Use** on any item. The action depends on archetype:
  - Instant items (PartyHeal, AP, Acceptance Rate) resolve immediately and close the panel.
  - Targeted items close the panel and enter targeting mode — click a valid tile.
  - DialogAssist items show a message; use them from inside a taming dialogue instead.
- Items with `AP Cost > 0` deduct AP before resolving. If the player has insufficient AP the item is blocked.

---

## Code API

```csharp
// Check if player has an item
PlayerInventory.Instance.HasItem(itemData);

// Remove one of an item
PlayerInventory.Instance.RemoveItem(itemData);

// Get all items as a list of (ItemData, quantity) tuples
PlayerInventory.Instance.GetAll();

// Add an acceptance bonus before the next taming attempt
TamingSystem.Instance.AddAcceptanceBonus(0.2f);
```
