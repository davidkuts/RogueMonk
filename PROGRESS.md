# PROGRESS.md — living project state

> **Claude Code: read this at the start of every session. Update it before ending every session or completing any milestone/sub-task.** Keep entries terse — this file is context, not a diary. When a milestone is done, collapse its sub-tasks into one line.

## Current status
- **Active milestone:** M2 complete pending a final look — M3 (combat data system) is next
- **Next action:** Human: run `Builds/Win64/RogueMonk.exe` for a last look at the pulsing pips, then give the go-ahead for M3 (AttackDefinition SOs, hit resolver + modifier pipeline, hitstop, screenshake, combo/cancel windows, input buffer).
- **Blocked on:** human go-ahead for M3

## Milestones
| # | Milestone | Status |
|---|---|---|
| 0 | Repo, packages, asmdefs, LFS, MCP bridge, gray-box room, Cinemachine rig | ✅ done |
| 1 | CharacterController movement + wall slide + camera follow (capsule) | ✅ done (pending feel-check) |
| 2 | Dash: travel curve, i-frames, charges, perfect-dodge refund | ✅ done (pending feel-check) |
| 3 | Combat data system: AttackDefinition SOs, hit resolver + modifier pipeline, hitstop, screenshake, combo + cancel windows, input buffer, EditMode tests | ⬜ |
| 4 | Enemy base, health/poise/stagger tiers, melee enemy w/ telegraphed lunge | ⬜ |
| 5 | Ranged enemy + projectile + telegraph | ⬜ |
| 6 | Room manager: templates, seeded selection, wave spawner, door gating, clear condition, confiner | ⬜ |
| 7 | Pause menu, restart, HUD, death screen + stats | ⬜ |
| 8 | Mixamo models + Animancer playback + toon shader pass | ⬜ |
| 9 | SFX, VFX, rumble, polish | ⬜ |

Status legend: ⬜ not started · 🔨 in progress · ✅ done · ⏸️ parked

## Session log
<!-- Newest first. One entry per session. Format:
### YYYY-MM-DD — short title
- Done: ...
- Decisions made: ... (anything not already in DESIGN.md)
- Known issues / TODO next: ...
-->

### 2026-08-06 — M2b: sequential recharge, dash pips, ✕ rebind
- Human verdict on M2: dash distance, smoothness and direction all "perfect". Three changes requested.
- **Recharge is now sequential.** M2 gave each spent charge its own parallel timer, so spending both back-to-back returned both together — no cost to burning them. Now a single timer refills one charge at a time; the second waits its full turn. Recharge dropped 2.5 s → **1.5 s** so a single dash recovers faster while a double dash costs 3.0 s total. **This changes a locked DESIGN.md decision — DESIGN.md § Movement & dash was updated to match.** A perfect-dodge refund keeps any accumulated progress on the charge still refilling rather than discarding it.
- **Dash pips added** (partial M7, pulled forward by request): `Game.UI.DashPipsView` drives two filled uGUI Images from `DashCharges.GetChargeFill(i)` — the sim decides which pip is refilling, so the display cannot disagree with it. Canvas is Screen Space Overlay, scaled to a 1920×1080 reference, pips bottom-left in reserved-saturated cyan. `Game.UI.asmdef` now references `UnityEngine.UI`. No new packages (com.unity.ugui was already present).
- **Dash rebound** from Circle (buttonEast) to **✕ (buttonSouth)** on the DualSense; keyboard Space / right mouse unchanged.
- Verified: 89/89 EditMode tests. Live in Play Mode: dash action resolves to `/DualSenseGamepadHID/buttonSouth`; with both charges spent, pip0 read 0.50 at half a period while pip1 stayed empty, then pip0 full / pip1 0.01 after one period, then both full after two — sequential recharge confirmed through the view, not just the sim.
- Follow-up same day: pips now **pulse** — a sine on `Time.unscaledTime` (so the HUD keeps breathing through hitstop and pause) scales pip brightness. Ready pips pulse deeply (depth 0.35, brightness 0.553–0.850 over a 1.11 s period); the refilling pip pulses subtly (depth 0.15) on a dimmer base colour with lower alpha, so "ready" still reads first. Rate and depth are serialized fields on the view — deliberately not a ScriptableObject, since they are view dressing rather than gameplay tuning and the human plans to redesign this element.
- Known issues / TODO next: pip size (102×16 at 1920×1080 reference) is a guess — easy to resize on the `DashPips` group. Still no HP bar; that stays M7. Human has flagged the whole pip element for a future redesign, so don't invest further in it.

### 2026-08-06 — M2: dash
- Human verdict on M1/M1b: camera and movement "perfect", keyboard and DualSense both good. M1 closed.
- Done: Simulation (engine-free, Game.Core): `PlayerDash` (fixed-direction burst along a travel curve, i-frames on the leading fraction, one-refund-per-dash perfect dodge with a `PerfectDodged` event), `DashCharges` (per-charge independent recharge timers, refund returns the newest spend), `InputBuffer` (generic press buffer in `Game.Core.Input`, window supplied by the caller — M3 reuses it for attacks). `IDashSettings` + `DashSettings.asset` hold every number, including the travel curve as an `AnimationCurve`. `PlayerMotor` now arbitrates walk vs dash and hands momentum back on dash exit; `PlayerInputReader` exposes the Dash edge; `PlayerLocomotion.SetVelocity` added for the handoff.
- Verified: 81/81 EditMode tests pass. Play Mode integration driven through the bridge: dash covered 4.14 m along facing (4.00 dash + 0.14 of exit momentum), ran 9 frames at dt=0.02 = 0.18 s, i-frames on 7 of those 9 (85 % window sampled at 60 Hz), charge spent then recharging; dashing at a wall from 2 m away stopped at x=9.58 with no tunnelling; perfect dodge refunded 1→2 and rejected the second attempt; dash-during-dash rejected.
- Decisions made: dash direction is the stick, or facing when the stick is neutral; no steering mid-dash. Perfect dodge refunds **once per dash** so a multi-hit attack cannot farm charges (DESIGN.md doesn't specify — revisit if it feels stingy). `PlayerDash.Tick` owns charge recharge whether or not a dash is live, so the adapter can't double-tick it. Exit speed is a fraction of walking top speed (default 1.0) and is deliberately not clamped, leaving room for an over-speed exit. The 150 ms buffer window lives in `DashSettings.bufferSeconds` for now; M3 may promote it to a shared input-settings asset when attacks need one.
- Testing note: driving synthetic input from `execute_code` only works while the **editor window has focus** — unfocused, the player loop freezes (observed stuck at frame 2) and the Input System soft-resets injected devices. Workaround used: invoke `PlayerMotor.Update` directly for a fixed number of frames, which still exercises the real CharacterController. The input edge itself was verified separately (`WasPressedThisFrame` → reader → true).
- Known issues / TODO next: `PlayerSettings.preloadedAssets` lost its `MonkControls` entry during the session and was restored by hand — if it vanishes again, that's the Input System revalidating project-wide actions; harmless because `PlayerInputReader` holds a direct serialized reference. No HUD pips yet (M7). Perfect dodge has no SFX/flash yet (M9) — only the event.

### 2026-08-06 — M1b: camera comfort pass + standalone playtest loop
- Human playtest verdict on M1: speed good, movement fluid, collision good; **camera made them dizzy on direction changes**.
- Root cause: the look-ahead offset was driven by *facing*. Reversing direction spins the capsule at 1080°/s, so the offset swept a ~1.25 m lateral arc through the perpendicular axis before settling on the far side — a fast sideways camera swing on every turnaround.
- Fix: `LookAheadTracker` now takes **velocity** (as a fraction of max speed) instead of facing+speed. Velocity passes through zero on a reversal, so the offset retracts along a straight line and never introduces a lateral component. Locked in by `LookAheadTrackerTests.DirectionReversal_NeverSwingsSideways`. Also softened: look-ahead 1.25→0.8 m, smooth time 0.35→0.55 s, Cinemachine damping 0.35→0.5.
- Playtest strategy (agreed): **playtest from a standalone build, never the editor.** `Builds/Win64/RogueMonk.exe` (development build, windowed 1600×900, `Builds/` gitignored). Claude rebuilds it via the MCP bridge after each change; the human runs the exe so editor overhead never contaminates a feel judgement. Editor Play Mode stays for Claude's own automated verification only.
- Cleanup: deleted the unused Unity template `Assets/InputSystem_Actions.inputactions`. It was wired as the Input System's project-wide actions asset (`EditorBuildSettings` config object `com.unity.input.settings.actions`), so that pointer was repointed at `MonkControls.inputactions` first.
- Known issues / TODO next: no in-game frame-time readout yet — if a build ever feels laggy we're still guessing. Cheap debug overlay is a candidate for M7 (HUD). `MonkControls` has no UI action map; add one when the pause menu/EventSystem lands in M7.

### 2026-08-06 — M1: movement + camera follow
- Done: Simulation layer (Game.Core, engine-free): `InputCurve` (radial deadzone + response exponent), `PlayerLocomotion` (accel/decel to a target velocity, rate-limited facing turn, planar-only), `LookAheadTracker` (damped camera lead scaled by speed), `ILocomotionSettings`/`ICameraLookAheadSettings`. Adapters (Game.Core.Player): `PlayerInputReader`, `PlayerMotor` (CharacterController.Move), `CameraFollowTarget`. `Assets/Settings/Data/PlayerMovementSettings.asset` holds every number. New input asset `Assets/Settings/Input/MonkControls.inputactions` (Keyboard&Mouse + Gamepad schemes; Move/Aim/Attack/Dash/Pause declared, only Move consumed in M1). Scene: Player (CharacterController h1.8 r0.4, tag Player) + Visual capsule + CameraTarget child; CM_GameplayCamera got a CinemachineFollow (WorldSpace binding, offset 0/12/-10, position damping 0.35) tracking CameraTarget. Game.Core.asmdef now references Unity.InputSystem.
- Verified: 32/32 EditMode tests pass (Game.Core.Tests); console clean; Play Mode driven with a synthetic gamepad through the MCP bridge — capsule accelerated to exactly 6 m/s, facing turned to the input direction, stopped hard against the +X wall at x≈9.58, and slid along +Z when pushed diagonally into it (sim velocity stayed the full diagonal; the controller resolved the slide). Camera target led the player by exactly the 1.25 m look-ahead and the Cinemachine rig followed.
- Decisions made: input maps straight to world XZ (no camera-relative transform) because the camera yaw is locked at 0. Gravity is adapter-only (a constant downward stick speed while grounded) — the sim stays planar so dash/knockback later compose cleanly. Look-ahead lives on a separate CameraTarget child so Cinemachine damping and look-ahead damping tune independently. Settings SO implements the sim's tuning interfaces so EditMode tests never touch assets.
- Known issues / TODO next: starting values are guesses (maxSpeed 6, accel 70, decel 90, turn 1080°/s, deadzone 0.15, response 1.4, lookahead 1.25 m / 0.35 s smooth, camera damping 0.35) — all need a human feel pass. The unused Unity template asset `Assets/InputSystem_Actions.inputactions` is still in the project; delete once nothing references it. Camera framing note: the room is 20×20 but the rig only frames ~11×5 m, so the north/south walls are off-screen and near-edge geometry clips at the bottom of frame — expected for a follow camera at 50°/FOV 32.5; revisit only if it reads badly with enemies present.

### 2026-08-06 — M0: project setup + feedback loop
- Done: .gitignore/.gitattributes with LFS (verified: URP.png stored as LFS object) + initial commit. Cinemachine 3.1.7 installed (Input System 1.20.0, AI Navigation 2.0.14, Test Framework 1.6.0 were already present). 10 asmdefs (Game.Core/Combat/Enemies/Level/UI + .Tests) under Assets/Scripts/<Module>[/Tests], compile clean in-editor AND via batchmode. Gray-box scene Assets/Scenes/GrayboxArena.unity: 20×20 floor, 4 walls, 4 obstacle blocks, Main Camera + CinemachineBrain, CM_GameplayCamera vcam (pitch 50°, yaw 0, FOV 32.5, pos 0/12/-10). Enter Play Mode Options on with Domain+Scene Reload disabled; Force Text + Visible Meta Files confirmed. Smoke test Game.Core.Tests.TestPipelineSmokeTests passes (run via bridge). Batchmode fallback command verified on a project copy and recorded in CLAUDE.md.
- Decisions made: MCP for Unity ships with HTTP transport default + auto-start off → no bridge ever listened. Fix: Assets/Editor/McpBridgeBootstrap.cs ([InitializeOnLoad]) forces stdio transport, starts the bridge (port 6400), and enforces Enter Play Mode options. Claude Code registration: `UnityMCP` stdio server in local scope. Test asmdefs use overrideReferences + nunit.framework.dll + UNITY_INCLUDE_TESTS; runtime asms expose internals to their .Tests via AssemblyInfo.cs.
- Known issues / TODO next: native UnityMCP tools appear in Claude Code only after a session restart (this session drove the bridge via a scratchpad stdio client). Follow-up same day: deleted TutorialInfo/SampleScene/Readme, removed 6 unused packages (ai.assistant, ai.inference, visualscripting, timeline, multiplayer.center, collab-proxy), build settings → GrayboxArena; console clean, compile green after resolve. Active Input Handling already "Input System Package" only.

## Tuning values changed from DESIGN.md defaults
<!-- Record here whenever a starting number from DESIGN.md gets re-tuned, so DESIGN.md stays the design intent and this stays the current truth. e.g. "dash distance 4m → 4.5m (felt short in large rooms)" -->
- Camera look-ahead 1.25 m → **0.8 m**, smooth time 0.35 s → **0.55 s**, Cinemachine position damping 0.35 → **0.5** (playtest 2026-08-06: direction changes were making the human dizzy). DESIGN.md's "~1–1.5 m look-ahead" is now the upper bound, not the target.
- Human-confirmed good as of 2026-08-06: max speed 6 m/s, accel 70, decel 90, wall collision/slide, camera look-ahead 0.8 m / 0.55 s / damping 0.5, and both KB&M + DualSense input.
- Human-confirmed good 2026-08-06: dash 4 m / 0.18 s / front-loaded travel curve (tangents 2.2 → 0.15) / direction handling / exit speed 1.0× walk.
- Dash recharge **2.5 s parallel → 1.5 s sequential** (playtest: parallel timers returned both charges at once). i-frames 85 %, 2 charges, 0.15 s buffer unchanged and not yet feel-tested — no enemies to dodge until M4.

## Open questions for the human
- Should a multi-hit attack be able to refund more than one charge per dash? Currently capped at one. (Unanswerable until enemies exist in M4 — carry it forward.)
- (resolved) Pip pulse — added 2026-08-06. Human confirmed the whole element is slated for a redesign later, so keep effort here minimal.
- (resolved) Sequential recharge at 1.5 s per charge — human confirmed good on 2026-08-06.
- (resolved) M2 dash feel — distance, smoothness, direction confirmed good on 2026-08-06.
- (resolved) M1 feel — camera and movement confirmed good on 2026-08-06.
- (resolved) 2026-08-06 cleanup questions: human delegated the call; template assets and 6 unused packages removed, McpBridgeBootstrap kept permanently as self-healing infra, build settings now list GrayboxArena only.
