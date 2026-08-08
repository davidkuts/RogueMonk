using System.Collections.Generic;
using Game.Core.Audio;
using Game.Core.Diagnostics;
using Game.Core.Feedback;
using Game.Core.Player;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// The Undertow — the default answer to being swarmed (DESIGN.md § The Vortex).
    ///
    /// <para>A stationary spin that drags everything loose in time toward the player, ticking it on
    /// the way in and delivering it staggered. It exists because the base kit otherwise has no
    /// answer to a crowd: without it, every multi-enemy room is "kite until a boon fixes it", and a
    /// player who never draws the right boon has no tool at all.</para>
    ///
    /// <para>It runs as an ordinary attack through <see cref="PlayerAttackController"/>, so it
    /// inherits the wind-up/active/recovery grammar, the dash-cancel rule and the guarantee that a
    /// long frame cannot swallow its window. The one thing it does for itself is hit detection,
    /// because it has to strike the same enemy once per tick — see
    /// <see cref="PlayerAttackController.SuppressDefaultHitbox"/>.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerAttackController))]
    public sealed class PlayerVortex : MonoBehaviour
    {
        [SerializeField] VortexDefinition vortex;
        [SerializeField] PlayerAttackController attacks;
        [SerializeField] PlayerInputReader input;

        [Header("Targeting")]
        [SerializeField, Tooltip("Layers the pull can reach. Matches the attack controller's.")]
        LayerMask hittableLayers = ~0;
        [SerializeField, Tooltip("Origin height for the radial query, so a ground-level sphere still overlaps capsules.")]
        float hitboxHeightOffset = 0.9f;

        [Header("Feedback")]
        [SerializeField] Vector2 castRumble = new Vector2(0.35f, 0.6f);
        [SerializeField, Tooltip("The character model. Spun in code across the move, because the clip's own rotation only survives Unity's humanoid root baking at about half strength and the circle never closes. Left empty, the first child with a renderer is used.")]
        Transform modelRoot;

        readonly Collider[] overlapResults = new Collider[32];
        readonly List<IDamageable> caught = new List<IDamageable>();
        readonly HashSet<IDamageable> pulledThisSpin = new HashSet<IDamageable>();
        readonly Dictionary<IDamageable, float> pullImmuneUntil = new Dictionary<IDamageable, float>();

        VortexAbility ability;
        VortexSpin spin;
        bool spinning;
        bool bodySpinning;
        float spinElapsed;
        Quaternion modelRestRotation = Quaternion.identity;

        /// <summary>0 just spent, 1 ready. For a HUD dial.</summary>
        public float ReadyFraction => ability != null ? ability.ReadyFraction : 1f;

        public bool IsReady => ability != null && ability.IsReady;

        public float CooldownRemaining => ability != null ? ability.CooldownRemaining : 0f;

        /// <summary>True while the pull window is open — the gameplay half of the move.</summary>
        public bool IsSpinning => spinning;

        /// <summary>
        /// True while the body is still turning, which outlasts the pull: the pull ends with the
        /// active window, the turn runs to the end of recovery. VFX follow this one, or the smear
        /// would cut out part-way through a spin that is visibly still going.
        /// </summary>
        public bool IsBodySpinning => bodySpinning;

        public VortexDefinition Definition => vortex;

        void Awake()
        {
            if (attacks == null) attacks = GetComponent<PlayerAttackController>();
            if (input == null) input = GetComponent<PlayerInputReader>();

            if (vortex == null)
            {
                Debug.LogError($"{nameof(PlayerVortex)} on '{name}' has no {nameof(VortexDefinition)} assigned.", this);
                enabled = false;
                return;
            }

            if (modelRoot == null)
            {
                var renderer = GetComponentInChildren<Renderer>();
                if (renderer != null && renderer.transform != transform)
                    modelRoot = renderer.transform.parent != null && renderer.transform.parent != transform
                        ? renderer.transform.parent
                        : renderer.transform;
            }

            if (modelRoot != null)
                modelRestRotation = modelRoot.localRotation;

            ability = new VortexAbility(vortex.CooldownSeconds, vortex.PerHitRefundSeconds);
            spin = new VortexSpin(vortex.TickCount, vortex.ActiveSeconds);

            ability.BecameReady += OnBecameReady;
            attacks.Hit += OnHit;
            attacks.Attacks.ActiveEnded += OnActiveEnded;
        }

        void OnDestroy()
        {
            if (ability != null) ability.BecameReady -= OnBecameReady;
            if (attacks != null)
            {
                attacks.Hit -= OnHit;
                attacks.Attacks.ActiveEnded -= OnActiveEnded;
            }
        }

        void Update()
        {
            float deltaTime = Time.deltaTime;
            ability.Tick(deltaTime);

            if (!spinning && input != null && input.VortexPressedThisFrame)
                TryCast();

            // The turn is driven off the attack itself rather than off the pull window, so it keeps
            // going through recovery. Tying it to the pull left the body standing still for the last
            // stretch of a move whose limbs were still moving, which read as the animation freezing.
            // The rotation itself is applied in LateUpdate — see there for why.
            bool vortexRunning = attacks.Attacks.Current != null && attacks.Attacks.Current.Id == vortex.Id;
            bodySpinning = vortexRunning;
            spinElapsed = vortexRunning ? attacks.Attacks.Elapsed : 0f;

            if (!spinning)
                return;

            // Dash-cancelled, or interrupted some other way: keep whatever pull already happened and
            // forfeit the remaining ticks, which is what makes cancelling a real mid-spin decision.
            if (!vortexRunning)
            {
                EndSpin(cancelled: true);
                return;
            }

            if (attacks.Attacks.Phase != AttackPhase.Active)
                return;

            float elapsedActive = attacks.Attacks.Elapsed - vortex.WindupSeconds;
            int due = spin.Due(elapsedActive);
            for (int i = 0; i < due; i++)
                Tick();
        }

        void TryCast()
        {
            if (!ability.IsReady)
                return;

            if (!attacks.TryStartSpecial(vortex))
                return;

            ability.TryConsume();
            spin.Reset();
            caught.Clear();
            pulledThisSpin.Clear();
            spinning = true;
            attacks.SuppressDefaultHitbox = true;

            RumbleDirector.Rumble(castRumble.x, castRumble.y);
            AudioDirector.PlaySound(GameSound.Dash);

            GameLog.Info(LogCategory.Combat,
                $"VORTEX        {vortex.Id}  radius {vortex.RadiusMeters:0.#}m -> ring {vortex.InnerRingMeters:0.#}m  " +
                $"{vortex.TickCount} ticks over {vortex.ActiveSeconds:F2}s  cooldown {vortex.CooldownSeconds:0.#}s");
        }

        /// <summary>
        /// One tick of the spin: everything in radius takes a resolved hit whose knockback is
        /// negative, which the enemy reads as a pull. Same impulse channel as an ordinary knockback,
        /// reversed sign — no second movement path to keep in step with the first.
        /// </summary>
        void Tick()
        {
            Vector3 origin = transform.position + Vector3.up * hitboxHeightOffset;
            float now = Time.time;

            int count = Physics.OverlapSphereNonAlloc(
                origin, vortex.RadiusMeters, overlapResults, hittableLayers, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider collider = overlapResults[i];
                if (collider == null || collider.transform.IsChildOf(transform))
                    continue;

                var target = collider.GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive)
                    continue;

                Vector3 toTarget = collider.transform.position - transform.position;
                toTarget.y = 0f;
                float distance = toTarget.magnitude;

                // Pull strength is proportional to how far past the ring they are, so the spin
                // gathers a spread crowd onto the ring instead of firing everything through the
                // player. Something already on the ring is ticked but not moved.
                // Anything past the ring gets a floor on its impulse. Proportional-only pull meant
                // a target standing just outside the ring moved a few centimetres, which is
                // indistinguishable from the move doing nothing — and "it doesn't pull them in" is
                // exactly how it read in play. If the vortex catches you, you move.
                float overshoot = Mathf.Max(0f, distance - vortex.InnerRingMeters);
                float impulse = overshoot > 0f
                    ? Mathf.Clamp(overshoot * vortex.PullImpulsePerMeter, vortex.MinPullImpulse, vortex.MaxPullImpulse)
                    : 0f;

                // Recently dragged in, so this spin neither moves it nor interrupts it again. With
                // no cooldown on the ability this window is the only thing standing between
                // "spammable" and "permanent stun-lock", which is exactly what it was reserved for.
                // The ticks still land: it is standing in a vortex.
                bool pullImmune = pullImmuneUntil.TryGetValue(target, out float until) && now < until;
                if (pullImmune)
                    impulse = 0f;

                // One impulse per target per spin, not one per tick. Three stacked impulses
                // overshot the ring completely and body-blocked against the player's own capsule
                // (measured: everything ended up at 0.777 m, which is just the two capsule radii
                // touching). A single impulse sized to the overshoot lands them *on* the ring,
                // which is the whole point of having one.
                if (!pulledThisSpin.Add(target))
                    impulse = 0f;

                Vector3 direction = distance > 0.001f ? toTarget / distance : Vector3.forward;

                HitContext context = HitContext.FromAttack(vortex, target, direction, collider.ClosestPoint(origin));
                context.Knockback = -impulse; // negative: inward
                attacks.Resolver.Resolve(ref context);

                if (!pullImmune && !caught.Contains(target))
                    caught.Add(target);
            }
        }

        /// <summary>
        /// Pays out anything the active window still owed, then delivers the arrival stagger.
        ///
        /// <para>The stagger survives a cancel on purpose. It is the promise that answers the
        /// ability's own risk — it drags threats into hug range — so forfeiting it when the player
        /// dash-cancels would turn the cancel into a trap rather than a choice.</para>
        /// </summary>
        void OnActiveEnded(IAttackDefinition definition)
        {
            if (!spinning || definition == null || definition.Id != vortex.Id)
                return;

            int owed = spin.Drain();
            for (int i = 0; i < owed; i++)
                Tick();

            EndSpin(cancelled: false);
        }

        /// <summary>
        /// Applies the spin — and, when there is no spin, holds the model at its rest pose.
        ///
        /// <para>In <c>LateUpdate</c> because the Animator poses the character <em>after</em>
        /// <c>Update</c> runs. Rotating the model in Update meant the Animator wrote over it the
        /// same frame, and the residue accumulated: the model drifted several degrees off rest and
        /// kept creeping. Procedural rotation layered on top of animation has to come after the
        /// animation, which is exactly what LateUpdate is for.</para>
        ///
        /// <para>Restoring every frame rather than once on the transition is deliberate for the
        /// same reason — it means nothing can leave the body quietly askew.</para>
        /// </summary>
        void LateUpdate()
        {
            if (modelRoot == null)
                return;

            if (bodySpinning)
                DriveSpin(spinElapsed);
            else
                RestoreModel();
        }

        /// <summary>
        /// Turns the model across the move.
        ///
        /// <para>The clip is authored with a full revolution in it, but Unity's humanoid
        /// root-rotation baking only delivered about half of it to the body — measured at ~192° of
        /// an authored 366° — so the circle visibly never closed. Owning the turn in code makes it
        /// exact, gives the direction a single explicit source, and keeps the clip's job to what it
        /// is good at: the limbs. Root motion stays off; this rotates the *visual* only, never the
        /// player transform, so facing, aim and the hitbox are untouched.</para>
        ///
        /// <para>Spread over wind-up + active rather than the whole move, so the turn completes as
        /// the pull does and recovery is the character settling rather than still spinning.</para>
        /// </summary>
        void DriveSpin(float elapsed)
        {
            if (modelRoot == null)
                return;

            float t = Mathf.Clamp01(elapsed / vortex.SpinSeconds);
            modelRoot.localRotation = modelRestRotation * Quaternion.Euler(0f, vortex.SpinDegrees * t, 0f);
        }

        /// <summary>
        /// Hands the body back square. Always called when the attack stops being the vortex, however
        /// it stopped — a cancelled spin that left the model at 200° would leave the character
        /// permanently facing the wrong way.
        /// </summary>
        void RestoreModel()
        {
            if (modelRoot != null)
                modelRoot.localRotation = modelRestRotation;
        }

        void EndSpin(bool cancelled)
        {
            spinning = false;
            attacks.SuppressDefaultHitbox = false;

            // Deliberately does NOT restore the model here: the pull ends with the active window but
            // the turn runs on through recovery, and LateUpdate owns the model either way.
            if (cancelled)
                bodySpinning = false;

            float now = Time.time;
            for (int i = 0; i < caught.Count; i++)
            {
                IDamageable target = caught[i];
                if (target == null || !target.IsAlive)
                    continue;

                target.ApplyStagger(vortex.ArrivalStaggerSeconds);
                pullImmuneUntil[target] = now + vortex.PullImmunitySeconds;
            }

            GameLog.Info(LogCategory.Combat,
                $"VORTEX end    caught {caught.Count}  ticks {spin.Fired}/{spin.TickCount}" +
                (cancelled ? "  (cancelled - remaining ticks forfeited)" : string.Empty) +
                $"  stagger {vortex.ArrivalStaggerSeconds:0.00}s");

            caught.Clear();
            PruneImmunities(now);
        }

        /// <summary>Keeps the immunity map from growing across a whole run of dead enemies.</summary>
        void PruneImmunities(float now)
        {
            if (pullImmuneUntil.Count == 0)
                return;

            var stale = new List<IDamageable>();
            foreach (KeyValuePair<IDamageable, float> entry in pullImmuneUntil)
            {
                if (entry.Value <= now || entry.Key == null || !entry.Key.IsAlive)
                    stale.Add(entry.Key);
            }

            for (int i = 0; i < stale.Count; i++)
                pullImmuneUntil.Remove(stale[i]);
        }

        /// <summary>
        /// Aggression is what recharges the vortex. The ability's own ticks are excluded, or it
        /// would refund its own cooldown out of the crowd it just gathered.
        /// </summary>
        void OnHit(HitContext context)
        {
            if (context.Attack == null || context.Attack.Id == vortex.Id)
                return;

            ability.RegisterLandedHit();
        }

        void OnBecameReady()
        {
            // Readiness has to be perceivable without looking at the HUD, or players sit on it.
            AudioDirector.PlaySound(GameSound.PerfectDodge);
            GameLog.Info(LogCategory.Combat, $"VORTEX ready  {vortex.Id}");
        }
    }
}
