# Monk Roguelike — MVP Design Document

Top-down action roguelike (Hades-2-like). One highly mobile monk character; melee combos (punch, punch, kick), short Genji-style dash with i-frames. Combat is read-and-react: every enemy attack is telegraphed and human-reactable. Clear rooms of enemies; level ends when the final room is cleared.

## Locked decisions

### Platform & tech
- Engine: **Unity 6.3 LTS**, URP, C#
- Packages: Input System (new), Cinemachine 3, AI Navigation, Unity Test Framework
- MCP bridge: **Coplay unity-mcp** (free, MIT, local, no sign-in) installed from day one; batchmode CLI + Editor.log as fallback. Official Unity MCP (requires Unity Cloud link + AI trial) is an optional upgrade, never required
- 3D game, fixed top-down-ish camera
- Editor: Enter Play Mode Options with Domain Reload OFF; Force Text serialization; Git LFS for fbx/textures/audio
- Assembly definitions: Game.Core, Game.Combat, Game.Enemies, Game.Level, Game.UI (+ .Tests each)
- Core rule: **simulation layer (combat resolution, state machines, room graph, RNG) is plain C# with zero MonoBehaviour dependency**. MonoBehaviours are thin adapters. Simulation is covered by EditMode tests.

### Camera
- Cinemachine perspective, ~50° pitch, FOV 30–35°, fixed yaw, no player camera control
- Slight damped look-ahead toward facing/aim (~1–1.5 m)
- Screenshake via Cinemachine impulse, driven by hitstop events
- Camera confined to current room bounds (per-room confiner collider)

### Input
- Controller-first (like Hades). PS5 DualSense + Xbox pads + WASD/mouse all supported
- Input System, two control schemes (Gamepad, KeyboardMouse), auto-switch on last-used device
- IAimSource abstraction: combat code never knows the active device
- Radial deadzone + response curve on left stick
- Rumble via Gamepad.SetMotorSpeeds, tied to hitstop events
- Glyph swapping (✕/○ vs A/B) via TMP sprite assets keyed by control scheme
- Known risk: DualSense differs over USB vs Bluetooth; Steam Input remaps DualSense to XInput — test both paths

### Movement & dash
- Built-in CharacterController (collide-and-slide vs walls), fully code-driven; no Rigidbody movement; root motion OFF everywhere
- Knockback = manual velocity impulse
- Dash: ~4 m over ~0.18 s; i-frames cover first ~85% of the dash; never crosses room boundaries
- **2 dash charges**, **sequential recharge — only one charge refills at a time**, ~1.5 s each (revised 2026-08-06 after playtest; was "2.5 s each, independent parallel timers", which made both charges return together and removed the cost of burning them back-to-back); two pips on HUD
- **Perfect dodge:** if dash i-frames overlap an attack's active frames, the charge is refunded instantly (distinct SFX/flash). Strict overlap required.
- Dash charges are a shared offense/defense resource (see cancel rule)

### Attacks & combo
- Combo: punch → punch → kick, aimed by facing + Hades-style soft auto-aim (nearest enemy in ~45° cone within range; facing rotates toward target over the windup, no instant snap; cone/snap speed per-attack data)
- Attack phases: windup (committed, NOT cancellable) → active (hitbox live) → recovery
- **Recovery is dash-cancellable from its first frame** — costs a dash charge (the skill-ceiling rule)
- ~150 ms input buffer on attack/dash inputs (mandatory)
- Starting frame data (tune later): punch 100/60/180 ms, punch2 90/60/200 ms, kick 180/90/350 ms (kick ≈3× poise damage); combo window 400 ms; hitstop 60 ms light / 100 ms heavy
- All timings/values live in ScriptableObjects, never hardcoded

### Stagger tiers (enemies)
1. **Staggerable** (most trash): poise bar → break = interrupt + hit reaction + brief vulnerability, poise refills after delay
2. **Armored** (some normals): armor bar must be stripped first, then behaves as tier 1
3. **Immune** (elites/bosses): full hit feedback (flash, hitstop, SFX) but never interrupted, no knockback
- Rule: immune enemies get longer/louder telegraphs (~650–750 ms windup vs ~450 ms for staggerable melee)

### Telegraph grammar
- Melee windups 400–500 ms; ranged 600–800 ms (human reaction ~250 ms + read margin)
- Consistent visual language: same color = same threat type; every attack has an audio cue
- Gameplay-reserved hues: saturated colors used ONLY for telegraphs, projectiles, dash trail, elemental FX — never in the environment

### Player health
- HP number, **no healing at all** during the run
- Consequences: generous HP pool (~10–15 mistakes to die across a level); **mandatory ~0.5 s post-hit invulnerability** with character flash

### Level structure
- **6–7 rooms per level**, hand-authored room prefabs (8–12 templates) with tagged spawn points; randomize room selection, order, spawn population. NO fully-procedural geometry.
- **Rooms are discrete, Hades-style** (decided 2026-08-07): one room exists at a time and the exit door swaps it for the next — the player does not walk through connected geometry. This is what makes per-room camera confinement and per-room NavMesh baking work.
- **The last room is a boss room** (decided 2026-08-07): 5 ordinary rooms, then the boss. It is appended rather than drawn, so it is always last. Boss mechanics are not in the MVP yet; the room is signalled by tint, banner and an advance warning.
- **Wave spawns** within rooms
- Doors gate until room cleared; level complete when final room cleared
- NavMesh baked per room prefab offline
- MVP hazard budget: exactly ONE environmental hazard type (telegraphed floor hazard)

### Enemies (MVP: two types)
- Melee humanoid: telegraphed lunge; Staggerable tier
- Ranged: telegraphed projectile; pick tier during tuning (candidate for Armored)

### RNG & death flow
- **Seeded runs from day one**: single RNG owned by RunContext; room order, spawns, (later) boon offers all draw from it; seed logged at run start
- Death screen with run stats (time, rooms, damage dealt/taken, perfect dodges) + hold-to-retry
- New seed on retry by default; debug "retry same seed" key for development
- EditMode soak tests replay seeded generation and assert solvability

### Art direction
- Cel shading, 2–3 hard light bands, thin dark outlines
- Strict limited palette (~6 environment colors); saturated hues reserved for gameplay info
- Shader: Flat Kit or Toony Colors Pro 2 (URP)
- Gray-box (capsules) until milestone 8; animations from Mixamo (Humanoid rig, retargeted to enemies), root motion off, clips trimmed to match frame data
- Animation playback: Animancer (preferred) or Playables API — clips driven from code, no Animator Controller graphs

### UI
- ESC / pad Start: pause menu (resume, restart level, quit)
- HUD: HP bar, 2 dash pips
- Death screen w/ stats

## Future system (architect now, build later): elemental boons
After each level the player picks a power: fire, ice, wind, earth, nature, power/force — modifying attack mechanics. NOT in MVP, but the combat core must not preclude it:
1. Hit resolution goes through a **modifier pipeline**: resolver builds a HitContext and passes it through an ordered IHitModifier list (empty in MVP)
2. Every damageable entity has a **status effect container** (MVP's only status = stagger)
3. AttackDefinition carries a **DamageType enum** (default Physical) from day one

## Data model (ScriptableObjects)
```csharp
AttackDefinition {
  float windupSec, activeSec, recoverySec;
  bool  cancellableOnRecovery;     // true for all player attacks
  float comboWindowSec;
  HitboxShape shape; float damage;
  DamageType damageType;           // Physical for MVP
  float poiseDamage;
  float knockback, hitstopSec;
  float autoAimConeDeg, aimSnapSpeed;
}

EnemyDefinition {
  float health;
  StaggerTier tier;                // Staggerable | Armored | Immune
  float poiseMax, poiseRegenDelay, poiseRegenRate;
  float armorMax;                  // Armored only
  float staggerDurationSec;
  AttackDefinition[] attacks;
}
```

## Build order
0. Repo, packages, asmdefs, LFS, CLAUDE.md, Unity MCP bridge, gray-box room, Cinemachine rig
1. CharacterController movement + wall slide + camera follow (capsule)
2. Dash: travel curve, i-frames, charges, perfect-dodge refund — tune until perfect before proceeding
3. Combat data system: AttackDefinition SOs, hit resolver + modifier pipeline, hitstop, screenshake, combo + cancel windows, input buffer. EditMode tests.
4. Enemy base, health/poise/stagger tiers, death. Melee enemy w/ telegraphed lunge
5. Ranged enemy + projectile + telegraph
6. Room manager: prefab templates, seeded selection, wave spawner, door gating, clear condition, confiner
7. Pause menu, restart, HUD, death screen + stats
8. Mixamo models + Animancer playback + toon shader pass
9. SFX (whiff/hit/dash/perfect-dodge/enemy death), VFX, rumble, polish

## Model routing (Claude Code)
| Work | Model |
|---|---|
| Architecture, CLAUDE.md, asmdef layout | Fable 5 |
| Combat frame-data system, dash/i-frame/cancel state machine | Fable 5 |
| Hit-resolution modifier pipeline design | Fable 5 |
| Room generation + solvability | Fable 5 |
| First enemy AI + telegraph state machine | Fable 5 |
| Second enemy, gameplay iteration, MonoBehaviour glue, bug fixes | Sonnet 5 |
| Editor tooling, SO authoring, test scaffolding, data tables | Haiku 4.5 |
| Debugging after two failed attempts | escalate to Fable 5 |
