# HANDOFF.md

Context document for Claude Code. Read this before touching the project.

## What this is

A League of Legends–style MOBA prototype in **Unity 6.3 LTS (6000.3.2f1)**, URP,
using the **new Input System** (`UnityEngine.InputSystem`). Solo project, early
prototype stage. Core camera + click-to-move + basic ranged combat work.

Repo root: `C:\Users\user\League of Legends MMORPG Idea`

## Hard constraints — do not violate

- **New Input System only.** `UnityEngine.Input` (legacy) throws
  `InvalidOperationException` at runtime. Use `Mouse.current`, `Keyboard.current`.
- **No `localStorage`-style assumptions** — this is a real Unity project, not a sandbox.
- **Do not edit scene or prefab files directly.** Go through the Unity MCP bridge
  or ask. `.unity` / `.prefab` are text-serialized but hand-editing corrupts references.
- **Do not touch `.meta` files.** Deleting or regenerating them breaks every
  Inspector reference in the scene.
- **Exit Play mode before scene operations.** They silently fail otherwise.
- **Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`.** Already in `.gitignore`.

## Known open issue

`com.unity.ai.assistant` and `com.unity.ai.generators` both ship an asmdef
defining `Unity.AI.Generators.IO.Srp`, causing:

```
Assembly with name 'Unity.AI.Generators.IO.Srp' already exists
```

Fix is to remove one package or update both. The MCP bridge lives in
`com.unity.ai.assistant`, so this must be resolved for MCP to work.

## Scripts (`Assets/Scripts/`)

| File | Class | Attached to | Purpose |
|---|---|---|---|
| `topDownCamera.cs` | `MobaCamera` | Main Camera | Top-down camera rig |
| `CursorLock.cs` | `CursorLock` | GameManager | Confines cursor, Esc releases |
| `ClickIndicator.cs` | `ClickIndicator` | GameManager | Ground click marker ring |
| `CursorManager.cs` | `CursorManager` | GameManager | Swaps cursor art on enemy hover |
| `ChampionController.cs` | `ChampionController` | Champion | Right-click move / attack orders |
| `ChampionCombat.cs` | `ChampionCombat` | Champion | Range ring, targeting, firing |
| `Health.cs` | `Health` | Champion + enemies | HP, damage, death |
| `RangeIndicator.cs` | `RangeIndicator` | *(runtime only)* | Range ring visual |
| `Projectile.cs` | `Projectile` | *(runtime only)* | Homing bullet |

`RangeIndicator` and `Projectile` are **never added in the Inspector**.
`ChampionCombat` calls `AddComponent<RangeIndicator>()`; `Projectile.Spawn()`
creates bullets in code. Adding them manually creates duplicates/orphans.

## Architecture notes

### Camera (`MobaCamera`)

Does **not** parent to the champion. Tracks a `focus` point on the ground plane
and derives `transform.position` from `focus`, `pitch`, and `currentDistance`
each frame. This is what makes lock/unlock, edge panning, and minimap jumps all
trivial — change `focus`, everything else follows.

- Pitch 55°, distance 12–26, FOV should be ~35 (60 looks fisheye)
- `Y` toggles champion lock, hold `Space` to centre
- Edge pan is gated behind `edgePanInEditor` so the camera doesn't run away
  when the cursor leaves the Game view during development
- Bounds clamp `focus` to a `Rect` (x = world X, y = world Z)
- Public: `FocusOn(Vector3)`, `SnapToTarget()`, `SetLocked(bool)`

### Ground raycasting

`ClickIndicator.TryGetGroundPoint()` is the **single source of truth** for
screen→world. `ChampionController` and `ChampionCombat` both delegate to it so
the marker and the actual destination can never disagree. Uses a math `Plane`
by default (`useColliders = false`) so it works without ground colliders; flip
to collider mode once terrain has real height variation.

### Order resolution — `ChampionController.Update()`

```
right-click pressed
  └─ enemy under cursor?  → combat.AttackTarget(), set attackOrderActive
  └─ else                 → combat.CancelOrders(), move order, green marker
```

`attackOrderActive` exists because `holdToRepath` re-runs this block every frame
while the button is held. Without the flag, the held-button path immediately
cancels the attack order that the initial press just issued.

### Order resolution — `ChampionCombat.HandleArmedClick()`

`A` toggles the range ring (light blue). While armed, left-click resolves:

```
1. enemy under cursor  → attack that one (ignores range; chases)
2. nearest in range    → attack it
3. nothing             → attack-move to clicked point
```

Red marker in all three cases. Ring dismisses in all three.

### Combat loop

`TickCombat()` chases if out of range, otherwise `agent.ResetPath()`, faces the
target via Slerp, and fires on a `1/attacksPerSecond` cooldown.
`Projectile` homes to the target Transform and applies damage on arrival — damage
is applied directly, **not** through physics collision. Bullets have no collider.

### Procedural visuals

`RangeIndicator` and `ClickIndicator` build their meshes/lines at runtime.
No prefabs, textures, or custom shaders. Both use `Sprites/Default` (falls back
to URP Particles/Unlit). Circles are generated in local **XY** then rotated 90° on
X to lie flat — building them in XZ *and* rotating tips them on edge (was a bug).

`RangeIndicator`'s inner glow is a triangle-fan mesh of `radialSteps` concentric
rings with alpha `Lerp(centerAlpha, rimAlpha, t^falloff)` baked into **vertex
colors**. `falloff ≈ 2.6` pins the glow to the rim, matching LoL.

## Scene setup that is NOT in code

These are Inspector values. They will be lost if the scene is overwritten.

- **NavMeshAgent on Champion:** Speed 6, Angular Speed 1000, Acceleration 60,
  Stopping Distance 0.1, Auto Braking **off**. Defaults feel like a barge.
- **NavMeshSurface** on the floor, baked. Must re-bake after floor changes —
  it does not update automatically. `NavMesh.SamplePosition` silently drops
  orders when there's no navmesh under the click.
- **Enemy layer** must exist. `ChampionCombat.enemyMask` and
  `CursorManager.hoverMask` should be set to it, not `Everything`.
- **Enemies need a Collider** — `OverlapSphere` and raycasts find nothing without one.
- **Cursor textures:** Texture Type `Cursor`, Read/Write **on**, Alpha Is
  Transparency **on**, Mip Maps **off**, format **RGBA 32 bit**, Compression None.
  Anything else → "Invalid texture used for cursor".

## Third-party assets

`RPG Monster DUO PBR Polyart` — free pack, built for **Built-in RP**. Materials
render magenta until converted: Window → Rendering → Render Pipeline Converter →
Built-in to URP → Material Upgrade.

## Current state

Working: camera (pan/zoom/lock), click-to-move, click marker, range ring toggle,
target acquisition, projectile damage, death, custom cursors.

Not built yet: abilities, mana, health bars, minimap, respawn, AI, multiple
champions, networking, continuous attack-move (currently fires once on click and
just walks — doesn't re-scan while moving).

## Working style

- Prefer small, verifiable changes over large rewrites.
- Give complete files when a script changes substantially; diffs for one-liners.
- Flag when a change requires Inspector work — I can't infer that from a diff.
- Explain *why* something broke, not just the fix.
