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

        readonly Collider[] overlapResults = new Collider[8];

        ProjectileMotion motion;
        HitResolver resolver;
        IAttackDefinition payload;
        Transform owner;
        float radius;

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
            LayerMask blockers)
        {
            payload = attack;
            resolver = hitResolver;
            owner = shooter;
            radius = Mathf.Max(0.05f, profile.ProjectileRadius);
            hittableLayers = targetLayers;
            blockingLayers = blockers;

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

            if (motion.Expired)
            {
                GameLog.Debug(LogCategory.Enemy, $"projectile expired after {motion.DistanceTravelled:0.##}m");
                Despawn();
            }
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

        void Despawn() => Destroy(gameObject);
    }
}
