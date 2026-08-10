# BOONS.md — Transmission System (Boons / Power-Ups)

> Status: DESIGN LOCKED (pending two open decisions at bottom)
> Fiction: boons are **patches transmitted to the Second Hand** by the people trying to reach Cole across time. No gods, no nature spirits — five humans in a ruined lab, and one signal nobody can explain.
> Elemental architecture stubs (fire/ice/wind/earth/nature/force) map 1:1 onto the six givers. Stub names stay in code; giver names are presentation-layer.

---

## 1. Core Fiction

The Second Hand is June's backup unit and is still paired to the lab network. Each jump briefly touches the present; the surviving team pushes data through the handshake window. They cannot pull Cole back — they can only patch his hardware. Every boon is proof that someone is still fighting for him. This directly opposes the Custodian's thesis ("let her go") and makes the boon system a characterization delivery channel: every giver gets offer/pickup/legendary voice lines.

---

## 2. The Six Givers

| Giver (channel) | Element stub | Person | Design space | Voice identity |
|---|---|---|---|---|
| **Overclock** | Fire | **Mara** — Cole's training partner, competitive fighter | Raw damage, attack speed, bigger payloads. Pure damage baseline; her boons carry NO riders, ever | Zero theory, pure conviction. "It made the numbers bigger. Take it." |
| **Fray** | Nature | **Dr. Eleanor Reeve** — June's senior colleague, materials science | Decay. Signature status **Fray** (timeline unraveling DoT) + armor decay | Clipped, formal, never apologizes. Carries the most guilt (signed the safety review). Hard-exposition mouthpiece |
| **Stasis** | Ice | **Percy** — June's grad student / assistant | Slows, roots, high-tier brief full-stops on staggerable enemies. Control instead of damage | Young, terrified, talks too fast — but his systems never miss. Containment protocols are the only thing he knows cold |
| **Echo** | Force | **Denny** — Cole's oldest friend, pre-June era | Things happen twice: delayed repeats, re-pulses, afterimages. Weak per-instance, compounding over a fight | Not technical; pattern-spotter; de facto mission-control voice. Remembers versions of Cole that no longer exist |
| **Ward** | Earth | **Frank** — Cole's old coach | Insurance: one-hit shields, extended blink i-frames, wider Split Second window, stored-damage healing. Zero direct damage | Oldest voice on the channel. Only one who talks about anything other than the mission. "Come home." |
| **Flux** | Wind | **Unknown waveform** (see Open Decisions) | High variance: chance-based effects, per-room rerolls, stronger-with-downside offers. Equal expected value, wild distribution | No voice — waveform only. Other givers argue on-channel about whether Cole should install these |

### Relationship map (for banter + Resonance foreshadowing)
- Mara ↔ Frank: shared gym history. Natural banter pair.
- Denny ↔ everyone: mission-control glue voice.
- Dr. Reeve ↔ Percy: authority/panic contrast — she radiates control and is drowning; he radiates panic and is the most reliable.
- Flux ↔ Dr. Reeve: she flatly declares the signal impossible ("compiled on hardware we haven't built").

---

## 3. Balance Rule — Power Budget

Every boon has a fixed power budget per tier. Utility is purchased against it.

- Pure damage boon = 100% of budget into damage (Overclock's exclusive lane).
- Every rider (slow, root, shield, DoT, armor shred, echo, reroll) costs **30–50% of the damage portion**.
- Worked example at equal tier, Square (combo) slot:
  - Overclock: **+40% damage**
  - Stasis: **+20% damage + slow on hit**
  - Fray: **+10% damage + Fray stacks on hit**
- **Rarity scales numbers only. Rarity NEVER adds mechanics.** A Rare is a bigger Normal; an Epic is a bigger Rare. Keeps tier comparisons legible and the balance sheet trivial.
- ALL numeric values below are ScriptableObject playtest variables. Numbers in this doc are starting points, not commitments.

### Rarity tiers
| Tier | Material read | Scalar (baseline) |
|---|---|---|
| Normal | Brass | 1.0× |
| Rare | Silver | 1.5× |
| Epic | Gold | 2.25× |
| **Perpetual** (Legendary) | Unique | Transforms, doesn't scale |

---

## 4. Boonable Slots

| Slot (ability ID) | Ability | Current default binding |
|---|---|---|
| ATK | Auto-attack combo | Square |
| BLINK | Time-blink dash (i-frames) | X |
| VORTEX | Spinning crane kick — AoE pull + supercharge feed | Circle |
| SPLIT | Split Second (perfect-block-gated riposte) | Triangle |
| CAST | Thrown Second | TBD |

> **Binding rule:** boons attach to ability IDs, NEVER to inputs. Input rebinding is a planned feature; nothing in the boon system may reference a button. Bindings above are current defaults only (deliberately Hades-parity for playtest comparison).

PASSIVE is an additional non-slot category (regen, fragments, shields) with no binding. CAST boonability: **see Open Decisions**.

Offer rule (Hades-style single-giver draft, different execution): a boon point offers **3 choices from ONE giver's pool**, random slots/rarities. Which giver contacts you at each point is drawn from the run's available channel pool.

---

## 5. Offering Tables (per giver, per slot)

Format: effect at Normal; Rare/Epic scale numbers by rarity scalar. One offering per giver per slot keeps pools tight for v1; expand later if drafts feel samey.

### Overclock — Mara (pure damage lane)
| Slot | Offering | Normal effect |
|---|---|---|
| ATK | Redline | +40% combo damage |
| BLINK | Hot Arrival | Blink detonates on arrival (blast = 60% of ATK) |
| VORTEX | Overdriven Coil | +50% vortex payload damage |
| SPLIT | Haymaker Protocol | Riposte +60% damage |
| CAST | Burnout Round | Thrown Second +50% impact damage |
| PASSIVE | Spec Violation | +15% global damage |

### Fray — Dr. Reeve (decay lane)
| Slot | Offering | Normal effect |
|---|---|---|
| ATK | Loose Threads | Hits apply Fray stacks (DoT, stacking) |
| BLINK | Wake of Rust | Blink trail applies Fray to enemies crossed |
| VORTEX | Rot Well | Enemies in vortex gain Fray per tick |
| SPLIT | Deep Fracture | Riposte applies heavy Fray burst + **armor decay** |
| CAST | Corrosive Second | Embedded projectile pulses Fray in small radius |
| PASSIVE | Entropy Field | Frayed enemies take +X% armor-break damage |

> **Resolves open project question:** bonus armor damage vs amber-tier enemies lives HERE (Fray's armor-decay identity), NOT on the base Split Second/Riposte. Base riposte stays armor-neutral.

### Stasis — Percy (control lane)
| Slot | Offering | Normal effect |
|---|---|---|
| ATK | Drag Coefficient | +20% damage, hits slow 25% for 2s |
| BLINK | Cold Departure | Enemies near blink origin rooted 1s |
| VORTEX | Containment Spin | Vortex duration +50%; staggerables held longer |
| SPLIT | Hard Lock | Riposte roots target 2s (staggerable: brief full-stop) |
| CAST | Anchor Round | Embedded projectile emits slow aura until return |
| PASSIVE | Protocol Nine | Slowed/rooted enemies deal −20% damage |

### Echo — Denny (repetition lane)
| Slot | Offering | Normal effect |
|---|---|---|
| ATK | Second Take | Combo finisher repeats at 40% power after 0.8s |
| BLINK | Afterimage | Blink leaves image that copies your next hit at 50% |
| VORTEX | Encore Pulse | Vortex re-pulses once at 50% strength |
| SPLIT | Reprise | 3s after riposte, it re-triggers on nearest enemy at 60% |
| CAST | Round Trip Twice | Thrown Second's return pass hits again at 70% |
| PASSIVE | Standing Wave | Every 4th instance of any damage repeats at 30% |

### Ward — Frank (survival lane, zero direct damage)
| Slot | Offering | Normal effect |
|---|---|---|
| ATK | Guard High | Every 10th combo hit grants a one-hit shield |
| BLINK | Slip the Punch | Blink i-frames +40% duration |
| VORTEX | Corner Work | Taking damage while vortex active: 30% stored, refunded as healing after |
| SPLIT | Read the Room | Split Second timing window +35% |
| CAST | Covering Throw | While Thrown Second is out, −15% damage taken |
| PASSIVE | Stay Standing | One-hit shield regenerates every 30s |

### Flux — Unknown (variance lane)
| Slot | Offering | Normal effect |
|---|---|---|
| ATK | Noise Floor | Hits: 15% chance to crit for 3× |
| BLINK | Skip Frame | Blink 25% chance to reset its own cooldown |
| VORTEX | Interference | Vortex randomly rolls one bonus per cast (damage / duration / radius) |
| SPLIT | Dropped Packet | Riposte: 50% chance double damage, 50% chance normal |
| CAST | Unstable Payload | Thrown Second +80% damage, 20% chance it detonates early |
| PASSIVE | Static | Reroll one random owned boon's numbers each room (never below Normal) |

Flux design note: EV parity with other givers; distribution is the identity. Downside offers must telegraph the downside on the choice UI.

---

## 6. Perpetuals (Legendaries)

One per giver. **Transforms, never scales.** Prerequisite: own 2–3 boons from that giver (playtest variable) — built toward, not lucked into.

| Giver | Perpetual | Effect |
|---|---|---|
| Mara | **Past Spec** | All rider-free damage bonuses you own gain +50% effect; combo finisher becomes an armor-ignoring blow |
| Dr. Reeve | **Total Entropy** | Fray stacks no longer cap; at 20+ stacks the target's armor tier degrades one step |
| Percy | **Full Containment** | Vortex becomes a stasis sphere: staggerables fully frozen inside; on collapse, stored damage releases as burst |
| Denny | **Second Verse** | Split Second riposte happens twice — second instance free-aims at nearest enemy at full power |
| Frank | **One More Second** | On death: the loop stutters, rewinds Cole 3s with a sliver of health. Once per run. Clean, safe, no cost |
| Flux | **Borrowed Time** *(if Flux = June)* | On death: the seal visibly cracks as the waveform spends part of itself to restart the fight. More health returned than Frank's, but a story-visible cost — the crack persists in the hub |

Frank vs Flux revive differentiation: his is safe and clean; hers is stronger but *costs something you can see*. Never let a player hold both live at once (owning one suppresses the other from offers).

---

## 7. Resonance (duo-equivalent)

Fiction: two friends' transmissions sit on nearby frequencies; carrying qualifying boons from both causes constructive interference. **Foreshadowed by audio:** their hub-adjacent chatter voice lines start overlapping/cross-talking once a Resonance enters your offer pool.

| Pair | Resonance | Effect |
|---|---|---|
| Percy + Dr. Reeve | **Cold Storage Still Rots** | Slowed/rooted/frozen enemies accumulate Fray at 2× rate |
| Mara + Denny | **Louder the Second Time** | All Echo repeats trigger at 100% power instead of reduced |
| Frank + Flux | **House Odds** | When a Ward shield breaks, it rerolls into a random 10s buff |
| Mara + Frank | **Old Gym Rules** | While shielded, +30% damage (their shared-history pair) |
| Percy + Denny | **Hold and Repeat** | Rooting an enemy triggers your last combo finisher on them at 50% |
| Dr. Reeve + Flux | **Impossible Chemistry** | Fray ticks have 10% chance to crit for 3× (she hates this one — voice line) |

Prereq structure: 1+ qualifying boon from each giver in the pair; Resonance then joins that pair's shared offer pool. v1 ships 6 pairs (above); full 15-pair matrix deferred.

---

## 8. Presentation — Complications, Not Cards

- During runs, kills shed **loose seconds** — glowing fragments that drift into the Second Hand (auto-collect, no pickup friction).
- At a boon point, the unit projects its watch face large in front of Cole. The three offers appear as **complications** — real horological term for extra mechanisms on a watch face.
- Each offer is a physical component hologram styled per giver:
  - Mara: hot brass, glowing
  - Dr. Reeve: oxidizing/patina surfaces
  - Percy: frosted silver
  - Denny: components that visibly tick twice
  - Frank: heavy, guarded casing
  - Flux: hands spinning erratically
- **Selection = tuning.** Rotate left stick to tune between frequencies; the giver's garbled voice line fades in as you hover their signal. Confirm to slot the complication; collected fragments visibly flow in and materialize the part.
- Build inspection: pause menu shows the full watch face with every installed complication, grouped by giver. Rarity reads as material (brass / silver / gold). Perpetuals get unique treatment.
- Rationale: delivers the MMO "equipping a real object" feel without armor slots — **your build IS the watch.**

---

## 9. Implementation Notes (for future PROMPT_BOONS.md)

- Element stubs (fire/ice/wind/earth/nature/force) remain the code-level channel IDs; giver identity is data.
- Boon = ScriptableObject: giver, slot, rarity, budget-derived values, rider list, prereq flags (Perpetual/Resonance).
- Offer generator: pick giver from run channel pool → sample 3 from giver pool (slot/rarity weighted) → inject Perpetual/Resonance when prereqs met.
- Rarity scalar applied at load — one number tunes an entire tier.
- Voice line hooks: on-offer, on-hover (tuning), on-pickup, on-Perpetual, Resonance cross-talk.
- Capsule-first applies: complication holograms ship as colored primitive placeholders behind the same presentation interface.

---

## 10. Open Decisions

1. **Flux identity — LEANING: June, woven into the seal.** Leak-through of the containment structure; reveal in final biome mid-fight vs future-Cole when the waveform resolves into her voice pattern for one frame. Reframes run history; deepens the Custodian (the seal he protects is partly made of her); respects the apparition-June constraint (hub echo names the wall; Flux is the other side of it). Fallback: Flux is the Custodian's own lost person — weaker gut-punch, but makes his backstory playable. **Decide before recording any Flux-adjacent lines** (recording order: Custodian first — his lines reference the seal's composition either way; write them ambiguous until this locks).
2. **CAST slot boonability:** include Thrown Second offerings from day one of the boon system, or exclude the slot until the ability itself is locked and implemented? (Tables above include CAST rows so nothing needs redesign either way — they can ship disabled.)
