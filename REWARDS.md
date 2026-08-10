# REWARDS.md — Run Economy & Non-Boon Rewards

> Status: DESIGN LOCKED (Borrowed Minutes flagged experimental; see §9)
> Companion doc to BOONS.md. Core principle: **the entire run economy is denominated in time.** Every reward, currency, and shop interaction routes through the Second Hand — one visual/fictional system, multiple denominations.

---

## 1. Currency Denominations

| Currency | Scope | Fiction | Source | Spent on |
|---|---|---|---|---|
| **Seconds** | Boon-bound | Loose time fragments shed by kills, drawn into the Second Hand | Kills (auto-collect) | Materializing complications (boons) ONLY — never shop-spendable |
| **Minutes** | Run currency | *Bandwidth* — accumulated time the friends spend holding the transmission window open long enough to send matter | Room rewards, kills (small trickle), Strays/effects | Supply Drops, Borrowed Minutes repayment |
| **Hours** | Meta currency | Stabilized time that survives the loop reset | Rare room rewards, boss guarantees, milestone kills | Permanent hub unlocks |
| **Amber** | Premium meta | Time preserved solid | Bosses (the Tyrant sheds the most — diegetic: he's saturated with temporal debris) | Big permanent unlocks: Perpetual prereq reductions, hub facilities, (optionally) Thrown Second slot unlock |

Hard rule: **Seconds and Minutes never cross.** Boons never compete with purchases for the same resource.

All drop rates, trickle amounts, and prices = ScriptableObject playtest variables.

---

## 2. Reward Type Pool (per-room rewards)

Every room exit leads to exactly one reward type. Full pool:

| Type | Category | Effect |
|---|---|---|
| **Transmission** | Boon | 3-choice complication draft from one giver (see BOONS.md) |
| **Minutes cache** | Currency | Run currency payout |
| **Hours cache** | Currency | Meta currency payout (rarer) |
| **Splice** | Healing | Rewind Cole's body to a less-injured state (see §3) |
| **Stray** | Item | Passive trinket lost in the timestream (see §4) |
| **Stopgap** | Consumable | Single-use canned emergency time (see §5) |
| **Recalibration** | Upgrade | Upgrade one owned complication's rarity a step (see §6) |
| **Supply Drop** | Shop | Spend Minutes to fabricate items through the window (see §7) |

Boss rooms: guaranteed Hours + Amber + a high-tier reward from the normal pool.

---

## 3. Splice (Healing)

- The Second Hand rewinds Cole's body to a less-injured state. Wounds visually play backwards.
- **Soft cap rule:** a Splice cannot rewind past the start of the current biome. Healing is naturally capped per biome without a potion economy.
- Tiering: Normal/Rare/Epic Splice = larger rewind depth (numbers only, per project rarity rule).
- SO variables: rewind % per tier, biome-boundary reset behavior.

---

## 4. Strays (Passive Items)

A **Stray** is an object that fell out of its era and drifted in the timestream until the Second Hand snagged it.

- **Equip rule: one Stray equipped at a time.** Swapping is free at pickup; the replaced Stray is lost (keeps decisions weighty; revisit in playtest).
- Passive derives from what the object *was* — the design constraint that keeps them original and readable.
- **Double duty — biome foreshadowing:** Strays found in early biomes are artifacts of later biomes. The player holds pieces of Egypt/Greece/Medieval hours before arriving. Anachronisms can also point backwards (a dinosaur tooth found in Medieval).

### Launch set (Biome 1 findable, cross-biome origins)
| Stray | Origin era | Passive |
|---|---|---|
| Corroded Obol | Greece | Supply Drops cost −20% (a coin warps the window's economics) |
| Gauntlet Buckle | Medieval | One-hit shield after each successful Split Second |
| Linen Scrap | Egypt | Splices rewind +25% deeper |
| Cracked Hourglass | Present | +1 carried Stopgap slot |
| Displaced Tooth | Cretaceous (found elsewhere) | +15% damage vs enemies of the era it was displaced from |
| Signal Mirror | Unknown | Flux offerings appear +50% more often (pairs with the Flux mystery) |

Expansion rule: each new biome adds 3–5 Strays; at least half must originate from *other* biomes.

---

## 5. Stopgaps (Consumables)

Canned emergency time. Single-use, activated on a dedicated input (D-pad candidate).

- **Carry cap: 2** (SO variable). Panic buttons, not a hoardable resource.
- Launch set:
  - **Stored Rewind** — instant 2-second personal rewind (position + health)
  - **Pocket Freeze** — 1.5s stasis burst around Cole
  - **Wound Spring** — instant Vortex recharge
- Sources: Supply Drops (cheap), rare room rewards.

---

## 6. Recalibration

- One friend gets a strong enough connection to **retune an owned complication**, upgrading its rarity one step: Normal→Rare→Epic.
- Upgrades **tier only** — consistent with "rarity scales numbers, never mechanics." No leveling system, no XP.
- Cannot produce Perpetuals; Epics cannot be recalibrated further.
- The retuning friend = the giver who owns that complication (voice line hook).

---

## 7. Supply Drop (The Shopkeeperless Shop)

There is no merchant. A Supply Drop is a point where the connection is strong enough to open the transmission window for **matter**, not just data. Spending Minutes = paying to hold the window open; bigger items cost more window time.

- Visual: a crate materializing in a cone of light, assembled from fabrication pulses.
- Audio: the friends argue logistics over the channel. Rotating lines — Percy running inventory badly, Mara sneaking in something you didn't order, Frank always attaching food, Dr. Reeve disapproving of the manifest.
- Stock (per visit, drawn from): Stopgaps, one Stray, one Splice, occasionally a Transmission (single fixed complication at a premium — no 3-choice draft).
- Prices in Minutes; all SO variables.

---

## 8. Tier Parity & Door Previews

**Parity rule:** at every room-choice fork, the game rolls a *reward quality tier* first; every door then offers a different reward *type at that same tier*. The player chooses what KIND of help — never whether they're getting scammed. (Retained deliberately: parity is genre-good-UX, not any one game's signature.)

**Door preview presentation:** no floating icons over doors. As Cole approaches a fork, the Second Hand projects each exit's **incoming signal** — one waveform per door, in the same visual language as the boon-tuning UI:

| Signal shape | Reward |
|---|---|
| A giver's frequency signature | Transmission (readable WHICH giver, by waveform style) |
| Fabrication pulse | Supply Drop |
| Flatline stabilizing | Splice |
| Dense tick cluster | Minutes cache |
| Slow deep pulse | Hours cache |
| Foreign/anachronistic warble | Stray |
| Sharp single spike | Stopgap |
| Harmonic retune sweep | Recalibration |

Rarity tier of the fork reads as signal **strength/material color** (brass/silver/gold glow), consistent with complication rarity presentation.

Everything comes through the watch: boons, shop, healing, previews — one system, one prop.

---

## 9. Borrowed Minutes (EXPERIMENTAL — post-core)

- Each Supply Drop offers one item priced beyond your current Minutes, payable in **Borrowed Minutes**.
- Taking it puts Cole in time debt: for the next N rooms, the debt manifests as a mild persistent disadvantage (candidates: enemies slightly desynced in their favor; small drain on Seconds income) until repaid from earnings.
- A loan-shark mechanic with no loan shark — borrowing against your own timeline. No timer pressure added to moment-to-moment play.
- Status: **flagged experimental.** The economy stands without it; implement only after core loop validates. Ties thematically to Flux/instability if the Flux=June decision lands.

---

## 10. Implementation Notes (for future PROMPT_REWARDS.md)

- Reward roll order: tier roll → type assignment per door (no duplicate types per fork) → content roll within type.
- Currency wallets: Seconds (existing, boon pipeline), Minutes (run-scoped, resets on death), Hours + Amber (persistent save data).
- Stray = ScriptableObject: origin era, passive hook (reuses hit-modifier pipeline / status containers where possible), foreshadow flag.
- Stopgap activation reserves one input; confirm controller mapping against locked base kit before implementation.
- Door signal previews reuse the boon-tuning waveform renderer — build that component once, parameterized.
- Capsule-first: Supply Drop crate, Stray pickups, and door signals all ship as primitives/placeholder waveforms behind final interfaces.

---

## 11. Open Decisions

1. **Stray swap policy:** replaced Stray lost vs. banked for the rest of the run (current: lost — revisit in playtest).
2. **Stopgap input mapping:** which D-pad direction (or other input) activates the carried Stopgap.
3. **Amber-gated Thrown Second:** does the Thrown Second slot unlock via Amber at the hub, or is it story-unlocked? (Interacts with BOONS.md open decision #2.)
4. **Borrowed Minutes:** go/no-go after core economy playtest.
