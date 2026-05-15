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
Animation Event calls monster.OnAttackAnimationHit()
       ↓
OnAttackAnimationHit:
  - applies all effects to all targets
  - spawns the VFX
```

If no animation trigger is configured on the `AttackEntry`, effects apply immediately as a fallback so existing monsters without animations continue to work.

---

## ScriptableObject Fields Added

### AttackData (per-attack, shared across all monsters)

| Field | Type | Description |
|---|---|---|
| `vfxPrefab` | `GameObject` | The VFX prefab to instantiate |
| `vfxTarget` | `AttackVFXTarget` (enum) | Where to spawn: `AttackerSpawnPoint` or `EnemyGridCell` |

`AttackVFXTarget` enum values:
- `AttackerSpawnPoint` — spawns once at the attacker's spawn point (with offset). Uses the spawn point Transform from the monster's `AttackEntry`.
- `EnemyGridCell` — spawns one instance per target at each target's world position (with offset).

### MonsterData → AttackEntry (per-monster, per-attack)

| Field | Type | Description |
|---|---|---|
| `vfxSpawnPoint` | `Transform` | Bone child on this monster's prefab used as the spawn origin |
| `vfxSpawnOffset` | `Vector3` | World-space offset applied on top of the spawn point position |

The offset and spawn point are on `AttackEntry` (not `AttackData`) because different monsters using the same attack will have different body proportions and different bone positions.

---

## How to Wire the Animation Event

1. In Unity, open the **Animation** window (`Window > Animation > Animation`)
2. Select the monster's attack animation clip
3. Scrub to the frame where the hit should land (e.g. the moment the claw connects or the fireball leaves the mouth)
4. Click **Add Event** (the flag icon on the timeline)
5. In the Inspector for the event, set **Function** to `OnAttackAnimationHit`
6. No parameters are needed

The `Monster` component on the same GameObject receives the call automatically.

---

## How to Set Up the VFX Spawn Point (Bone Child Method)

This is the standard Unity approach for effects that should follow a moving body part (e.g. a fireball spawning from an open dragon mouth).

### Why it works
A rigged 3D model has a bone hierarchy. Each bone is a Transform in the prefab's hierarchy. Child Transforms of a bone automatically follow that bone's position and rotation during every animation — including mid-animation poses like an open jaw.

### Steps
1. Open the monster's prefab
2. In the **Hierarchy** panel, expand the skeleton until you find the relevant bone (e.g. `Jaw`, `Mouth`, `Head_Lower`, `Muzzle`)
3. Right-click the bone → **Create Empty** — name it `VFX_MouthSpawnPoint` (or similar)
4. In **Scene** view (with the prefab open), position the empty at the tip of the mouth opening
5. Open the monster's `MonsterData` asset
6. Find the relevant `AttackEntry` in the Move Pool
7. Drag the `VFX_MouthSpawnPoint` Transform into the `vfxSpawnPoint` slot

At runtime, `entry.vfxSpawnPoint.position` returns the world-space position of that empty at whatever frame `OnAttackAnimationHit()` fires — which is already the animated (open-mouth) position.

### Vertex tracking (NOT used here)
It is technically possible to sample a vertex position directly from a `SkinnedMeshRenderer` each frame, but this requires knowing a specific vertex index and is significantly more complex. The bone-child empty approach is the Unity standard and has zero runtime cost.

---

## Inspector Assignment Checklist

For each monster + attack combination:

- [ ] `AttackData.vfxPrefab` — drag the VFX prefab
- [ ] `AttackData.vfxTarget` — choose `AttackerSpawnPoint` or `EnemyGridCell`
- [ ] `MonsterData.AttackEntry.AnimationTrigger` — the Animator trigger string for this attack
- [ ] `MonsterData.AttackEntry.vfxSpawnPoint` — drag the bone-child empty from the monster prefab
- [ ] `MonsterData.AttackEntry.vfxSpawnOffset` — fine-tune position if needed
- [ ] Animation clip — add an `OnAttackAnimationHit` event at the impact frame

---

## Gotchas

- **PP is consumed when the attack starts**, not when the hit lands. If the animation is interrupted before `OnAttackAnimationHit` fires, effects will never apply but PP is already spent. This is acceptable for the current design.
- **Pending state is per-monster** — `_pendingTargets`, `_pendingAttackIndex`, `_pendingIsDirect`, and `_awaitingAnimationHit` are stored on `Monster`. If two attacks are somehow queued on the same monster before the first animation event fires, the second call will overwrite the first. Avoid overlapping attack initiations on the same monster.
- **AOE**: `ExecuteAttack` takes a `List<Monster>` so all targets are stored and processed together in one `OnAttackAnimationHit` call. The animation fires once regardless of how many targets are in the list.
- **ScriptableObject Transform references**: `vfxSpawnPoint` in `AttackEntry` must reference a Transform from a **prefab asset**, not a scene object. Scene-object references in ScriptableObjects are not serialized and will be lost.
