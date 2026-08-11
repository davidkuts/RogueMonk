using System;
using System.Collections.Generic;
using Game.Core.Diagnostics;
using Game.Core.Economy;
using Game.Core.Player;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// The transmission boons the player holds this run, granted by the drafts that
    /// Transmission rewards open. Sits beside <see cref="PlayerBoons"/> (the between-levels
    /// elemental picks) rather than replacing it — the two are different beats of the run and
    /// the full boon system will decide their final relationship.
    ///
    /// Hit-side effects register on the player's resolver as ability-scoped modifiers; the
    /// Ward lane patches the dash's i-frame fraction instead, because insurance is not a hit.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerAttackController))]
    public sealed class TransmissionBoons : MonoBehaviour
    {
        [SerializeField] PlayerAttackController attacks;
        [SerializeField, Tooltip("For the Ward lane's i-frame and grace patches.")]
        PlayerMotor motor;
        [SerializeField, Tooltip("For the Ward lane's shield procs (Guard High).")]
        PlayerHealth health;
        [SerializeField, Tooltip("Normal/Rare/Epic multipliers, shared with everything rarity touches.")]
        RarityScalarSettings rarityScalars;

        readonly List<OwnedBoon> owned = new List<OwnedBoon>();
        readonly List<TransmissionBoonDefinition> ownedDefinitions = new List<TransmissionBoonDefinition>();

        /// <summary>Echo's owed repeats. One scheduler for the lane, shared by every Echo boon held.</summary>
        public EchoRepeatScheduler Repeats { get; } = new EchoRepeatScheduler();

        /// <summary>
        /// Flux's rolls come from here.
        ///
        /// <para>Derived off the run rather than being the run stream, and seeded by
        /// <see cref="BindRunStream"/> when a run begins. How many times Flux rolls is a function
        /// of how the player fights, so spending run-stream draws on it would mean two players
        /// quoting the same seed got different levels — the exact failure DESIGN.md guarded the
        /// boss's move selection against, and M16 guarded reward content against.</para>
        ///
        /// <para>Falls back to a fixed-seed source so a scene with no director (the enemy lab,
        /// tests) still rolls rather than silently never proccing.</para>
        /// </summary>
        public Game.Core.Rng.IRandomSource FluxStream { get; private set; } =
            new Game.Core.Rng.XorShiftRandom(0xF10C5u);

        /// <summary>Hands Flux a stream derived from this run's seed. Called when a run begins.</summary>
        public void BindRunStream(Game.Core.Rng.IRandomSource stream)
        {
            if (stream != null)
                FluxStream = stream;
        }

        public readonly struct OwnedBoon
        {
            public readonly TransmissionBoonDefinition Definition;
            public readonly RewardTier Tier;

            public OwnedBoon(TransmissionBoonDefinition definition, RewardTier tier)
            {
                Definition = definition;
                Tier = tier;
            }
        }

        public IReadOnlyList<OwnedBoon> Owned => owned;

        /// <summary>The owned definitions alone, for draft dedup.</summary>
        public IReadOnlyList<TransmissionBoonDefinition> OwnedDefinitions => ownedDefinitions;

        /// <summary>Raised whenever the loadout changes, for HUD and the debug overlay.</summary>
        public event Action Changed;

        /// <summary>A boon may install several pipeline pieces (a stat mod AND a shield proc).</summary>
        readonly Dictionary<TransmissionBoonDefinition, List<IHitModifier>> registered =
            new Dictionary<TransmissionBoonDefinition, List<IHitModifier>>();

        void Awake()
        {
            if (attacks == null) attacks = GetComponent<PlayerAttackController>();
            if (motor == null) motor = GetComponent<PlayerMotor>();
            if (health == null) health = GetComponent<PlayerHealth>();
        }

        float Scalar(RewardTier tier) => rarityScalars != null ? rarityScalars.Scalar(tier) : 1f;

        public void Grant(TransmissionBoonDefinition boon, RewardTier tier)
        {
            if (boon == null || ownedDefinitions.Contains(boon) || attacks == null)
                return;

            float scalar = Scalar(tier);
            var installed = new List<IHitModifier>();

            AbilityScopedModifier modifier = boon.CreateModifier(scalar);
            if (modifier != null)
                installed.Add(modifier);

            // Guard High: if the cadence is the marked stat, rarity divides the hit count so a
            // better card shields more often — the number scales, the mechanic does not.
            if (boon.ShieldEveryNHits > 0 && health != null)
                installed.Add(new ShieldProcModifier(
                    boon.Ability, boon.ScaledShieldEveryNHits(scalar), health.GrantOneHitShield));

            // Entropy Field's lane: bonus against targets already under a status.
            if (boon.VsStatusDamageBonus > 0f || boon.VsStatusPoiseBonus > 0f)
            {
                installed.Add(new StatusConditionalModifier(
                    boon.Ability, boon.VsStatus,
                    1f + boon.ScaledVsStatusDamageBonus(scalar),
                    1f + boon.ScaledVsStatusPoiseBonus(scalar)));
            }

            // Denny's lane: the hit happens again, later, weaker.
            if (boon.RepeatFraction > 0f)
            {
                installed.Add(new EchoRepeatModifier(
                    boon.Ability, boon.RepeatAnyAbility, boon.ScaledRepeatFraction(scalar),
                    boon.RepeatDelaySeconds, boon.RepeatEveryNHits, Repeats));
            }

            // The waveform's lane: sometimes it hits far harder. Rolled from a stream derived off
            // the run, never the run stream — how often it rolls depends on how the player fights.
            if (boon.ProcChance > 0f)
            {
                installed.Add(new ChanceCritModifier(
                    boon.Ability, boon.ScaledProcChance(scalar), boon.ProcMultiplier,
                    boon.ProcHitstopBonus, FluxStream));
            }

            for (int i = 0; i < installed.Count; i++)
                attacks.Resolver.AddModifier(installed[i]);

            if (installed.Count > 0)
                registered[boon] = installed;

            owned.Add(new OwnedBoon(boon, tier));
            ownedDefinitions.Add(boon);
            ApplyDashPatches();

            GameLog.Info(LogCategory.Combat,
                $"TRANSMISSION INSTALLED  {boon.DisplayName} [{boon.Giver}/{boon.Ability}] at {tier}  -  {owned.Count} held");

            Changed?.Invoke();
        }

        /// <summary>Strips everything, for a new run. Mirrors <see cref="PlayerBoons.ClearForNewRun"/>.</summary>
        public void ClearForNewRun()
        {
            if (attacks != null)
            {
                foreach (KeyValuePair<TransmissionBoonDefinition, List<IHitModifier>> entry in registered)
                {
                    for (int i = 0; i < entry.Value.Count; i++)
                        attacks.Resolver.RemoveModifier(entry.Value[i]);
                }
            }

            registered.Clear();
            owned.Clear();
            ownedDefinitions.Clear();
            Repeats.Clear();
            ApplyDashPatches();
            Changed?.Invoke();
        }

        /// <summary>
        /// Recomputes the dash's i-frame and grace multipliers from every owned Ward-lane
        /// boon. Set as one product each rather than incrementally, so clearing and
        /// re-granting can never drift the values.
        /// </summary>
        void ApplyDashPatches()
        {
            if (motor == null || motor.Dash == null)
                return;

            float iFrames = 1f;
            float grace = 1f;
            for (int i = 0; i < owned.Count; i++)
            {
                TransmissionBoonDefinition boon = owned[i].Definition;
                float scalar = Scalar(owned[i].Tier);
                if (boon.IFrameBonus > 0f)
                    iFrames *= 1f + boon.ScaledIFrameBonus(scalar);
                if (boon.DodgeGraceBonus > 0f)
                    grace *= 1f + boon.ScaledDodgeGraceBonus(scalar);
            }

            motor.Dash.IFrameFractionMultiplier = iFrames;
            motor.Dash.DodgeGraceMultiplier = grace;

            // Skip Frame: the best owned chance wins rather than the chances compounding, matching
            // how the shield regen interval resolves. Recomputed wholesale, never incremented.
            float refundChance = 0f;
            for (int i = 0; i < owned.Count; i++)
            {
                TransmissionBoonDefinition boon = owned[i].Definition;
                if (boon.DashRefundChance > 0f)
                    refundChance = Mathf.Max(refundChance, boon.ScaledDashRefundChance(Scalar(owned[i].Tier)));
            }

            motor.Dash.RefundChance = refundChance;
            motor.Dash.RefundStream = FluxStream;

            // Stay Standing: the shortest owned regen interval wins; if the cadence is the
            // marked stat, rarity divides it.
            shieldRegenInterval = 0f;
            for (int i = 0; i < owned.Count; i++)
            {
                TransmissionBoonDefinition boon = owned[i].Definition;
                if (boon.ShieldRegenSeconds <= 0f)
                    continue;

                float interval = boon.ScaledShieldRegenSeconds(Scalar(owned[i].Tier));
                if (shieldRegenInterval <= 0f || interval < shieldRegenInterval)
                    shieldRegenInterval = interval;
            }

            shieldRegenElapsed = 0f;
        }

        float shieldRegenInterval;
        float shieldRegenElapsed;

        /// <summary>
        /// Pays out the repeats that have come due.
        ///
        /// <para>Damage is applied straight to the target, NOT through the resolver. A repeat is
        /// the consequence of a hit that already resolved — exactly like a burn tick — and sending
        /// it back down the pipeline would let <see cref="EchoRepeatModifier"/> schedule a repeat
        /// from its own repeat, forever. DESIGN.md settled that for DoT in M12.</para>
        ///
        /// <para>Each repeat still reports itself, so the player sees a second number arrive and
        /// can tell the lane is working. A silent repeat would be the invisible-reward mistake the
        /// empowered strike already taught this project once.</para>
        /// </summary>
        void DeliverEchoRepeats()
        {
            if (Repeats.PendingCount == 0)
                return;

            IReadOnlyList<EchoRepeatScheduler.PendingRepeat> due = Repeats.Tick(Time.deltaTime);
            for (int i = 0; i < due.Count; i++)
            {
                EchoRepeatScheduler.PendingRepeat repeat = due[i];
                if (repeat.Target == null || !repeat.Target.IsAlive)
                    continue;

                if (repeat.Target is IEchoRepeatTarget echoable)
                    echoable.ApplyEchoRepeat(repeat.Damage, repeat.DamageType);
            }
        }

        /// <summary>
        /// Ward's Stay Standing: counts up only while the shield is down, so "re-arms every
        /// 30s" means thirty seconds of actually being unshielded, and a shield from any other
        /// source (the Gauntlet Buckle, Guard High) resets nothing — it simply pauses the clock.
        /// </summary>
        void Update()
        {
            DeliverEchoRepeats();

            if (shieldRegenInterval <= 0f || health == null || !health.IsAlive)
                return;

            if (health.HasOneHitShield)
            {
                shieldRegenElapsed = 0f;
                return;
            }

            shieldRegenElapsed += Time.deltaTime;
            if (shieldRegenElapsed < shieldRegenInterval)
                return;

            shieldRegenElapsed = 0f;
            health.GrantOneHitShield();
        }
    }
}
