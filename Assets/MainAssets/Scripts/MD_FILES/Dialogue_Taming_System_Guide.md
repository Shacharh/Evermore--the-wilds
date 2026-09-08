# Dialogue & Taming System — Developer & Designer Guide

## Overview
The dialogue system is built on **xNode** — all dialogue is authored as graph assets in the Project window. Three dialogue types are supported: **NPC**, **World**, and **Taming**. NPC and World use a linear (or branching) node chain. Taming uses a pool of question nodes drawn randomly each session.

The taming system sits on top: it gates dialogue behind an acceptance roll, runs the Q&A, then makes a final tame roll based on the player's performance.

---

## Prerequisites

- **xNode** must be imported.
- **UIStyleConfig** (`Resources/UIStyleConfig`) must have a `PanelSettings` asset assigned — all dialogue UI depends on this.
- `DialogueUI` and `TamingSystem` are auto-created singletons; do not place them in the scene.

---

## Dialogue Type 1 — NPC Dialogue

Used for talking to characters. Follows a fixed node chain.

### Building the Graph

1. Right-click in Project → **Create > Evermore > Dialogue > Dialogue Graph**. Name it (e.g. `Shopkeeper_Graph`).
2. Double-click the asset to open the xNode editor.
3. **Start node** — right-click → **Dialogue > Start**. Fill in `Speaker Name` (shown in the panel header).
4. **Dialogue nodes** — right-click → **Dialogue > Simple Dialogue**. Fill in `Prompt` (the text body).
5. **Branching** — right-click → **Dialogue > Option Dialogue**. Add entries to the `Options` list. Each entry creates an output port (`option_0`, `option_1`, …). Connect each port to a different downstream node.
6. **End node** — right-click → **Dialogue > End**. Every path must terminate here. Leave `Outcome` as `None`.
7. Connect nodes: drag from the **next** output port of each node to the **input** port of the next.

### Triggering in Code

```csharp
[SerializeField] DialogueGraph npcGraph;

void Interact() => DialogueUI.Instance.OpenGeneral(npcGraph);
```

The player clicks the panel to advance. The panel closes automatically when an End node is reached.

---

## Dialogue Type 2 — World Dialogue

Used for interactive world objects (signs, shrines, chests with text). Identical to NPC dialogue now but kept separate so it can diverge in style later (e.g. no speaker portrait, different panel colour).

### Building the Graph

Same as NPC dialogue. You can use **Simple Dialogue** nodes or **World Dialogue** nodes (teal tint) — both work identically at runtime. Use World Dialogue nodes to visually mark a graph as belonging to a world object rather than a character.

### Triggering in Code

```csharp
[SerializeField] DialogueGraph worldGraph;

void OnInteract() => DialogueUI.Instance.OpenWorld(worldGraph);
```

---

## Dialogue Type 3 — Taming Dialogue

Used when the player attempts to tame an enemy monster. Pool-based: the graph holds a settings node and any number of question nodes. A random subset is drawn each session. **No node connections are needed.**

### Building the Graph

1. Right-click → **Create > Evermore > Dialogue > Dialogue Graph**. Name it after the species (e.g. `Slime_TamingGraph`).
2. Double-click to open the xNode editor.
3. **Taming Start node** — right-click → **Dialogue > Taming > Taming Start**. Configure:
   - `Questions Per Session` — how many questions to draw per attempt (e.g. `3`)
   - `Base Acceptance Override` — leave at `-1` to use the value from `TamingPersonality`, or set a `0`–`1` value to override for this graph only
4. **Taming Question nodes** — right-click → **Dialogue > Taming > Taming Question**. Add as many as you want. For each:
   - `Prompt` — the question text
   - `Question Type` — `Simple`, `Hard`, or `Tricky` (badge shown in UI)
   - `Answers[0..2]` — fill text and set tags. **Required: exactly one `Correct`, one `Wrong`, one `ReallyBad`.**
   - The custom node editor colours answers green / yellow / red and warns if tags are wrong.
5. **Leave all nodes floating** — no connections needed. The runner scans the graph automatically.

### Assigning to a Monster

Open the **MonsterData** ScriptableObject for the species. Under the **Taming** header:

| Field | Description |
|---|---|
| `Taming Personality` | Drag in a `TamingPersonality` asset (see below) |
| `Dialogue Graph` | Drag in the taming graph you just built |

---

## Taming Personality ScriptableObject

Each species needs a **TamingPersonality** asset. This is where all taming numbers live.

Create via **Assets > Create > Evermore > Taming > Taming Personality**.

| Section | Field | Description |
|---|---|---|
| Base Acceptance | `baseAcceptance` | Baseline tame probability 0–1 before any bonuses |
| Formula Weights | `hpBonusMax` | Max bonus added when monster HP is at 0 |
| | `bstNormDivisor` | Scales down the HP bonus for stronger species |
| | `critBonus` | Bonus if the last hit on this monster was a crit |
| | `statusEffectBonus` | Bonus per active status effect on the monster |
| | `superEffectiveBonus` | Bonus if the last hit was super effective |
| | `failedAttemptPenalty` | Subtracted per previous failed dialogue attempt |
| Dialogue | `dialogueMaxBonus` | How much a perfect Q&A session can add to the final roll |
| | `dialogueInitiationCost` | AP the player spends just to open the dialogue |
| | `apDebtPerReallyBad` | AP debt applied if the player picks a ReallyBad answer |
| Rewards | `partialSuccessGoldReward` | Gold given when the roll fails but the player got at least 1 correct |
| Debug | `debugGuaranteeTame` | Tick to skip the final roll and always tame on a perfect session |

---

## Taming Flow (Runtime)

1. **Player selects Dialogue** from the radial menu on an enemy monster tile.
2. **Guard checks:**
   - Must be the player's turn
   - Monster must not be `CommunicationLocked` (triggered by a past ReallyBad answer)
   - Monster's `MonsterData` must have both a `TamingPersonality` and a `DialogueGraph` assigned
3. **AP is spent** (`dialogueInitiationCost` from TamingPersonality).
4. **Acceptance roll:** `finalAcceptance = baseAcceptance + HP bonus + crit/SE bonuses + status bonuses − failed attempts penalty + any item bonuses`. If `Random.value > finalAcceptance`, the monster refuses and the dialogue does not open.
5. **Dialogue opens** — questions are drawn randomly from the graph (up to `questionsPerSession`).
6. **Player answers** — for each question:
   - **Correct** → score goes up, next question
   - **Wrong** → next question, no score
   - **ReallyBad** → session ends immediately with `CapturePenalty`
7. **Final roll** after all questions: `finalChance = acceptance + (score × dialogueMaxBonus)`. Roll decides outcome:

| Result | Condition |
|---|---|
| **Tamed** | `Random.value ≤ finalChance` |
| **Partial** (gold reward) | Roll failed but player got ≥ 1 correct answer |
| **Fail** | Roll failed and player got 0 correct |
| **Penalty** | Any ReallyBad answer — AP debt applied, monster heals 15% HP, `CommunicationLocked` set |

8. **On tame:** monster is removed from `EnemyTurnController`, added to `PlayerTurnController`, HP bar turns green, tile tint turns green.

---

## Assist Items in Taming

If the player has a **DialogAssist** item in their inventory, an **ASSIST** button appears during taming. Using it consumes one item and applies one of three effects to the current question:

| Assist Type | Effect |
|---|---|
| `HintReveal` | Correct answer is italicised |
| `EliminateOption` | One wrong answer is removed from the display |
| `AllowRetry` | Next wrong pick doesn't count — one free retry |

`usesPerSession` on the ItemData limits how many times the assist can trigger per dialogue session.

---

## Code API

```csharp
// Open NPC or World dialogue from a trigger/interactable
DialogueUI.Instance.OpenGeneral(graph);
DialogueUI.Instance.OpenWorld(graph);

// Taming is triggered by the radial menu automatically — but can be called directly:
TamingSystem.Instance.AttemptDialogue(monster);

// Add an item-based acceptance bonus before the next attempt
TamingSystem.Instance.AddAcceptanceBonus(0.2f);
```

---

## Common Mistakes

| Problem | Cause | Fix |
|---|---|---|
| Dialogue button missing from radial menu | Monster is `CommunicationLocked` | A ReallyBad answer was given in a past session — this species is permanently locked for that monster instance |
| "Stares blankly" message | No `TamingPersonality` or no `DialogueGraph` assigned | Assign both in MonsterData |
| Monster always refuses | `baseAcceptance` too low | Raise `baseAcceptance` in TamingPersonality, or use AcceptanceRateEnhancing items before initiating |
| Questions never show | `TamingStartNode` missing from graph | Add exactly one TamingStartNode |
| Compile error on answer tags | Not exactly one of each tag type | Each question must have one `Correct`, one `Wrong`, one `ReallyBad` — check the node editor warning box |
