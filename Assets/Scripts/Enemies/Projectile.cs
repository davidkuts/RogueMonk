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

        void Update()
        {
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
    }
}
