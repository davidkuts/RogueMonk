# ASSETS_BIOME1.md — Mesh & Animation List (The Cretaceous)

Companion to ENEMIES_BIOME1.md. Defines the minimum shippable asset set per enemy: mesh spec, rig template, and animation clip list. Built for the locked pipeline: Flux concepts → Hunyuan3D mesh → discard AI textures → palette-strip remap → master toon shader.

---

## 1. Production tags

Every clip is tagged by how it gets made:

- **[R] Retarget** — sourced from an animation pack / library and retargeted onto our rig. Nobody studies these mid-combat. Zero hand-animation.
- **[A] Authored** — hand-keyed or Cascadeur-assisted. Reserved for **signature telegraphs only** — the frames that *are* the gameplay. This is the entire animation budget.
- **[C] Code/VFX** — no animation clip at all; achieved via runtime playback tricks, shader/material states, or particle FX.

**Budget rule:** [A] clips across the whole biome should total under ~15 short clips. If an [A] count grows, a design is too animation-hungry — simplify the design, not the schedule.

**Retarget sourcing note:** Raptor, ceratopsian, ankylosaur, carnotaurus, spinosaurid, and T-rex are among the most common rigged-and-animated dinosaur archetypes on the Unity Asset Store — this roster was silhouette-designed, but it is also *conveniently retarget-friendly*. Buy one or two quality packs; the toon shader equalizes everything on contact.

---

## 2. Mesh specs (shared)

| Property | Entry tier | Elites | Boss |
|---|---|---|---|
| Tri budget | 5–12k | 15–30k | 40–60k |
| Materials | 1 (palette strip) | 1 (+1 amber/echo state) | 2 (hide + debris/amber set) |
| Textures | None — palette-strip UV only | Same | Same |
| LODs | None (top-down, small on screen) | None | None |
| Blendshapes | None | Ambershell: none (see states) | None |

- **Silhouette check gate:** before rig, render each mesh as a flat black blob at gameplay camera angle next to the others. If any two blur, fix the mesh, not the color.
- **Scrapfeathers exception:** one ~800-tri mesh, GPU-instanced. This is the whole point of the design.
- **Hunyuan3D guidance:** generate at T-pose / neutral stance from a side-and-front concept pair; dinosaurs are forgiving subjects (organic, no faces to uncanny). Retopologize only if auto-rig deformation visibly breaks at the shoulders/hips/tail root.

## 3. Rig templates

Four rigs cover seven enemies:

1. **Raptor-biped** (light biped + tail): Swiftjaw
2. **Quadruped-heavy** (quad + tail): Cerashorn, Ambershell, Sailspit (sail is a 2–3 bone chain add-on)
3. **Carno-biped** (heavy biped + tail): Twice-Struck
4. **Colossus-biped** (heavy biped + tail + jaw + 2–3 debris jiggle bones): Tyrant

Scrapfeathers: no skeleton — vertex-animated or 2-bone hop, instanced.

---

## 4. Per-enemy clip lists

### 4.1 SWIFTJAW — 7 clips
| Clip | Tag | Notes |
|---|---|---|
| Idle (alert sway) | [R] | |
| Run | [R] | The clip on screen 90% of its life — pick a good one |
| Pounce crouch (telegraph) | **[A]** | 0.4s. Readable haunch-load. Signature. |
| Pounce lunge | [R] | Generic leap retargets fine |
| Snap combo (2 swipes) | [R] | Second swipe timing stretched in code, not re-animated |
| Hit react / stagger | [R] | One clip serves both, speed-scaled |
| Death | [R] | Ragdoll blend acceptable |

### 4.2 CERASHORN — 8 clips
| Clip | Tag | Notes |
|---|---|---|
| Idle | [R] | |
| Walk | [R] | |
| Ground-paw (telegraph) | **[A]** | 0.8s. THE signature clip of the biome — dust VFX layered on |
| Charge loop | [R] | |
| Wall-slam + dazed loop | [R] | Impact frame gets a code-driven camera shake, not animation |
| Frill shove | [R] | Any headbutt/toss retargets |
| Hit react / stagger | [R] | |
| Death | [R] | |

### 4.3 SAILSPIT — 9 clips
| Clip | Tag | Notes |
|---|---|---|
| Idle | [R] | |
| Walk | [R] | |
| Backpedal | [R] | Reverse-play walk is acceptable v1 |
| Throat-glow gulp (telegraph) | **[A]** | 0.6s head-rear + throat bulge; amber glow is material state [C] |
| Glob spit | [R] | Short head-snap; projectile is VFX |
| Sail rattle (telegraph) | **[A]** | 0.7s. Sail-chain shake; amber flash is [C] |
| Tail fan whip | [R] | Spines are spawned projectiles [C] |
| Hit react / stagger | [R] | |
| Death | [R] | |

### 4.4 SCRAPFEATHERS — 4 clips
| Clip | Tag | Notes |
|---|---|---|
| Hop-idle | [R] | |
| Scurry | [R] | |
| Nibble peck | [R] | |
| Death pop | [C] | Particle burst + mesh despawn. No death animation. |

Flocking, lane-blocking, contact chip: all [C].

### 4.5 AMBERSHELL — 9 clips
| Clip | Tag | Notes |
|---|---|---|
| Idle | [R] | Amber glow pulse is material state [C] |
| Slow walk | [R] | |
| Tail sweep wind-up (telegraph) | **[A]** | 1.0s. Big, slow, honest. Signature. |
| Tail sweep | [R] | |
| Roll tuck + roll loop | [R] | |
| Wall-crack dazed | [R] | Plate cracks are material/mesh state swap [C], not animation |
| Compression stomp | [R] | Self-slam; AoE ring is VFX |
| Hit react (soft zone) | [R] | Armored zones play no react — that *silence* is feedback [C] |
| Death | [R] | Plate shatter is particle FX [C] |

### 4.6 TWICE-STRUCK — 8 clips (+0 for the ghost)
| Clip | Tag | Notes |
|---|---|---|
| Idle | [R] | |
| Run | [R] | |
| Horn rush | [R] | |
| Skull hook (telegraph + hit) | **[A]** | Rising headbutt arc; the one bespoke attack |
| Double stamp | [R] | AoE ring is VFX |
| Hit react | [R] | |
| Stagger | [R] | Staggering despawns pending echo [C] |
| Death | [R] | Ghost dissolves via shader [C] |

**The echo costs zero clips:** duplicate mesh, same animator, playback delayed 0.5s, echo shader. Entirely [C]. This is why this elite exists.

### 4.7 THE TYRANT — 11 clips
| Clip | Tag | Notes |
|---|---|---|
| Idle (breathing menace) | [R] | |
| Walk / turn-in-place | [R] | Turn can be procedural root rotation + walk blend |
| Lunge bite (telegraph + bite) | **[A]** | Fast telegraph is the fight's core read — author it |
| Tail arc | [R] | 270° sweep; big T-rex packs include tail attacks |
| Arena charge loop | [R] | |
| Junk-ring slam + stagger loop | [R] | Scripted stagger window |
| Roar A — phase transition | [R] | Every T-rex pack ships a roar |
| Roar B — junk-rain | [R] | Same roar, different head angle acceptable; falling junk is [C] (materialize shimmer + telegraph circles + prop drops) |
| Flinch (plate-crack moments) | [R] | |
| Death collapse | **[A]** or [R] | Author only if pack deaths look cheap — this is the biome's climax frame |
| — Phase 3 stutter | [C] | **No new clips.** Runtime frame-skip / playback freeze + position warp on the existing moveset. The corruption is code. |

Debris growth per phase: attachment prop swaps [C]. Seed-junk piece: prop attachment from seed [C].

---

## 5. Authored-clip budget check

Total **[A]** clips: **8** (Swiftjaw pounce-crouch, Cerashorn ground-paw, Sailspit gulp + sail rattle, Ambershell tail wind-up, Twice-Struck skull hook, Tyrant lunge bite, Tyrant death-optional). Well under the 15-clip ceiling. Every one of them is a telegraph — the animation budget is spent exactly where the player's eyes are.

## 6. Shared VFX list (supports all of the above)

- Telegraph flash (white/red) — universal attack-imminent shader pulse
- Amber material state (glow pulse, harden, crack, shatter particles)
- Stall zone decal + slow-tick particles
- Echo shader (cyan-white translucent, Twice-Struck + future biomes)
- Split Second hit-spark + time-ripple (player-side, listed for completeness)
- Junk materialize shimmer + ground impact circle + dust burst
- Death dissolve (standard) / death pop (swarm)

## 7. Build order

Mirrors ENEMIES_BIOME1.md §7: capsules first for the entire roster, then meshes in order Swiftjaw → Sailspit → Cerashorn → Scrapfeathers → Ambershell → Twice-Struck → Tyrant. Retarget passes can batch (one pack-import session covers most [R] clips); [A] clips are authored last, once capsule-phase timings are locked — never animate a telegraph whose duration might still change.
