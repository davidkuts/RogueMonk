# PROGRESS.md — living project state

> **Claude Code: read this at the start of every session. Update it before ending every session or completing any milestone/sub-task.** Keep entries terse — this file is context, not a diary. When a milestone is done, collapse its sub-tasks into one line.

## Current status
- **Active milestone:** M0 done — awaiting human check before M1
- **Next action:** Human: restart the Claude Code session (loads the native UnityMCP tools), open Assets/Scenes/GrayboxArena.unity, enter Play Mode, confirm camera framing. Then start M1 (CharacterController movement + camera follow).
- **Blocked on:** human playtest of the gray-box scene

## Milestones
| # | Milestone | Status |
|---|---|---|
| 0 | Repo, packages, asmdefs, LFS, MCP bridge, gray-box room, Cinemachine rig | ✅ done |
| 1 | CharacterController movement + wall slide + camera follow (capsule) | ⬜ |
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

### 2026-08-06 — M0: project setup + feedback loop
- Done: .gitignore/.gitattributes with LFS (verified: URP.png stored as LFS object) + initial commit. Cinemachine 3.1.7 installed (Input System 1.20.0, AI Navigation 2.0.14, Test Framework 1.6.0 were already present). 10 asmdefs (Game.Core/Combat/Enemies/Level/UI + .Tests) under Assets/Scripts/<Module>[/Tests], compile clean in-editor AND via batchmode. Gray-box scene Assets/Scenes/GrayboxArena.unity: 20×20 floor, 4 walls, 4 obstacle blocks, Main Camera + CinemachineBrain, CM_GameplayCamera vcam (pitch 50°, yaw 0, FOV 32.5, pos 0/12/-10). Enter Play Mode Options on with Domain+Scene Reload disabled; Force Text + Visible Meta Files confirmed. Smoke test Game.Core.Tests.TestPipelineSmokeTests passes (run via bridge). Batchmode fallback command verified on a project copy and recorded in CLAUDE.md.
- Decisions made: MCP for Unity ships with HTTP transport default + auto-start off → no bridge ever listened. Fix: Assets/Editor/McpBridgeBootstrap.cs ([InitializeOnLoad]) forces stdio transport, starts the bridge (port 6400), and enforces Enter Play Mode options. Claude Code registration: `UnityMCP` stdio server in local scope. Test asmdefs use overrideReferences + nunit.framework.dll + UNITY_INCLUDE_TESTS; runtime asms expose internals to their .Tests via AssemblyInfo.cs.
- Known issues / TODO next: native UnityMCP tools appear in Claude Code only after a session restart (this session drove the bridge via a scratchpad stdio client). Template leftovers kept for now: Assets/TutorialInfo, SampleScene, and unused packages (ai.assistant, ai.inference, visualscripting, timeline, multiplayer.center, collab-proxy) — see open questions.

## Tuning values changed from DESIGN.md defaults
<!-- Record here whenever a starting number from DESIGN.md gets re-tuned, so DESIGN.md stays the design intent and this stays the current truth. e.g. "dash distance 4m → 4.5m (felt short in large rooms)" -->
(none yet)

## Open questions for the human
- OK to delete template leftovers (Assets/TutorialInfo, Assets/Scenes/SampleScene.unity, Readme.asset) and remove unused packages (com.unity.ai.assistant, com.unity.ai.inference, com.unity.visualscripting, com.unity.timeline, com.unity.multiplayer.center, com.unity.collab-proxy)? Suggested, not done — CLAUDE.md says ask first.
- Keep Assets/Editor/McpBridgeBootstrap.cs permanently? It re-asserts stdio transport + Enter Play Mode options on every reload; harmless, but say the word if you'd rather configure once and delete it.
