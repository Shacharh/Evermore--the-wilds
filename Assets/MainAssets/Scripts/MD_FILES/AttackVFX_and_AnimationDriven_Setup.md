# Attack VFX & Animation-Driven Attack Setup

## What Was Built

Two connected systems were added to the attack pipeline:

1. **Animation-driven attack execution** — damage and effects now fire from an animation event at the moment of impact, not immediately when the attack is selected.
2. **Per-attack VFX spawning** — each attack can spawn a VFX prefab either at the attacker's spawn point or at the enemy's grid cell position.

---

## Attack Execution Flow

```
Player picks attack
       ↓
InputManager calls monster.ExecuteAttack(targets, attackIndex, isDirect)
       ↓
ExecuteAttack:
  - consumes PP immediately
  - stores pending context on the Monster (targets, attackIndex, isDirect)
  - fires the Animator trigger
       ↓
Animation plays
       ↓
Animation Event calls OnAnimationEvent() on MonsterAnimationEventHelper
       ↓
MonsterAnimationEventHelper invokes its UnityEvent
       ↓
UnityEvent calls Monster.OnAttackAnimationHit()
       ↓
OnAttackAnimationHit:
  - applies all effects to all targets
  - spawns the VFX
```

If no animation trigger is configured on the `AttackEntry`, effects apply immediately as a fallback so existing monsters without animations continue to work.

---

## ScriptableObject Fields

### AttackData (per-attack, shared across all monsters)

| Field | Type | Description |
|---|---|---|
| `vfxPrefab` | `GameObject` | The VFX prefab to instantiate |
| `vfxTarget` | `AttackVFXTarget` (enum) | Where to spawn: `AttackerSpawnPoint` or `EnemyGridCell` |

`AttackVFXTarget` enum values:
- `AttackerSpawnPoint` — spawns once at the attacker's spawn point (with offset). Resolved from `vfxSpawnPointPath` on the `AttackEntry` at runtime.
- `EnemyGridCell` — spawns one instance per target at each target's world position (with offset).

### MonsterData → AttackEntry (per-monster, per-attack)

| Field | Type | Description |
|---|---|---|
| `AnimationTrigger` | `string` | Animator trigger name that plays this attack |
| `vfxSpawnPointPath` | `string` | Relative path to the spawn point child on the monster prefab (set via the custom picker, not typed manually) |
| `vfxSpawnOffset` | `Vector3` | World-space offset applied on top of the spawn point position |

The path and offset live on `AttackEntry` (not `AttackData`) because different monsters using the same attack have different body proportions and different bone positions.

At runtime, `Monster.SpawnAttackVFX` resolves the path by calling `transform.Find(entry.vfxSpawnPointPath)` on the live monster instance, so the position correctly reflects the animated pose at the moment the event fires.

---

## How to Wire the Animation Event

Animation events cannot call `Monster.OnAttackAnimationHit()` directly if the `Animator` lives on a child object (the model root) while `Monster` is on the parent. Unity only dispatches animation events to components on the **same GameObject as the Animator**.

The fix is `MonsterAnimationEventHelper` — a relay script that sits on the Animator's GameObject and forwards the event via a `UnityEvent`.

### Setup steps

1. Select the monster prefab and find the child GameObject that has the `Animator` component (usually the model root).
2. Add the **`MonsterAnimationEventHelper`** component to that same GameObject.
3. In the Inspector, expand **On Animation Event** and click **+**.
4. Drag the **parent** GameObject (the one with `Monster`) into the object slot.
5. Set the function to **`Monster → OnAttackAnimationHit()`**.
6. Open the **Animation** window (`Window > Animation > Animation`), select the attack clip, and scrub to the frame where the hit should land.
7. Click **Add Event** (flag icon on the timeline).
8. Set **Function** to `OnAnimationEvent` — this is the method on `MonsterAnimationEventHelper`.
9. No parameters needed.

---

## How to Set Up the VFX Spawn Point

### Why a path string instead of a Transform reference

`AttackEntry` lives inside `MonsterData`, which is a ScriptableObject (an asset file). Unity does not allow ScriptableObject assets to hold references to components (like `Transform`) that live inside a prefab's hierarchy. Attempting to drag a child Transform into such a field shows **"type mismatch"** in the Inspector.

The solution is to store the **relative path** as a string (e.g. `Body/Jaw/VFX_MouthSpawnPoint`) and resolve it at runtime with `transform.Find(path)`.

### Using the custom spawn point picker

`AttackEntryDrawer` provides a GUI so the path is set by clicking, not typed manually:

1. Open the monster's `MonsterData` asset.
2. Expand the relevant entry in **Move Pool**.
3. Click the **`▶  VFX Spawn Point`** button to open the picker. The button shows the current path in parentheses when one is already set, e.g. `▶  VFX Spawn Point  (Body/Jaw/VFXPoint)`.
4. In **Monster Root**, drag the monster's prefab root GameObject (from the Project window or the Hierarchy while in Prefab Mode).
5. Use the **Spawn Point** dropdown to pick the child Transform to use as the origin. The list shows every child in the prefab hierarchy.
6. The **Path (read-only)** field confirms the string that will be stored and used at runtime.
7. Click the button again to collapse and hide the picker. The path is saved; the picker fields reset on collapse and also reset automatically when you click away to a different asset.

### Creating the spawn point child

1. Open the monster prefab (double-click it in the Project window).
2. In the Hierarchy, expand the skeleton until you find the relevant bone (e.g. `Jaw`, `Mouth`, `Muzzle`).
3. Right-click the bone → **Create Empty** — name it `VFX_MouthSpawnPoint` (or similar).
4. In Scene view, position the empty at the desired origin (e.g. the tip of the open mouth).
5. Follow the picker steps above to set `vfxSpawnPointPath` to this new object.

---

## Inspector Assignment Checklist

For each monster + attack combination:

- [ ] `AttackData.vfxPrefab` — drag the VFX prefab
- [ ] `AttackData.vfxTarget` — choose `AttackerSpawnPoint` or `EnemyGridCell`
- [ ] `MonsterData.AttackEntry.AnimationTrigger` — the Animator trigger string for this attack
- [ ] `MonsterData.AttackEntry` → click **VFX Spawn Point** button → set Monster Root → pick child from dropdown
- [ ] `MonsterData.AttackEntry.vfxSpawnOffset` — fine-tune position if needed
- [ ] Monster prefab (Animator child) — add `MonsterAnimationEventHelper`, wire `OnAnimationEvent` → `Monster.OnAttackAnimationHit()`
- [ ] Animation clip — add an event at the impact frame calling `OnAnimationEvent`

---

## Gotchas

- **PP is consumed when the attack starts**, not when the hit lands. If the animation is interrupted before `OnAttackAnimationHit` fires, effects will never apply but PP is already spent.
- **Pending state is per-monster** — `_pendingTargets`, `_pendingAttackIndex`, `_pendingIsDirect`, and `_awaitingAnimationHit` are stored on `Monster`. If two attacks are queued on the same monster before the first event fires, the second overwrites the first. Avoid overlapping attack initiations on the same monster.
- **AOE**: `ExecuteAttack` takes a `List<Monster>` so all targets are processed together in one `OnAttackAnimationHit` call. The animation fires once regardless of target count.
- **Animator must be on the same GameObject as `MonsterAnimationEventHelper`** — the relay script and the Animator must share a GameObject. `Monster` can be on the parent.
- **Spawn point path is case-sensitive** — `transform.Find()` matches by exact name. If a bone is renamed in the prefab after the path is set, update the path via the picker.
