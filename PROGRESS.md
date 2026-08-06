# PROGRESS.md — living project state

> **Claude Code: read this at the start of every session. Update it before ending every session or completing any milestone/sub-task.** Keep entries terse — this file is context, not a diary. When a milestone is done, collapse its sub-tasks into one line.

## Current status
- **Active milestone:** M1 built and verified — awaiting human playtest before M2
- **Next action:** Human: Play Mode in GrayboxArena, drive the capsule with WASD/left stick. Judge feel (top speed, accel/decel snap, turn rate, camera damping + look-ahead) and report tuning changes. Then M2 (dash).
- **Blocked on:** human feel-check of M1 movement

## Milestones
| # | Milestone | Status |
|---|---|---|
| 0 | Repo, packages, asmdefs, LFS, MCP bridge, gray-box room, Cinemachine rig | ✅ done |
| 1 | CharacterController movement + wall slide + camera follow (capsule) | ✅ done (pending feel-check) |
| 2 | Dash: travel curve, i-frames, charges, perfect-dodge refund | ⬜ |
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
(none yet)

## Open questions for the human
- M1 feel: is 6 m/s top speed right, and does the camera damping (0.35) + look-ahead (1.25 m) read well while strafing? Answer in a playtest note and the values move to the table above.
- (resolved) 2026-08-06 cleanup questions: human delegated the call; template assets and 6 unused packages removed, McpBridgeBootstrap kept permanently as self-healing infra, build settings now list GrayboxArena only.
