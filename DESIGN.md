# BETWEEN SECONDS (working title) — Design Document

Top-down action roguelike (Hades-2-like). One highly mobile monk character; melee combos (punch, punch, kick), short Genji-style dash with i-frames. Combat is read-and-react: every enemy attack is telegraphed and human-reactable. Clear rooms of enemies; level ends when the final room is cleared.

## Theme (locked 2026-08-08)

**The full pitch lives in `THEME.md` at the repo root — that file is the source of truth for fiction, and this section is only the part that binds engineering and content work.** It was settled elsewhere and is treated as final; the reason it is written down here at all is that a theme living in a chat log is a theme that gets lost.

Tagline: *"She dies at 6:14. You have all the time in the world."*

- **Premise in one line.** June, a physicist, dies sealing the breach of her own time machine at 6:14 PM. Cole — her partner, a martial artist, not a scientist — grabs her prototype wrist unit, the **Second Hand**, and falls to the deepest anchor she ever pinged. He climbs the eras back toward 6:14 to stop it. He fails. He jumps again. **The run structure is the story**, and each loop drags debris through history, which is why the eras bleed into each other.
- **The existing moveset is already canon — nothing in the simulation needs renaming to fit.** Dash = the **Blink**. Perfect dodge = the **Split Second**. The Riposte is the counter that comes from between two instants. No healing = his body has no present tense. These are diegetic names for systems that already ship; treat them as the player-facing vocabulary (HUD, sounds, tutorial text), not as a refactor.
- **Amber is the stagger system as lore.** Time hardens around fixed events like amber. Staggerable = loose in time · **Armored = amber-crusted, fused to a fixed point** · Immune = **Menders**, constructs made of set time. This is why the Armored tier finally has a reason to exist as a design space — see the palette conflict below, which must be resolved before any amber enemy is built.
- **Contamination is the art direction, and it is the solo-dev dividend.** Biomes are eras *infected by previous loops*: bronze in Cretaceous mud, hieroglyphs of a falling man, knights with jet-lances. Mixing asset packs across wildly different eras is normally incoherent; here it is the point, unified by the one toon shader and the palette strip. **Contamination props deliberately keep their source-era palette band so they read as intrusions.**
- **The villain is the Custodian** — causality's immune system, a courteous man with a watch that does not tick. June's death is a *containment weld*: saving her tears history open. He is a sympathetic jailer who might be right, which is what makes him work as a repeat final boss where "evil future you" would not. Future-Cole exists as a **mid**-boss, not the end.
- **Signature sound: the tick.** The Custodian's watch and the Split Second share one perfect tick. Worth designing early — it is the audio identity of the whole game, and the vortex's ready-state cue already leans on it.

### Biomes and the run
Chronological ascent, four full biomes (6–7 rooms each) plus one short final gauntlet: **Cretaceous → Egypt → Greece → Medieval → 6:14**. Bosses: **Tyrant** (the T-rex that comes back carrying the debris of your past fights) → **the Twice-Crowned** → **Talos** → **Mordred** → **the Custodian**.

- **A run stays 3 levels for now.** The theme wants five and the code takes any number, but content is what is missing, not capacity. The three levels that already exist map onto biomes 1–3, so the shipped run becomes Cretaceous → Egypt → Greece with no code change and a per-level boss instead of the Stone Warden three times. Extending to five is a content decision, not an engineering one.
- **The Stone Warden is a placeholder for Tyrant.** Its moveset is fine; what changes is the model, the name and the biome around it.

### Boons are the Deep Ages
The six elements are not gods but epochs so old that history's bleeding does not reach them: **the Hadean** (Fire) · **the Long Winter** (Ice) · **the Green Reach** (Nature) · **the First Breath** (Wind, patron of the Blink) · **the Bedrock** (Earth) · **the Unspent** (Force). `DamageType` is untouched — this is a naming and flavour pass over the six existing boon assets, and one of the cheapest wins available.

### Conflicts with decisions already locked (resolve before building to them)
1. ⚠️ **Amber is triple-booked, and this is the one that blocks enemy work.** The theme reserves amber-gold for the Armored tier "everywhere, no exceptions". But the telegraph grammar already assigns **amber = incoming projectile**, and the perfect-dodge trail plus the Riposte spark are **gold**. Three different meanings on one hue defeats the whole point of the grammar. **Recommended resolution: the theme keeps amber, and the projectile telegraph moves off it** — the armour colour is load-bearing narratively and the projectile hue is one value in two assets. Magenta is the cheapest free hue that stays legible against the muted palette. Needs a human call; see the hue assignments below.
2. **Talos is described as an Armored-tier showcase, but bosses are Immune tier.** Both cannot be literally true. Likely answer: Talos is Immune like every boss, and its amber plating is a *phase* layer that the Split Second cracks — which is the fantasy the pitch actually describes.
3. **THEME §8 wants the combat set hand-keyframed**; the shipped pipeline is Mixamo clips speed-fitted to frame data (`AnimationSet.SpeedToFit`). Not a contradiction so much as a later upgrade — game feel is the product, and hand-keying is where it ends up. Nothing to do now.
4. **Hub, meta-progression, wreckage currency, PETRI, June's portraits** are all post-MVP. Recorded so they are not re-invented, explicitly not scoped.

### Still open (author's call, not Claude's)
Cole's real name and the name of his discipline · June's arc and how much she notices · the true ending · Mordred's resolution · the cat's name · final title call. None of these block engineering.

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
- **Perfect dodge:** if dash i-frames overlap an attack's active frames, the charge is refunded instantly (distinct SFX/flash).
- **Dodge grace** (added 2026-08-07): protection extends a little past the i-frames, still counting as a perfect dodge. This exists because melee and projectiles are *not* equally dodgeable — a projectile's hitbox travels toward the player so any instant of the window catches it, while a melee swing is live for a tenth of a second and the player must still be standing in the arc. Without the grace one is comfortable and the other is frame-perfect.
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

### The Vortex — default anti-swarm ability (added 2026-08-08, **built 2026-08-08, M13**)
Diegetically **the Undertow**: the Second Hand drags the local second into a whirlpool, everything loose in time slides toward the drain, and what is set in amber holds. (Working name — *the Draw* and *Turnabout* were the alternatives; it is one string to change.) Bound to **○ / B on pad, E on keyboard**, deliberately clear of the Riposte on △ / Q.

A radial spin (~0.6 s) that **pulls every staggerable enemy in radius to an inner ring around the player**, dealing light damage ticks on the way in and delivering them **briefly staggered on arrival**.

- **It exists because the base kit otherwise has no answer to being swarmed.** Without it every multi-enemy room is "kite until a boon fixes it", and a player who never draws the right boon has no tool at all. The default kit must solve the default problem.
- **The arrival stagger is non-negotiable.** The ability voluntarily drags threats into hug range; if they arrive mid-wind-up it is a self-inflicted wound, not a tool. Pull = interrupt, always.
- **Its job is space and setup, not damage.** Base damage stays modest; boons are what scale it into a damage tool. Ticks resolve through the ordinary hit resolver, so elemental boons land on it via the existing `IHitModifier` pipeline with zero new plumbing. The intended combat sentence is **vortex gathers → supercharged attack spends the pile** — discoverable, not tutorialised.
- Phases follow the universal grammar: windup (committed) → active (pull + ticks) → recovery, **dash-cancellable from its first frame** per the cancel rule. Cancelling early keeps whatever pull already happened but forfeits the remaining ticks — a real mid-spin decision.
- **Pull is negative knockback** through the existing manual velocity impulse. Same system, reversed sign, no new movement code path.

**Stagger-tier interaction** — Staggerable: pulled, ticked, staggered on arrival. **Armored: takes ticks and armour damage but resists the pull** — amber holds its position, and the failed drag flares gold. This keeps the tier readable in a single frame (who slid and who did not) and preserves the Split Second as the only answer to amber; a late Bedrock/Force boon that lets the vortex move even amber is the reserved upgrade slot. Immune: full hit feedback, no displacement, no interrupt, consistent with tier 3 everywhere else.

- **Resolved while building it (2026-08-08): the resistance ends when the armour does.** DESIGN already says an Armored enemy "behaves as tier 1" once its armour is stripped, and making the pull the one exception would be a special case with nothing behind it. So an Armored enemy resists while its amber is intact and is pulled like anything else afterwards. The numbers keep the read clean: one vortex is 3 × 8 = 24 poise against 40 armour, so a *fresh* Armored enemy cannot be cracked and dragged by the same spin. Cracking amber remains a thing you do first, with something else.
- **The arrival stagger is not forfeited by a dash-cancel.** It is the promise that answers the ability's own risk — it drags threats into hug range — so making the cancel drop it would turn a choice into a trap. Cancelling forfeits the remaining *ticks*, nothing else.

**Recharge is hits, not perfect dodges.** Baseline cooldown ~10 s; every landed player hit shaves ~0.4 s; whiffs shave nothing. Aggressive play cycles it roughly twice as fast as passive play, and passive play still gets it on a fair timer. It is **explicitly not** tied to perfect dodges: a perfect dodge already pays three rewards (charge refund, Focus, Riposte), and a fourth would overload one input and make the entire kit degrade at once for players who struggle with timing — the exact frustration this ability exists to prevent. Vortex uptime rides the accessible skill axis; perfect dodging rides its own. HUD is one radial dial beside the dash pips, and readiness must be perceivable without looking at it (dial glow plus the game's signature **tick**) or players will sit on it.

**Watch in playtest:** pulling *ranged* enemies out of their firing positions is likely stronger than the swarm-clear itself. Acceptable — it is skill expression and it reinforces "space, not damage" — but it is precisely why base damage must stay low and must never scale except through boons.

**Animation & VFX** (locked 2026-08-08):
- **A stationary grounded spin.** The body stays planted and rotates in place; it must not travel, because the vortex moves *enemies*, and a spin that also slid the player would read as the player being pulled too, inverting the whole idea. Code owns position as always.
- **Silhouette: Spinning Crane Kick** — a wide horizontal leg sweep at a low-to-mid guard, so the shape itself says "everything around me at once" rather than "the thing in front of me". Reading at a glance from the top-down camera is the only job the pose has. **Shipped clip: Mixamo "Hurricane Kick"** (1.833 s, speed-fitted to the 0.82 s attack).
- **Clip fitted to the 120 / 450 / 250 ms phases**, so wind-up, spin and settle land on the frame data rather than near it. The established mechanism is `AnimationSet.SpeedToFit` — one clip fitted to one attack length; fitting a single clip across all three phases is the cheap version, and if the spin's visual sweep drifts off the 450 ms pull it needs per-phase mapping, which is a small piece of real work rather than a data tweak.
- **Time-identity is carried by the VFX, not the body.** The pose is ordinary martial arts; what makes it a time ability is the effect — the Blink's chromatic smear, circular — in the **reserved dash hue**, exactly as the dash itself is sold entirely on VFX in that hue. This keeps the whole Second Hand kit reading as one colour family. ⚠️ That hue currently also means *dash charges*: if the pips stop reading as "my dashes" in play, the vortex effect is what changed, and the fix is a shifted tint rather than a new colour.
- **The spin direction and the VFX swirl direction must match.** A body turning one way inside a vortex turning the other reads as broken before anyone can say why. Whichever way the chosen clip rotates is the authority; the effect follows it.
- ⚠️ **Clip collision with the Riposte**, whose own animation TODO names "Standing Melee Attack 360 High" / "Spin Kick" — the same family. Two spins that look alike would blur the game's two most distinct buttons. They must differ on silhouette: the Riposte is a **directional** sweep committing forward through one target, the vortex is **stationary and symmetrical**, going nowhere. Pick the Riposte clip with that contrast in mind, or hand it the travelling one.

**Playtest knobs** (all ScriptableObject, never hardcoded): radius 4 m (too big trivialises rooms, too small whiffs on spread swarms) · inner ring 1.5 m (inside kick range, outside body-block jank) · 120/450/250 ms phases (active must feel instant-ish under pressure) · 3 light ticks, total ≤ one punch, poise per tick ≈ punch-level · arrival stagger 0.4 s (long enough to start a supercharge, short enough not to chain-stun) · baseline cooldown 10 s · per-hit refund 0.4 s (a 3-hit combo ≈ 1.2 s) · pull-immunity 1.0 s after arrival, which is what prevents vortex→vortex juggle loops once boons cut the cooldown.

```csharp
VortexDefinition {           // or extend AttackDefinition with a nullable pull block
  float radius, innerRing;
  float pullDurationSec;     // = active phase
  int   tickCount; float tickDamage, tickPoiseDamage;
  float arrivalStaggerSec;
  float cooldownSec, perHitRefundSec;
  DamageType damageType;     // Physical; boons stamp elements via the pipeline
}
```

**Build slot:** the original note placed it at step 4.5, after the melee enemy and before the ranged one, because the pull needs bodies to prove itself and the ranged enemy is its best stress test. Both of those shipped long ago, so it now sits **alongside enemy variety** — a swarm answer wants a swarm to answer, and the archetype that swarms does not exist yet.

### Stagger tiers (enemies)
1. **Staggerable** (most trash): poise bar → break = interrupt + hit reaction + brief vulnerability, poise refills after delay
2. **Armored** (some normals): armor bar must be stripped first, then behaves as tier 1
3. **Immune** (elites/bosses): full hit feedback (flash, hitstop, SFX) but never interrupted, no knockback
- Rule: immune enemies get longer/louder telegraphs (~650–750 ms windup vs ~450 ms for staggerable melee)

### Telegraph grammar
- Melee windups 400–500 ms; ranged 600–800 ms (human reaction ~250 ms + read margin)
- Consistent visual language: same color = same threat type; every attack has an audio cue
- Gameplay-reserved hues: saturated colors used ONLY for telegraphs, projectiles, dash trail, elemental FX — never in the environment
- **Hue assignments** (locked 2026-08-07): **red** = melee arc or burst · **amber** = incoming projectile ⚠️ *(contested — the theme reserves amber-gold for the Armored tier; see Theme § conflicts. Recommended move: magenta.)* · **violet** = gap-closer, "it is coming to you" · **lime-yellow** = ground hazard, "this floor is about to hurt". Violet arrived with the boss's Slam and lime with its Eruption, each so a new threat class could never be confused with one already learned. A centred burst (the boss's Nova) deliberately stays red — it is still a melee threat, and the ground decal already distinguishes a disc around the attacker from an arc in front of it.
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
- **Variety is the next milestone, and it is now themed** (2026-08-08): the first archetype set is **Biome 1, the Cretaceous** — contaminated fauna and a stranded time-tourist camp, with the amber-crusted Armored tier finally built around as a threat rather than merely supported by `PoiseSystem`. The archetypes worth having are the ones that give the control boons and the Vortex a reason to exist: something that **closes fast** (root answers it), something that **swarms** (chill and the Vortex answer it), something that **punishes standing still**. Adding one is a `EnemyDefinition` + `AttackDefinition` + `EnemyArchetypeDefinition` + prefab, with no code changes.

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
- **Theme-derived, added 2026-08-08** (full reasoning in `THEME.md` §2, §8): the limited palette is now a **palette strip** — one gradient atlas, all meshes UV'd to bands, one texture per biome — and **contamination props deliberately keep their source-era band** so they read as intrusions rather than as mistakes. **Amber is a shader showcase**: translucent, refractive, glowing, and one good amber material carries the entire Armored-tier visual language. Human enemies across all eras share one humanoid skeleton (hoplite / guard / knight = same rig, different silhouette and palette); **Tyrant and Talos are the two bespoke rigs worth real time**, and they are also the marketing images.

### UI
- ESC / pad Start: pause menu (resume, restart level, quit)
- HUD: HP bar, 2 dash pips
- Death screen w/ stats

## Elemental boons — BUILT 2026-08-07 (M12)
After each level the player picks a power: fire, ice, wind, earth, nature, force. The three seams reserved in M3 are what made this a data-and-wiring job rather than a rewrite:
1. Hit resolution goes through a **modifier pipeline** — boons are `IHitModifier`s at Order 50
2. Every damageable entity has a **status effect container** — Burning/Chilled/Rooted now have consumers
3. AttackDefinition carries a **DamageType enum** — now drives spark colour

### Locked decisions
- **A run is N levels**, each ending in a boss, with a boon choice between them. The run *waits* for the choice; it is the only moment in a run that is a decision rather than an execution.
- **Difficulty must escalate per level.** Without it, later levels are easier than earlier ones, because the player arrives carrying boons and the content never grew to answer them.
- **Status magnitudes are global, in `StatusSettings` — never on the boon.** Burning always burns at the same rate whatever inflicted it, so the player learns a status once. A boon decides *whether* to apply one, not how hard it bites. This is why the status container stores only durations.
- **Damage-over-time bypasses the hit resolver.** It is the consequence of a hit that already resolved; running it back through the pipeline would let a burn modifier re-apply burning from its own damage, forever.
- **A Physical boon never stamps its damage type**, or stacking a pure-damage boon after an elemental one would blank the element.
- **Offers are drawn by shuffling and taking the front**, never by picking and rejecting duplicates — the latter consumes an unpredictable number of draws and desynchronises the seed.
- Boons are **offensive only** for now: the pipeline is wired on the attacker's resolver. A defensive boon would have to register on every enemy's instead.

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
