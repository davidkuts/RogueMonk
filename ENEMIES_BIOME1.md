# ENEMIES_BIOME1.md — The Cretaceous (Deepest Anchor)

Locked enemy roster for Biome 1 of BETWEEN SECONDS. Companion to THEME.md, STORY.md, DESIGN.md.

**Biome thesis:** The Cretaceous is the deepest anchor point — temporal flotsam sinks here. The entire food chain is ranked by how much broken time (amber) each creature has consumed. Scrapfeathers scavenge it, Sailspit spits it, Ambershell wears it, the Tyrant is saturated with it. Killing the apex releases the biome's major boon: the ecosystem explains the reward.

---

## 1. Readability rules (non-negotiable)

**One shape class per enemy.** At the ~50° top-down camera, players read backs, heads, and posture. No two enemies share a body plan.

**One identity color per enemy.** Cel-shading flattens texture detail; hue carries identity. Identity colors must stay out of the reserved gameplay channels.

**Reserved semantic colors (from DESIGN.md palette rules):**

| Color | Meaning | Never used for |
|---|---|---|
| **Amber (orange-gold)** | Solidified time: armored tier, armor plates, stall zones, boss armor patches | Enemy identity color on staggerable enemies |
| **Red / white flash** | Attack telegraph / hit confirm | Idle enemy coloration |
| **Cyan-white ghost** | Temporal echo (Twice-Struck ghost, future echo VFX) | Environment props |

Amber is a *semantic channel*, not a skin. If it glows amber, the player's rule is: "that is hardened time — it blocks stagger or blocks movement."

**No ceilings — ever (project-wide rule).** All arenas and rooms are open-sky. No enemy ability, boss phase, or level mechanic may depend on ceiling, rim, or overhang geometry. Telegraphed air attacks are allowed, but every vertical threat must be **sky-sourced** (falls from above the camera / materializes mid-air) and **ground-telegraphed** (impact circle on the floor). This rule applies to all biomes, all abilities, and all level design going forward — record it in DESIGN.md as well.

**Silhouette + color taxonomy at a glance:**

| Enemy | Shape class | Identity color | Tier |
|---|---|---|---|
| Swiftjaw | Small, low, darting biped | Teal | Entry |
| Cerashorn | Wide frill, quadruped | Brick red | Entry |
| Sailspit | Tall vertical sail | Violet | Entry (ranged) |
| Scrapfeathers | Swarm carpet of tiny bodies | Slate green | Entry |
| Ambershell | Low glowing dome | Amber (armored — allowed) | Elite |
| Twice-Struck | Large horned biped + trailing ghost | Bone white (ghost: cyan) | Elite |
| The Tyrant | Colossus with embedded debris | Storm gray-green, amber accents | Boss |

Black-blob test: all seven silhouettes are distinguishable with zero color and zero animation. Color is the second layer, not the crutch.

---

## 2. Entry tier

### 2.1 SWIFTJAW — raptor pack (rusher)

- **Archetype:** Fast melee flanker. Staggerable.
- **Shape class:** Small, low, horizontal darting biped.
- **Identity color:** Teal body, pale underbelly.
- **Role:** Split Second economy engine; teaches dash-as-positioning. The baseline every room composition is built on.

**Behavior:** Hunts in offset pairs — while one attacks, the other circles to flank. Attack cooldowns are staggered so the player always has one active threat and one repositioning threat.

**Moveset:**
- *Pounce bite* — short crouch telegraph (0.4s), lunge along a line. Premium perfect-dodge bait.
- *Snap combo* — two quick swipes at melee range; second swipe has a slightly longer wind-up (rhythm trap).

**Why challenging:** Never the enemy you're looking at. Their job is to punish tunnel vision on bigger threats.

**Tuning notes:** Low HP (2–3 light hits). Spawn in pairs minimum. Cap simultaneous attackers at 2 regardless of pack size (Hades-style attack token system).

---

### 2.2 CERASHORN — juvenile ceratopsian (charger)

- **Archetype:** Baitable line charger. Staggerable.
- **Shape class:** Wide frill, horns, quadruped stance — unmistakable from above.
- **Identity color:** Brick red frill and back, dun body.
- **Role:** Teaches baiting and environmental play. Emergent friendly-fire generator.

**Moveset:**
- *Line charge* — paws the ground (0.8s telegraph, dust VFX), locks a straight line, charges. Cannot steer once committed.
  - Sidestep → slams wall, self-stuns 1.5s (punish window).
  - Dash *through* → Split Second, player ends up behind it.
  - Bait into other enemies → charge damages and knocks down anything in its path. Intended, encouraged, fun.
- *Frill shove* — close-range 120° push with knockback, low damage. Exists so hugging it isn't free.

**Why challenging:** Alone, a tutorial. In a mixed wave, its charge lines carve the arena into moving no-go corridors while Swiftjaws work your flanks.

**Tuning notes:** Medium HP. Charge should one-shot Scrapfeathers and knock down Swiftjaws — players discovering this is a designed delight.

---

### 2.3 SAILSPIT — sail-backed amber-eater (artillery)

- **Archetype:** Ranged area denial + pattern fan. Staggerable. **Sole ranged enemy of Biome 1.**
- **Shape class:** Tall vertical sail — the only vertical silhouette in the biome.
- **Identity color:** Violet sail and body. Throat and sail glow **amber only during attack telegraphs** (it is channeling eaten amber — semantic color used correctly).
- **Role:** Both ranged lessons in one body: area denial and projectile reading.

**Moveset:**
- *Amber glob* — throat glows amber (0.6s), lobs an arcing projectile. Direct hit: minor damage. Splash hardens into a **stall zone**: amber-tinted puddle, slows *movement only* (not attacks), radius ~1.5m, duration ~2s. Shapes the arena; never feels like a stun.
- *Spine fan* — sail rattles and flashes (0.7s), whips tail to flick a fan of 3–5 spines in a spread. Gaps between spines are dash-through lanes. Projectile-reading exercise.

**Behavior:** Skittish. Backpedals when the player closes; forces committed dashes rather than walk-downs. Once cornered, it's helpless — the lesson "closing distance counters ranged" pays off in every later biome.

**Tuning notes:** Low-medium HP. Max 2 per room in early levels. Stall zones from multiple Sailspits should be able to overlap but not stack slow values.

---

### 2.4 SCRAPFEATHERS — carrion swarm

- **Archetype:** Swarm / space-clogger. Staggerable (trivially — they die to anything).
- **Shape class:** A moving carpet of tiny bodies. Collides with no other silhouette by definition.
- **Identity color:** Slate green, glinting eyes.
- **Role:** Makes sweeping attacks valuable; makes every other enemy scarier by clogging space around it.

**Behavior:** Flocking swarm of 6–12. Individually one-hit kills. Nibble for chip damage on contact; crucially, their bodies **block dash lanes** (dash still i-frames through, but you land where they are and take contact chip on landing frames). They scavenge time-junk — the bottom of the amber food chain.

**Moveset:** Contact nibble only. No telegraphed attacks. The swarm *is* the attack.

**Why challenging:** They cost almost nothing alone and change everything in combination. A Cerashorn charge you'd casually sidestep becomes lethal when Scrapfeathers own the sidestep space.

**Tuning notes:** Cheapest enemy in the game to build: one tiny mesh, GPU instanced, boids-lite flocking. Sweep-type player attacks should feel gloriously efficient against them. Never spawn as the only enemy type in a wave.

---

## 3. Elites

### 3.1 AMBERSHELL — ankylosaur (armored showcase)

- **Archetype:** Armored-tier teaching elite. **Armored** (no stagger, heavy damage reduction) on plated zones; soft zones are staggerable-normal.
- **Shape class:** Low, wide dome. The only enemy that **glows amber at rest** — the armor color language introducing itself.
- **Identity color:** Amber plating over dark umber hide (reserved color used exactly as intended).
- **Role:** Teaches the armored tier and positional damage.

**Zones:**
- *Armored:* skull, back dome, flanks — amber plates. No stagger, ~70% damage reduction.
- *Soft:* tail base, underside — full damage, staggerable.

**Moveset:**
- *Tail sweep* — 270° rear arc, big slow telegraph (1.0s). Jump-the-gap timing via dash i-frames.
- *Rolling charge* — tucks and rolls in a line. Baited into a wall → **cracks its own plating**, exposing temporary armored-zone weak points (8s), then re-hardens.
- *Compression stomp* — if the player camps its underside too long, a delayed AoE self-slam. Anti-degenerate-strategy valve.

**Why challenging:** The Ambershell never kills you. The Swiftjaws that catch you mid-circle do. It is a positioning tax collected while the room does the killing.

**Encounter design:** First appearance is a **solo mini-arena** so the armor lesson lands clean. Every later appearance has escorts.

---

### 3.2 TWICE-STRUCK — carnotaurus (echo elite)

- **Archetype:** Delayed-read duelist. Staggerable but tanky.
- **Shape class:** The only large biped below the boss. Horned skull, sprinter build — and a **translucent ghost of itself trailing 0.5s behind**, a silhouette feature no other enemy can fake.
- **Identity color:** Bone white body; ghost rendered in reserved cyan-white echo shader.
- **Role:** The game's thesis as an enemy. Trains the delayed-read discipline the Custodian weaponizes in Biome 5.

**Core mechanic:** Every attack happens **twice**. The real attack lands; 0.5s later a ghost replay traces the identical arc with identical hitbox. Dodging the attack is not enough — you cannot dodge *into where it just was*.

**Moveset:**
- *Horn rush* — short charge with head swing. Then the echo rush along the same line.
- *Skull hook* — rising headbutt arc at melee range. Echo follows.
- *Double stamp* — AoE stomp ring. Echo ring expands from the same point.

**Interaction rules:**
- Perfect-dodging the *real* attack grants the Split Second as normal. Perfect-dodging the *echo* also counts — generous, and it teaches players to engage with the mechanic instead of just running.
- Staggering the real body **despawns the pending echo** (the loop is interrupted). Reward for aggression.

**Why challenging:** Doubles the effective attack density without doubling animation count. Punishes rhythm memorization; rewards reading.

**Build note:** Cheapest spectacular elite possible — ghosted duplicate mesh, delayed animation playback, one palette swap. The toon shader makes the echo pop for free.

---

## 4. Boss — THE TYRANT

The apex predator of the deepest anchor: a T-rex that has eaten temporal garbage for sixty-six million subjective years. Embedded in its hide: scaffolding, chain, amber boulders, and — small, glinting — a wristwatch.

- **Shape class:** Colossus. Class of one.
- **Identity color:** Storm gray-green hide; amber debris accents that grow more prominent per phase.
- **Arena:** The Shedding Ground — an open, bone-littered clearing under open sky, ringed by half-buried time-junk (a rusted gantry, a fossilized filing cabinet). No ceiling, no overhangs; all vertical threats are sky-sourced and ground-telegraphed.
- **Stagger tier:** Immune (boss tier), with scripted stagger windows (wall-slam, plate-crack).

### Phase 1 — The Predator (100% → 66%)
Pure animal. Teaches the vocabulary.
- *Lunge bite* — fast, short telegraph. Premium Split Second bait.
- *Tail arc* — 270° sweep. Dash-timing check.
- *Arena charge* — crosses the clearing. Dashing **through** it (not away) is the stylish dodge and grants flank position. Impact with the junk ring at the arena edge = brief scripted stagger window.

### Phase 2 — The Hoard Wakes (66% → 33%)
The debris in its hide resonates.
- **Amber patches harden across flanks and skull** — body parts become armored-tier mid-fight. The player must re-learn where to hit.
- *Junk-rain roar* — the roar tears at the anchor itself: debris from other seconds **materializes in mid-air overhead** (brief shimmer high above, then a falling object) with ground telegraph circles marking each impact. Ambient area denial layered over the Phase 1 moveset. Sky-sourced only — no ceiling, no rim geometry; the junk phases into existence, which is also better lore: the Tyrant's roar is loud enough to shake things loose from *time*, not from rock.

### Phase 3 — The Stutter (33% → 0%)
The stolen time destabilizes.
- The Tyrant **skips frames**: mid-charge it flickers and reappears ~3m ahead; the tail sweep plays its windup, drops a beat of silence, then executes.
- Every learned telegraph returns with corrupted timing — testing whether the player learned the *read* or just the rhythm.
- Deliberate gentle preview of the Custodian's fight, four biomes early.

**On kill:** The amber in its hide shatters — first major boon moment of the game.

**Seed flavor:** Each run, the Tyrant spawns with one different piece of visible junk pulled from the run seed. Cosmetic, cheap, screenshot bait.

---

## 5. Wave composition guidance

- **Vocabulary rooms (rooms 1–2):** Single new type + Swiftjaws. Introduce one verb at a time.
- **Sentence rooms (rooms 3–5):** Two to three types. Reference compositions:
  - Sailspit ×2 + Swiftjaw pack → "close distance under fire"
  - Cerashorn ×2 + Scrapfeathers → "your dodge lanes are owned"
  - Ambershell + Swiftjaw pack → "positioning tax"
- **Elite room (room 5–6):** One elite + light escort. Never two elites in Biome 1.
- **Boss (room 6–7):** The Tyrant, solo. Phase 2 junk-rain provides the "adds" pressure without add spawns.
- Scrapfeathers never spawn as a wave's only type.

## 6. Deferred content (designed, not built)

- **Stormstitch** — pterosaur dive-bomber. Silhouette-unique (flyer), but carries animation/readability tax. Revisit post-vertical-slice.
- Alpha Swiftjaw variant (pack-buff shriek) if Biome 1 needs a third mini-elite for depth patching.

## 7. Implementation order (per enemy)

1. **Capsule graybox** — primitive shapes, real hitboxes, real telegraph timings (colored decals/flashes). Prove the fight is fun before any art exists.
2. **Lock timings** — telegraph durations, cooldowns, attack tokens tuned against Cole's kit.
3. **Mesh** — concept (Flux + style LoRA) → Hunyuan3D → discard AI textures → palette-strip remap → master toon shader.
4. **Rig** — auto-rig pass (quadruped/biped templates), manual cleanup on weight painting only where deformation visibly breaks.
5. **Animate** — library/pack animations retargeted where possible; hand-key or AI-assist only the signature telegraph moves (the telegraph *is* the gameplay; it gets the animation budget).
6. **Swap** — capsule → skinned mesh behind the same enemy controller. Mechanics never wait on art.

Build order: Swiftjaw → Sailspit → Cerashorn → Scrapfeathers → Ambershell → Twice-Struck → Tyrant. (Cheapest loop-provers first; the boss last, when the player kit is stable.)
