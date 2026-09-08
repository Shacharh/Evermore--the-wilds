# Changelog — Items, Inventory, Dialogue & Taming Systems

## New Systems

### Dialogue System (xNode)
A graph-based dialogue system built on xNode. Supports three independent dialogue types, each with its own node set and trigger API.

**New files:**
- `Dialogue/DialogueGraph.cs` — ScriptableObject wrapper around xNode's NodeGraph
- `Dialogue/DialogueEnum.cs` — shared enums: `AnswerTag`, `QuestionType`, `DialogueOutcome`
- `Dialogue/DialogueRunner.cs` — runtime engine; drives both sequential and taming modes
- `Dialogue/Nodes/BaseDialogueNode.cs` — abstract base for all traversable nodes
- `Dialogue/Nodes/StartNode.cs` — entry point node; holds `speakerName`
- `Dialogue/Nodes/EndNode.cs` — terminal node; holds `DialogueOutcome`
- `Dialogue/Nodes/SimpleDialogueNode.cs` — single text prompt, one output
- `Dialogue/Nodes/OptionDialogueNode.cs` — branching node with dynamic output ports
- `Dialogue/Nodes/WorldDialogueNode.cs` — identical to SimpleDialogueNode, distinct tint for world objects
- `Dialogue/Nodes/Taming/TamingStartNode.cs` — taming settings node (questions per session)
- `Dialogue/Nodes/Taming/TamingQuestionNode.cs` — pool question node (prompt, type, 3 answers)
- `Editor/Nodes/TamingQuestionNodeEditor.cs` — custom xNode editor: colour-coded answers, tag validation

### Taming System
Orchestrates the full taming flow: acceptance roll → dialogue Q&A → final tame roll.

**New files:**
- `Taming/TamingSystem.cs` — auto-create singleton; handles the full taming loop
- `Taming/TamingPersonality.cs` — per-species ScriptableObject; all taming tuning values

### Inventory & Item UI
- `UI/InventoryUI.cs` — auto-create singleton UI Toolkit panel; shows items grouped by archetype

---

## Modified Files

### MonsterData.cs
- **Removed:** `baseAcceptance`, `personalityTamingModifier`
- **Added:** `tamingPersonality` (reference to `TamingPersonality` SO), `dialogueGraph` (reference to `DialogueGraph` SO)
- **Added:** `BST` computed property (sum of all base stats)

### Monster.cs
- **Added:** Taming state region — `CommunicationLocked`, `FailedDialogueAttempts`, `WasLastHitCrit`, `WasLastHitSuperEffective`
- **Added:** `LockCommunication()`, `IncrementFailedAttempts()`
- **Updated:** `TameMonster()` — flips `IsEnemy`, refreshes tile tint, fires `OnRosterChanged`
- **Updated:** `ResetForNewTurn()` — resets crit/SE flags each turn
- **Updated:** `ApplyDamage()` — sets crit and SE flags on hit

### MonsterHPBar.cs
- **Added:** `_fillImg` reference stored at build time
- **Added:** Subscribes to `Monster.OnRosterChanged`; calls `RefreshBarColor()` when any monster changes sides
- **Result:** HP bar turns green immediately when a monster is tamed

### Items/ItemEnum.cs
- **Added archetypes:** `AcceptanceRateEnhancing`, `DialogAssist`
- **Added enum:** `DialogAssistType` — `HintReveal`, `EliminateOption`, `AllowRetry`

### Items/ItemData.cs
- **Added fields:** `acceptanceBonus`, `appliesToEntireAttempt`, `assistType`, `usesPerSession`

### Editor/ItemDataEditor.cs
- **Added sections:** `DrawAcceptanceRateSection()`, `DrawDialogAssistSection()` for the two new archetypes

### Items/PlayerInventory.cs
- Starting item minimum quantity changed from 0 to 1

### UI/InventoryUI.cs
- Panel now centred on screen (was bottom-right)
- **Added:** `AcceptanceRateEnhancing` item handling — calls `TamingSystem.AddAcceptanceBonus()`
- **Added:** `DialogAssist` archetype blocked outside dialogue with a message

### UI/HUDController.cs
- Button layout changed to row: AP circle → INVENTORY → END TURN (was stacked vertically)

### UI/DialogueUI.cs
- `OpenGeneral(DialogueGraph)` — speaker name now read from `StartNode.speakerName`, no string parameter
- `OpenWorld(DialogueGraph)` — added; identical to OpenGeneral for now
- **Added:** Portrait panel in taming mode — shows monster sprite, name, element type, current HP

### Grid/InputManager.cs
- **Added:** `case RadialActionType.Dialogue` — triggers `TamingSystem.AttemptDialogue()`

### Grid/RadialMenu.cs
- **Added:** Dialogue option on enemy tiles when monster is not `CommunicationLocked`

### Grid/MenuOptionData.cs
- **Added:** `Dialogue` to `RadialActionType` enum

### Grid/Tile.cs
- `SetMonsterTint()` already existed; now called by `Monster.TameMonster()` to refresh tint on side-switch

### Grid/CameraController.cs
- **Added:** `mouseControlEnabled` field (default `false`) — gates right-mouse orbit and middle-mouse pan

### GeneralScripts/GameSettings.cs
- Edge scroll default changed from enabled to disabled

### GeneralScripts/GameInitializer.cs
- Removed `TamingConfig` reference (TamingConfig deleted)

### TurnsSystem/PlayerTurnController.cs
- **Added:** `APDebt` property, `AddAPDebt()`, `TrySpendAPForDialogue()`

### TurnsSystem/TurnController.cs
- **Added:** `AddMonster()`

### UI/TurnIndicator.cs
- `FeatureEnabled` changed from `const bool` to `static readonly bool` — fixes CS0162 unreachable code warning

---

## Deleted Files
- `Taming/TamingConfig.cs` — all data moved to `TamingPersonality`
