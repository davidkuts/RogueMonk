using Game.Combat;
using Game.Core.Diagnostics;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Everything the Tyrant does that a boss moveset alone cannot express: amber hardening across
    /// its body mid-fight, and Phase 3's corrupted timing.
    ///
    /// <para>A sidecar rather than a subclass. <c>BossController</c> is sealed, signed off, and
    /// drives the Stone Warden as well — and none of what follows needs to be *inside* it. It
    /// subscribes to the brain's phase changes and manipulates the body from outside, which also
    /// means the Tyrant's two bespoke systems can be switched off in a playtest by disabling one
    /// component.</para>
    ///
    /// <para>The junk-rain deliberately does <em>not</em> live here: it is an ordinary boss move
    /// with hazards, unlocked at phase 1, whose hazard prefab happens to carry a
    /// <see cref="SkyDropVisual"/>. Reusing the shipped hazard path is what guarantees the junk is
    /// ground-telegraphed and cannot land anywhere the circle did not promise.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BossController))]
    public sealed class TyrantPhaseDirector : MonoBehaviour
    {
        [Header("Phase 2 — the hoard wakes")]
        [SerializeField, Tooltip("Zero-based phase at which the amber hardens. 1 = the second phase (66% health).")]
        int hardenAtPhase = 1;

        [SerializeField, Tooltip("Zone ids that become armoured. The rest of the body stays soft, which is what makes the player re-learn where to hit.")]
        string[] hardeningZones = { "FlankL", "FlankR", "Skull" };

        [SerializeField, Range(0f, 1f), Tooltip("Damage reduction the new plating provides once it hardens.")]
        float hardenedReduction = 0.7f;

        [Header("Phase 3 — the stutter")]
        [SerializeField, Tooltip("Zero-based phase at which the timing starts to corrupt. 2 = the third phase (33% health).")]
        int stutterAtPhase = 2;

        [SerializeField, Tooltip("Seconds between stutters. The corruption has to be frequent enough to re-teach and rare enough to stay readable.")]
        float stutterIntervalSeconds = 2.6f;

        [SerializeField, Tooltip("How long a frame-hold freezes it. A beat of silence, not a pause.")]
        float holdSeconds = 0.16f;

        [SerializeField, Tooltip("How far a warp jumps it along its own facing. ENEMIES_BIOME1.md 4 Phase 3 asks for ~3m.")]
        float warpDistance = 3f;

        [Header("Junk-ring impact")]
        [SerializeField, Tooltip("Move id of the arena charge. Hitting the junk ring at the arena edge with it opens a scripted stagger window.")]
        string chargeMoveId = "ArenaCharge";

        [SerializeField, Tooltip("How long it stands inert after slamming the junk ring. The punish window an Immune enemy earns with positioning instead of poise.")]
        float junkRingStaggerSeconds = 2.0f;

        [SerializeField, Tooltip("Layers that count as the junk ring. Environment only.")]
        LayerMask ringLayers = 1;

        [SerializeField, Tooltip("Colour held through a scripted stagger, so it reads as unmistakably as a poise break does on trash.")]
        Color scriptedStaggerColor = new Color(0.55f, 0.4f, 1f);

        [Header("Seed junk")]
        [SerializeField, Tooltip("One of these is shown per run, chosen from the run seed. Cosmetic — screenshot bait, per ENEMIES_BIOME1.md 4.")]
        GameObject[] seedJunkOptions = new GameObject[0];

        BossController boss;
        EnemyActor actor;
        CharacterController controller;
        DamageZone[] zones;

        bool subscribed;
        bool hardened;
        bool stuttering;
        float stutterTimer;
        float holdRemaining;
        int stutterCount;

        Vector3 positionBeforeMove;
        float scriptedStaggerRemaining;
        bool ringSlammedThisCharge;
        int seedJunkIndex = -1;

        /// <summary>True while the scripted junk-ring window is open and it is standing inert.</summary>
        public bool IsScriptedStaggered => scriptedStaggerRemaining > 0f;

        /// <summary>Which seed-junk prop this run drew. -1 before it is bound.</summary>
        public int SeedJunkIndex => seedJunkIndex;

        /// <summary>
        /// Draws this run's visible junk from the run seed.
        ///
        /// <para>Purely cosmetic — ENEMIES_BIOME1.md § 4 calls it "cheap, screenshot bait" — but it
        /// draws from the seeded stream rather than <c>UnityEngine.Random</c> so that quoting a seed
        /// still reproduces the run exactly, right down to what the boss is wearing.</para>
        /// </summary>
        public void BindSeedJunk(Game.Core.Rng.IRandomSource random)
        {
            if (random == null || seedJunkOptions.Length == 0)
                return;

            seedJunkIndex = random.NextInt(0, seedJunkOptions.Length);

            for (int i = 0; i < seedJunkOptions.Length; i++)
            {
                if (seedJunkOptions[i] != null)
                    seedJunkOptions[i].SetActive(i == seedJunkIndex);
            }

            GameLog.Info(LogCategory.Enemy, $"tyrant seed junk: piece {seedJunkIndex} of {seedJunkOptions.Length}");
        }

        /// <summary>True once the amber has hardened across its flanks and skull.</summary>
        public bool IsHardened => hardened;

        /// <summary>True while Phase 3's timing corruption is running.</summary>
        public bool IsStuttering => stuttering;

        /// <summary>How many stutters have fired. Alternates hold and warp deterministically.</summary>
        public int StutterCount => stutterCount;

        void Awake()
        {
            boss = GetComponent<BossController>();
            actor = GetComponent<EnemyActor>();
            controller = GetComponent<CharacterController>();
            zones = GetComponentsInChildren<DamageZone>(includeInactive: true);
        }

        void OnDestroy()
        {
            if (subscribed && boss != null && boss.Brain != null)
                boss.Brain.PhaseChanged -= OnPhaseChanged;
        }

        void Update()
        {
            // The brain does not exist until the spawner binds the boss its seeded stream, which
            // happens after Awake. Subscribing lazily avoids ordering assumptions between the two.
            if (!subscribed && boss.IsBound)
            {
                boss.Brain.PhaseChanged += OnPhaseChanged;
                subscribed = true;
            }

            if (scriptedStaggerRemaining > 0f)
            {
                TickScriptedStagger();
                return;
            }

            if (holdRemaining > 0f)
            {
                holdRemaining -= Time.unscaledDeltaTime;
                if (holdRemaining <= 0f)
                    EndHold();

                return;
            }

            CheckJunkRingImpact();

            if (stuttering)
                TickStutter();

            positionBeforeMove = transform.position;
        }

        /// <summary>
        /// Opens a punish window when the arena charge slams the junk ring at the arena edge.
        ///
        /// <para>ENEMIES_BIOME1.md § 4 Phase 1: "impact with the junk ring at the arena edge = brief
        /// scripted stagger window". It has to be scripted because the Tyrant is Immune tier —
        /// <c>EnemyActor.ApplyStagger</c> refuses Immune outright, and rightly so. This is the same
        /// idea as the phase break: a window earned with <em>positioning</em> rather than poise,
        /// which is what stops an un-interruptible boss reading as unresponsive.</para>
        ///
        /// <para>Detected by displacement, like every other charge in this biome: a
        /// <c>CharacterController</c> reports a side collision for any graze, and only "did it
        /// actually stop?" is the right question.</para>
        /// </summary>
        void CheckJunkRingImpact()
        {
            bool charging = boss.IsBound
                && boss.Attacks.Phase == AttackPhase.Active
                && boss.Brain.CurrentMove != null
                && boss.Brain.CurrentMove.Id == chargeMoveId;

            if (!charging)
            {
                ringSlammedThisCharge = false;
                return;
            }

            if (ringSlammedThisCharge)
                return;

            IAttackDefinition current = boss.Attacks.Current;
            if (current == null)
                return;

            float speed = boss.Brain.CurrentMove.LungeDistance / Mathf.Max(0.0001f, current.ActiveSeconds);
            float expected = speed * Time.deltaTime;
            if (expected <= 0.0001f)
                return;

            float actual = Vector3.Distance(
                new Vector3(positionBeforeMove.x, 0f, positionBeforeMove.z),
                new Vector3(transform.position.x, 0f, transform.position.z));

            if (actual >= expected * 0.35f)
                return;

            if (!Physics.Raycast(transform.position, transform.forward, controller.radius + 0.6f,
                    ringLayers, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            ringSlammedThisCharge = true;
            BeginScriptedStagger();
        }

        void BeginScriptedStagger()
        {
            scriptedStaggerRemaining = junkRingStaggerSeconds;
            boss.enabled = false;
            actor.TelegraphOverride = scriptedStaggerColor;
            actor.TelegraphProgress = 1f;

            GameLog.Info(LogCategory.Enemy,
                $"TYRANT slammed the junk ring - inert {junkRingStaggerSeconds:0.0}s (punish window open)");
        }

        void TickScriptedStagger()
        {
            scriptedStaggerRemaining -= Time.deltaTime;
            if (scriptedStaggerRemaining > 0f)
                return;

            scriptedStaggerRemaining = 0f;
            actor.TelegraphOverride = null;
            actor.TelegraphProgress = 0f;

            if (boss != null && actor.IsAlive)
                boss.enabled = true;
        }

        void OnPhaseChanged(int phaseIndex)
        {
            if (phaseIndex >= hardenAtPhase && !hardened)
                HardenAmber();

            if (phaseIndex >= stutterAtPhase && !stuttering)
            {
                stuttering = true;
                stutterTimer = stutterIntervalSeconds;
                GameLog.Info(LogCategory.Enemy, "TYRANT PHASE 3 - the stolen time destabilises; telegraph timing is now corrupt");
            }
        }

        /// <summary>
        /// Turns named zones armoured mid-fight.
        ///
        /// <para>Only some of them. Hardening the whole body would just be a damage-reduction buff;
        /// hardening the flanks and skull specifically means the soft ground <em>moved</em>, and the
        /// player has to find it again while the fight is still going. That is the phase's actual
        /// content — §4 Phase 2, "the player must re-learn where to hit".</para>
        /// </summary>
        void HardenAmber()
        {
            hardened = true;
            int count = 0;

            for (int i = 0; i < zones.Length; i++)
            {
                DamageZone zone = zones[i];
                if (zone == null || !ShouldHarden(zone.ZoneId))
                    continue;

                zone.SetArmored(true, hardenedReduction);
                count++;
            }

            GameLog.Info(LogCategory.Enemy,
                $"TYRANT PHASE 2 - the hoard wakes: {count} zone(s) hardened to amber (x{1f - hardenedReduction:0.00} damage)");
        }

        bool ShouldHarden(string zoneId)
        {
            for (int i = 0; i < hardeningZones.Length; i++)
            {
                if (hardeningZones[i] == zoneId)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Phase 3's corruption, alternating deterministically between the two failure modes §4
        /// describes: a beat of silence dropped into a telegraph, and a mid-move position jump.
        ///
        /// <para>Alternating rather than rolling is deliberate. A boss whose timing corrupts
        /// <em>randomly</em> is unlearnable, which would fail the read-and-react promise the whole
        /// game rests on; alternating keeps it corrupt but honest — and it costs no RNG draw, so it
        /// cannot perturb the boss's seeded move stream.</para>
        /// </summary>
        void TickStutter()
        {
            if (!actor.IsAlive)
                return;

            stutterTimer -= Time.deltaTime;
            if (stutterTimer > 0f)
                return;

            stutterTimer = stutterIntervalSeconds;
            stutterCount++;

            bool warp = stutterCount % 2 == 0;

            if (warp && boss.Attacks.IsAttacking)
                Warp();
            else
                BeginHold();
        }

        /// <summary>
        /// Freezes the body for a beat.
        ///
        /// <para>Implemented by switching the controller off, which stops it ticking its own attack
        /// state machine — so the wind-up genuinely holds where it is rather than being faked. §4:
        /// "the tail sweep plays its windup, drops a beat of silence, then executes." Runs on the
        /// unscaled clock so a hold cannot be swallowed by hitstop.</para>
        /// </summary>
        void BeginHold()
        {
            holdRemaining = holdSeconds;
            boss.enabled = false;

            GameLog.Debug(LogCategory.Enemy, $"tyrant stutter: frame-hold {holdSeconds:0.00}s");
        }

        void EndHold()
        {
            holdRemaining = 0f;

            if (boss != null && actor.IsAlive)
                boss.enabled = true;
        }

        /// <summary>
        /// Jumps the body forward along its own facing.
        ///
        /// <para>§4: "mid-charge it flickers and reappears ~3m ahead". The teleport syncs physics
        /// afterwards — moving a <c>CharacterController</c> without it leaves the physics engine
        /// believing the body is still where it was, a trap this project has hit twice before.</para>
        /// </summary>
        void Warp()
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
                return;

            Vector3 destination = transform.position + forward.normalized * warpDistance;

            // Refuse a warp that would put it inside geometry. A boss embedded in a wall is worse
            // than a boss that skipped one stutter.
            if (Physics.Raycast(transform.position, forward.normalized, warpDistance + controller.radius,
                    LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore))
            {
                GameLog.Debug(LogCategory.Enemy, "tyrant stutter: warp refused - wall ahead");
                return;
            }

            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            transform.position = destination;
            controller.enabled = wasEnabled;
            Physics.SyncTransforms();

            GameLog.Debug(LogCategory.Enemy, $"tyrant stutter: warp {warpDistance:0.0}m forward");
        }
    }
}
