using Game.Combat;
using Game.Core.Diagnostics;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// A travelling hitbox. Swept rather than point-sampled: at 9 m/s a 0.35 m projectile
    /// moves further than its own diameter in a single 60 Hz frame, so a naive per-frame
    /// overlap test would tunnel straight through a thin target.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Projectile : MonoBehaviour
    {
        [SerializeField] LayerMask hittableLayers;
        [SerializeField, Tooltip("Layers that stop the projectile without being damaged (walls).")]
        LayerMask blockingLayers;

        [SerializeField, Tooltip("Optional. Spawned wherever this projectile stops — hit, wall or expiry. Sailspit's glob leaves a stall zone this way.")]
        GameObject impactPrefab;

        [SerializeField, Tooltip("Optional. Peak height of a purely VISUAL lob. The hitbox stays planar, so what the player dodges is exactly what they see crossing the ground.")]
        float visualArcHeight;

        [SerializeField, Tooltip("Which child to lift along the arc. Left empty, the whole object is lifted, which also moves the hitbox — set this on anything with a real arc.")]
        Transform visualRoot;

        readonly Collider[] overlapResults = new Collider[8];

        ProjectileMotion motion;
        HitResolver resolver;
        IAttackDefinition payload;
        Transform owner;
        float radius;
        float arcRange;

        // --- lob mode ---
        bool isLob;
        Vector3 lobFrom;
        Vector3 lobTo;
        float lobDuration;
        float lobElapsed;
        ImpactTelegraph lobTelegraph;

        /// <summary>
        /// The footprint whatever this leaves behind will actually have, read off the prefab it
        /// will spawn. Lets a landing telegraph be drawn at the true size before the thing exists,
        /// so the warning cannot describe a different patch of ground from the one that appears.
        /// Zero when this projectile leaves nothing.
        /// </summary>
        public float ImpactFootprintRadius =>
            impactPrefab != null && impactPrefab.TryGetComponent(out StallZone zone) ? zone.EffectiveRadius : 0f;

        /// <summary>
        /// Arms the projectile. The resolver is the <em>shooter's</em>, so a projectile hit
        /// runs the same modifier pipeline as any other hit from that enemy.
        /// </summary>
        public void Launch(
            Vector3 origin,
            Vector3 direction,
            IAttackDefinition attack,
            HitResolver hitResolver,
            Transform shooter,
            RangedProfile profile,
            LayerMask targetLayers,
            LayerMask blockers,
            float expectedRange = 0f)
        {
            payload = attack;
            resolver = hitResolver;
            owner = shooter;
            radius = Mathf.Max(0.05f, profile.ProjectileRadius);
            hittableLayers = targetLayers;
            blockingLayers = blockers;

            // How far this shot is expected to travel, so a lob peaks over the middle of its own
            // flight rather than over the middle of its theoretical maximum range. 0 falls back to
            // the full range, which is what a straight bolt wants anyway.
            arcRange = expectedRange > 0.1f
                ? expectedRange
                : Mathf.Max(0.1f, profile.ProjectileSpeed * profile.ProjectileLifetime);

            motion = new ProjectileMotion(origin, direction, profile.ProjectileSpeed, profile.ProjectileLifetime);
            transform.position = origin;
            transform.localScale = Vector3.one * (radius * 2f);
        }

        /// <summary>
        /// Lobs this projectile onto a chosen patch of ground rather than at anybody.
        ///
        /// <para><b>It deals no contact damage at all.</b> That is the point of the rework: aimed at
        /// the player, a glob is either dodgeable — so it always lands where they just were and
        /// denies nothing — or it is not, which breaks DESIGN.md's no-guaranteed-damage rule.
        /// Thrown at the ground <em>near</em> them it becomes an area-denial tool whose whole threat
        /// is the puddle it leaves, and the answer is where you stand rather than whether you
        /// reacted in time.</para>
        ///
        /// <para>Walls stop mattering for the same reason: the shot arcs over the arena to a point
        /// already validated as walkable, so it can no longer paint a puddle onto a wall the way
        /// stopping-on-impact used to.</para>
        /// </summary>
        public void LaunchLob(
            Vector3 origin,
            Vector3 groundTarget,
            float airtimeSeconds,
            IAttackDefinition attack,
            HitResolver hitResolver,
            Transform shooter,
            RangedProfile profile,
            ImpactTelegraph telegraph)
        {
            payload = attack;
            resolver = hitResolver;
            owner = shooter;
            radius = Mathf.Max(0.05f, profile.ProjectileRadius);

            isLob = true;
            lobFrom = origin;
            lobTo = groundTarget;
            lobDuration = Mathf.Max(0.05f, airtimeSeconds);
            lobElapsed = 0f;
            lobTelegraph = telegraph;

            arcRange = Vector3.Distance(new Vector3(origin.x, 0f, origin.z), new Vector3(groundTarget.x, 0f, groundTarget.z));
            transform.position = origin;
            transform.localScale = Vector3.one * (radius * 2f);
        }

        void Update()
        {
            if (isLob)
            {
                TickLob();
                return;
            }

            if (motion == null)
                return;

            float deltaTime = Time.deltaTime;
            Vector3 from = motion.Position;
            Vector3 step = motion.Tick(deltaTime);

            if (step.sqrMagnitude > 0f && SweepHit(from, step))
                return;

            transform.position = motion.Position;
            ApplyVisualArc();

            if (motion.Expired)
            {
                GameLog.Debug(LogCategory.Enemy, $"projectile expired after {motion.DistanceTravelled:0.##}m");
                Despawn();
            }
        }

        /// <summary>
        /// Lifts the visual along a parabola without touching the simulated position.
        ///
        /// <para>ENEMIES_BIOME1.md § 2.3 wants Sailspit's glob to arc, and the arc is worth having:
        /// it reads as a lobbed blob of amber rather than a bolt, which is most of what separates
        /// Sailspit's two moves at a glance. But the hitbox stays exactly where
        /// <c>ProjectileMotion</c> puts it — planar, swept, already proven — so what the player
        /// dodges is the shadow crossing the ground, not a shape the physics never agreed to.</para>
        /// </summary>
        void ApplyVisualArc()
        {
            if (visualArcHeight <= 0f || visualRoot == null)
                return;

            float t = Mathf.Clamp01(motion.DistanceTravelled / arcRange);
            visualRoot.localPosition = new Vector3(0f, visualArcHeight * Mathf.Sin(t * Mathf.PI), 0f);
        }

        /// <summary>
        /// Flies the lob to its chosen point. Purely kinematic — no sweep, no overlap, no hit — so
        /// the only thing this can do to anybody is arrive.
        /// </summary>
        void TickLob()
        {
            lobElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(lobElapsed / lobDuration);

            Vector3 planar = Vector3.Lerp(lobFrom, lobTo, t);

            // The arc is the whole read at a glance: a lobbed blob rather than a flicked spine.
            // Here it may move the object outright, because there is no hitbox to keep honest.
            float lift = visualArcHeight > 0f ? visualArcHeight : Mathf.Max(1f, arcRange * 0.28f);
            planar.y = Mathf.Lerp(lobFrom.y, lobTo.y, t) + lift * Mathf.Sin(t * Mathf.PI);
            transform.position = planar;

            if (t < 1f)
                return;

            // The warning dies on the same frame the puddle is born, so the two are never both on
            // screen and "about to happen" can never be mistaken for "here now".
            if (lobTelegraph != null)
                lobTelegraph.Dismiss();

            if (impactPrefab != null)
                Instantiate(impactPrefab, lobTo, Quaternion.identity);

            GameLog.Debug(LogCategory.Enemy, $"glob landed at {lobTo}  after {lobDuration:0.##}s");
            Destroy(gameObject);
        }

        /// <summary>Returns true if the projectile was consumed this frame.</summary>
        bool SweepHit(Vector3 from, Vector3 step)
        {
            float distance = step.magnitude;
            Vector3 direction = step / distance;

            if (Physics.SphereCast(from, radius, direction, out RaycastHit blocked, distance, blockingLayers, QueryTriggerInteraction.Ignore))
            {
                GameLog.Debug(LogCategory.Enemy, $"projectile stopped by {blocked.collider.name}");
                motion.Expire();
                Despawn();
                return true;
            }

            int count = Physics.OverlapSphereNonAlloc(
                motion.Position, radius, overlapResults, hittableLayers, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider collider = overlapResults[i];
                if (collider == null || (owner != null && collider.transform.IsChildOf(owner)))
                    continue;

                var target = collider.GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive)
                    continue;

                HitContext context = HitContext.FromAttack(payload, target, direction, motion.Position);
                resolver.Resolve(ref context);
                motion.Expire();
                Despawn();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Drops whatever this projectile leaves behind, then removes it.
        ///
        /// <para>Spawned on <em>every</em> stop — target, wall or expiry — because a glob that only
        /// left its stall zone on a direct hit would make the area-denial move a reward for
        /// accuracy, when the whole point is that missing still costs the player ground.</para>
        /// </summary>
        void Despawn()
        {
            if (impactPrefab != null)
            {
                // On the floor, not at flight height: what it leaves is a patch of ground.
                Vector3 at = motion != null ? motion.Position : transform.position;
                Instantiate(impactPrefab, at, Quaternion.identity);
            }

            Destroy(gameObject);
        }

        /// <summary>
        /// A lob removed before it lands — a room cleared, a scene swapped — must take its warning
        /// with it. A landing indicator outliving the thing it was warning about is a promise of an
        /// impact that will never come, which is worse than no warning at all.
        /// </summary>
        void OnDestroy()
        {
            if (lobTelegraph != null)
                lobTelegraph.Dismiss();
        }
    }
}
