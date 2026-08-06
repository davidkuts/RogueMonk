# CLAUDE.md — Monk Roguelike (Unity 6)

Read DESIGN.md before any gameplay work. It is the source of truth for all locked design decisions.

## Session protocol (mandatory)
- **Session start:** read PROGRESS.md first. It tells you the active milestone, the next action, and recent session history. Do not re-plan work that is already marked done.
- **Session end / milestone or sub-task complete:** update PROGRESS.md — status table, a terse session-log entry (done / decisions / TODO next), any tuning values that now differ from DESIGN.md defaults, and any open questions for the human. Then commit it with the related code.
- If the human's request conflicts with PROGRESS.md state, trust the human and correct PROGRESS.md.
- Work on ONE milestone at a time. Finish, update PROGRESS.md, stop, and let the human playtest before moving on — never chain ahead into the next milestone unprompted.

## Project facts
- Unity 6.3 LTS, URP, C#, new Input System, Cinemachine 3, AI Navigation, Unity Test Framework
- **Editor feedback loop:** the Coplay unity-mcp bridge is installed — prefer it to read the Unity console, inspect scenes, create/modify GameObjects, and run tests, instead of asking the human to copy-paste. It is registered in Claude Code (local scope) as `UnityMCP` (stdio, `uvx --from "mcpforunityserver>=0.0.0a0" mcp-for-unity`); `Assets/Editor/McpBridgeBootstrap.cs` forces the editor-side stdio bridge up (port 6400, status file in `~/.unity-mcp/`) after every domain reload. Never declare a task done on unverified code.
- **Batchmode fallback** (verified 2026-08-06; works ONLY while the Unity editor is closed — the project lock rejects a second instance): editor exe is `C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Unity.exe`. Compile check: `Start-Process -Wait -PassThru "C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Unity.exe" -ArgumentList '-batchmode','-nographics','-quit','-projectPath','C:\RogueMonk\RogueMonk','-logFile','Logs\batch.log'` then check `$_.ExitCode` is 0 and grep the log for `error CS` (plain `& Unity.exe` does NOT block — it is a GUI-subsystem exe). Tests: same but `-runTests -testPlatform EditMode -testResults <abs path>` and NO `-quit`. Last resort: read `%LOCALAPPDATA%\Unity\Editor\Editor.log`.
- Assembly definitions: Game.Core, Game.Combat, Game.Enemies, Game.Level, Game.UI, plus matching .Tests assemblies

## Hard rules
1. **Simulation is engine-free.** Combat resolution, frame-timing state machines, poise/stagger, room graph generation, and RNG live in plain C# classes with no UnityEngine dependency beyond math structs. MonoBehaviours are thin adapters that forward Update/Time.deltaTime into the simulation.
2. **All tuning values live in ScriptableObjects** (AttackDefinition, EnemyDefinition, DashSettings, etc.). Never hardcode a timing, distance, damage, or cooldown in code. If a magic number appears in a .cs file, move it to data.
3. **Root motion is OFF everywhere.** All movement is code-driven. Animations are visuals only.
4. **Movement uses the built-in CharacterController**, not Rigidbody forces. Knockback is a manually integrated velocity impulse.
5. **All randomness draws from the RunContext RNG** (seeded). Never call UnityEngine.Random or new System.Random() directly in gameplay code.
6. **Windup is never cancellable; recovery is dash-cancellable** and cancelling costs a dash charge. Do not change this without an explicit instruction.
7. Saturated hues are reserved for gameplay information (telegraphs, projectiles, dash trail, elemental FX). Environment art stays within the muted 6-color palette.

## Workflow
- After editing scripts: verify compilation (batchmode CLI, or MCP console read if a bridge is installed) before declaring the task done. Never declare done on unverified code.
- Write or update EditMode tests for any simulation-layer change, and run them before declaring done. Feel/tuning is verified by the human in Play Mode; correctness is verified by tests.
- New attacks/enemies = new ScriptableObject assets + minimal code. Prefer data over branches.
- Seeded soak tests: room generation changes must keep the "N seeds all produce solvable levels" test passing.
- Keep diffs small and focused. Do not reorganize folders or rename public APIs without being asked.

## Don'ts
- Don't add Animator Controller state machines; animation playback is code-driven (Animancer / Playables).
- Don't add healing mechanics, extra dash charges, or telegraph-free attacks — these are locked design decisions.
- Don't bake NavMesh at runtime; NavMesh is baked per room prefab.
- Don't introduce packages without asking.
