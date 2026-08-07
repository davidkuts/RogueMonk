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
- **Perfect-dodge reward** (revised 2026-08-07, M11 — the refund alone tested as correct but unrewarding, because it pays out on the HUD, a second later, in a resource the player usually had anyway). It now also grants:
  1. **Focus** — a brief slow-motion window. The immediate sensory payoff, and also tactical: the slow is what gives room to walk into the punish just earned.
  2. **The Riposte** — a counter-attack on its own button (Triangle / Q) that does not exist until earned, and is spent on use. A wide arc, worth roughly a whole combo in one press.
- **Rejected 2026-08-07: a passive empowered next hit.** It was invisible — no button, no distinct sound, the same spark — and playtested as unnoticeable even on headphones. **A reward the player cannot perceive is not a reward.** Whatever a perfect dodge grants must be something they *do*, announced on screen, with its own sound.
- Dash charges are a shared offense/defense resource (see cancel rule)

### Hitbox shapes (locked 2026-08-07, M11.1)
- Melee attacks use **arcs — pizza slices centred on facing**, not spheres. A sphere in front of an attacker punishes standing anywhere near it, which reads as the attack simply happening to you; a wedge can be stepped out of sideways, so a telegraph is answered by *where you stand*.
- Full circles are reserved for attacks that genuinely are circles (the boss's Nova), so a ring telegraph always means "there is no side to step to".
- The telegraph decal renders the **actual hitbox**, arc included. A warning that could differ from the volume it describes would be worse than none.

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
- **Hue assignments** (locked 2026-08-07): **red** = melee arc or burst · **amber** = incoming projectile · **violet** = gap-closer, "it is coming to you" · **lime-yellow** = ground hazard, "this floor is about to hurt". Violet arrived with the boss's Slam and lime with its Eruption, each so a new threat class could never be confused with one already learned. A centred burst (the boss's Nova) deliberately stays red — it is still a melee threat, and the ground decal already distinguishes a disc around the attacker from an arc in front of it.
- **Ground decal** (added 2026-08-07, M10; extended to trash enemies in M11): attacks with a static footprint also paint the real hitbox on the floor during windup, filling from the centre outward so the fill reaches the outline exactly as the attack goes active. Colour says *what*; the decal says *where* and *when*. **It draws the actual hitbox, so it can never lie about reach** — that is the property that makes it trustworthy. Every telegraphed melee attack in the game uses it, so a wind-up is answered by **position** rather than by frame-perfect timing.
- Because telegraphs own the saturated end of the palette, **room tints must stay desaturated** — the boss room was retinted from red to cold slate for exactly this reason.

### Aiming (locked 2026-08-07, M11)
- **The stick outranks auto-aim, always.** Auto-aim is an assist: it snaps onto a target the player has not bothered to point at, and never overrides one they have.
- An attack **launches** in the stick's direction and keeps steering across its wind-up. Turning is not cancelling, so this leaves the never-cancel-a-wind-up rule intact — the attack still lands on its own frame data, only its direction is negotiable.

### Spawn fairness (locked 2026-08-07, M11)
- Every enemy has a **spawn grace** before it may attack. It still chases, so this is not a free window; it only prevents a hit the player had no opportunity to read.
- A spawn point too close to the player is **relocated at runtime**. The generated plan remains the authority on what spawns and how many, so a seed still reproduces the same fight.

### Player health
- HP number, **no healing at all** during the run
- Consequences: generous HP pool (~10–15 mistakes to die across a level); **mandatory ~0.5 s post-hit invulnerability** with character flash

### Level structure
- **6–7 rooms per level**, hand-authored room prefabs (8–12 templates) with tagged spawn points; randomize room selection, order, spawn population. NO fully-procedural geometry.
- **Rooms are discrete, Hades-style** (decided 2026-08-07): one room exists at a time and the exit door swaps it for the next — the player does not walk through connected geometry. This is what makes per-room camera confinement and per-room NavMesh baking work.
- **The last room is a boss room** (decided 2026-08-07): 5 ordinary rooms, then the boss. It is appended rather than drawn, so it is always last.
- **The boss room holds the boss and nothing else** (decided 2026-08-07, M10): one wave, one enemy, no escorts. Adds would wreck the readability of a 700 ms telegraph, and a lone boss is what lets its health bar mean "how far through this fight am I". The boss archetype carries selection weight 0, so it can only ever appear where the generator names it explicitly.
- **Wave spawns** within rooms
- Doors gate until room cleared; level complete when final room cleared
- NavMesh baked per room prefab offline
- MVP hazard budget: exactly ONE environmental hazard type (telegraphed floor hazard) — **spent 2026-08-07** on the boss's Eruption. It runs on the shared `AttackStateMachine` (telegraph = wind-up, eruption = active), so its timing and damage are an ordinary `AttackDefinition` and it inherits the same guarantee that a long frame cannot swallow its damage window.

### Enemies (MVP: two types)
- Melee humanoid: telegraphed lunge; Staggerable tier
- Ranged: telegraphed projectile; pick tier during tuning (candidate for Armored)

### Boss (added 2026-08-07, M10)
- **Immune tier, so it is never interrupted.** Its poise and armour pools are therefore zero — a non-zero pool would be a number the inspector shows but nothing can ever move.
- **A moveset, not an attack.** `BossDefinition` extends `EnemyDefinition` with moves (one or more chained attacks each), health-tied phases, and per-move range bands, cooldowns and weights. Ordinary archetypes are untouched by this.
- **Selection = deterministic legality gate, then a seeded weighted draw.** Pure random throws melee at twelve metres and read-and-react dies; pure scoring is memorised in half a minute. Repeats are discouraged, never forbidden.
- **Phases replace stagger.** Crossing a health threshold makes the boss finish its current swing, then stand inert and vulnerable — a punish window earned with damage rather than poise. This is what stops an un-interruptible enemy reading as unresponsive, and it does not violate the never-cancel-a-windup rule.
- **Boss randomness uses a stream derived from the run**, never the run stream itself: the number of draws depends on how the player fights, which would otherwise desynchronise every later draw and break seed reproducibility.
- **Greed punish** (added 2026-08-07): a designated **retaliation** move answers N hits taken inside a window, bypassing the **global** cooldown but never its own cooldown and never its own wind-up. This exists because an unconditional punish window meant a full combo was always free; making greed provoke an answer turns "two hits or three?" into a decision. Retaliations are **counter-only** — never in the ordinary rotation — or the move stops meaning "I got greedy". Out of *range* the debt stays owed; still *recharging* it is forgiven, because a counter that lands long after the greed reads as arbitrary. **The price is a range, re-drawn each time**, so it cannot simply be counted.
- **Setup-then-punish** (added 2026-08-07): the strongest difficulty lever that does not touch reaction time. A move whose first link denies ground in an arc **with the gap aimed back at the boss** herds the player to a known spot, and whose second link covers that spot. It stays fair only because at least two escapes remain open — dash out through the denial before it resolves, or take the gap and dodge the follow-up whose telegraph is already running. Design rule: **never leave exactly one answer.**
- **Difficulty comes from decision pressure and spacing, never from shortening tells.** Wind-ups stay at 650–750 ms. The levers are: cooldown length and *variance*, moves that deny ground, moves that punish a specific position, and projectiles that lead — all of which remain fully readable.

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
- **The dash has no animation clip** (decided 2026-08-07). Mixamo has none, and at 0.18 s — about five frames at 30 fps — a bespoke clip would be invisible. Genre convention is to sell a dash with VFX: afterimage ghosts in the reserved dash hue plus a trail, while the body keeps whatever pose it already had. `AnimationSet.Dash` stays optional; assigning a roll or dodge clip is possible but not required.
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
8. Mixamo models + Animancer playback + toon shader pass (shipped on Playables — Animancer not bought)
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
