# CLAUDE.md — Evermore: The Wilds

## Project Overview
Turn-based tactical RPG in Unity. Core systems: grid-based combat, AP-driven turns, scoring AI, type matchups, ScriptableObject-driven data.

Working directory: `Assets/MainAssets/Scripts/`

---

## Rules Claude Must Follow

### Rule 1 — Editor Script Reference Table
Whenever you create a custom Unity Editor script for a runtime file, add a row to the table below linking the editor script to the runtime file it serves. Keep this table up to date any time an editor file is created, moved, or renamed.

### Rule 2 — Session Documentation on /clear
Before every `/clear` (or when the user says "clear the chat"), you MUST:
1. Identify which subsystem(s) were worked on this session.
2. Create or update the relevant setup/documentation `.md` file for that subsystem inside `MD_FILES/` (see "Documentation Files" below for the pattern).
3. The doc must include:
   - **What was built / changed** this session (summary)
   - **How to create the necessary Unity objects** (GameObjects, components, menu paths)
   - **How to set up the ScriptableObjects or assets** involved
   - **Any wiring / inspector assignments** required
   - **Gotchas or known issues** discovered during the session
4. Only then confirm the clear.

If no code was changed this session, a one-line update to the relevant doc noting "no changes — session was read-only" is sufficient.

### Rule 3 — Check Editor Scripts When Editing Runtime Files
Whenever you edit a runtime file that has a corresponding editor script (see the Editor Script Reference Table below), you MUST:
1. Check whether the change affects the Inspector UI — e.g. added/removed/renamed serialized fields, changed field types, added new sections, or changed how data is structured.
2. If yes, update the editor script(s) accordingly so the Inspector stays correct and functional.
3. If no change to the editor is needed, note it briefly before moving on.

### Rule 4 — All MD Files Go in MD_FILES/
Every documentation `.md` file (setup guides, system guides, feature docs) MUST be created inside `MD_FILES/`. Never create documentation markdown files inside subsystem folders (AI/, Attacks/, TurnsSystem/, etc.). The only exception is `CLAUDE.md` itself, which stays at the Scripts root.

> Tip: use the Editor Script Reference Table below to know which runtime files have editor counterparts.

---

## Editor Script → Runtime File Reference Table

| Editor Script | Runtime File | Notes |
|---|---|---|
| `Editor/TypeMatchupTableEditor.cs` | `Attacks/TypeMatchupTable.cs` | Custom property drawer for the NxN grid |
| `Editor/TypeMatchupTableGUI.cs` | `Attacks/TypeMatchupTable.cs` | Shared GUI drawing helper used by editor and window |
| `Editor/TypeMatchupTableWindow.cs` | `Attacks/TypeMatchupTable.cs` | Standalone window: Window > Type Matchup Editor |
| `Editor/AttackEntryDrawer.cs` | `Monsters/MonsterData.cs` | PropertyDrawer for AttackEntry — VFX spawn point picker (drag root → choose child) |
| `Editor/AttackDataEditor.cs` | `Attacks/AttackData.cs` | CustomEditor — ReorderableList for effects; draws category-specific fields including Stage Count for buff |
| `Editor/AttackDataDrawer.cs` | `Attacks/AttackData.cs` | PropertyDrawer — renders AttackData references as a dropdown popup |
| `Editor/ItemDataEditor.cs` | `Items/ItemData.cs` | CustomEditor — shows archetype-relevant fields; includes AcceptanceRateEnhancing and DialogAssist sections |
| `Editor/Nodes/TamingQuestionNodeEditor.cs` | `Dialogue/Nodes/Taming/TamingQuestionNode.cs` | Custom node editor — colour-codes answers by tag, validates exactly 1 Correct/Wrong/ReallyBad |

---

## Documentation Files (per subsystem)

All docs live in `MD_FILES/`. Never put them anywhere else (see Rule 3).

| Subsystem | Doc File | What it covers |
|---|---|---|
| Turn System | `MD_FILES/TurnSystem_Documentation.md` | Setup, AP flow, API reference |
| AI System | `MD_FILES/AI_SYSTEM_GUIDE.md` | Scoring formula, ScriptableObjects, personalities |
| Type Matchup / Attacks | `MD_FILES/TypeMatchupTable_Setup.md` | Effectiveness values, editor window usage |

When working on a subsystem that doesn't yet have a doc file, create one in `MD_FILES/` following the same structure.

---

## Project Structure (quick reference)

```
Scripts/
├── AI/                  MonsterAIBrain, AIContext, AIAction, AIGameStateScorePoints, MonsterPersonality
├── Attacks/             AttackData, AttackDatabase, AttackEffect, TypeMatchupTable, StatusEffectData
├── Attributes/          ShowIfAttribute (conditional inspector fields)
├── Dialogue/            DialogueGraph, DialogueRunner, DialogueEnum; Nodes/: StartNode, EndNode,
│                        SimpleDialogueNode, OptionDialogueNode; Nodes/Taming/: TamingStartNode, TamingQuestionNode
├── Editor/              TypeMatchupTable editor tools + ItemDataEditor; Nodes/: TamingQuestionNodeEditor
├── GeneralScripts/      GameInitializer (singleton, loads DB + TypeMatchupTable on start)
├── Grid/                GridManager, Tile, InputManager, RadialMenu, CameraController, Obstruction
├── Items/               ItemData, ItemEnum, PlayerInventory (AcceptanceRateEnhancing + DialogAssist archetypes)
├── MD_FILES/            ALL documentation .md files live here (see Rule 3)
├── Monsters/            Monster, MonsterData (+ taming fields), MonsterAttack, MonsterSetup, MonsterSpawner
├── Player/OverWorldPlayer/  Overworld traversal only — not used in battle
├── Taming/              TamingSystem (auto-create singleton), TamingConfig (Resources/TamingConfig)
├── TurnsSystem/         TurnManager, PlayerTurnController (APDebt), EnemyTurnController, TurnController
├── UI/                  HUDController, MonsterInfoPanel, AttackInfoPanel, MonsterHPBar, BattleMessage,
│                        InventoryUI, DialogueUI (auto-create singleton, sortingOrder=300)
└── FileLogger.cs
```

## Key Conventions
- Movement cost: `Speed / 10` tiles per AP (min 1), called `TilesPerAP` — not `moveCost`.
- Pathfinding: BFS on the grid.
- All attacks, monster types, and AI configs are ScriptableObject assets.
- `Monster` instances share a `MonsterData` blueprint (multiple Slimes share one `SlimeData`).
- `TurnManager` and `GameInitializer` are singletons that survive scene loads.
- AI scoring: `finalScore = AIGameStateScorePoints + MonsterPersonality + Random(0, jitter)`.
