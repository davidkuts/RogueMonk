using System.Collections.Generic;
using Game.Combat;
using Game.Core.Diagnostics;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// A telegraphed patch of floor that erupts. DESIGN.md budgets exactly one environmental
    /// hazard type for the whole game, and this is it.
    ///
    /// It runs on the shared <see cref="AttackStateMachine"/> rather than a timer of its own,
    /// because a hazard <em>is</em> an attack: the telegraph is a wind-up, the eruption is an
    /// active window, and the fade is recovery. That buys the same guarantee every other attack
    /// has — the state machine walks every phase boundary it crosses, so a long frame can never
    /// swallow the damage window — and it puts the hazard's timing, damage and colour in an
    /// ordinary AttackDefinition instead of a bespoke asset type.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloorHazard : MonoBehaviour
    {
        [SerializeField] TelegraphDecal decal;
        [SerializeField, Tooltip("Layers this hazard can damage — the player, not the boss that made it.")]
        LayerMask hittableLayers;
        [SerializeField, Tooltip("Height the damage query is centred at, so it catches a standing capsule.")]
        float queryHeight = 0.9f;

        readonly AttackStateMachine attacks = new AttackStateMachine();
        readonly HashSet<IDamageable> alreadyHit = new HashSet<IDamageable>();
        readonly Collider[] overlapResults = new Collider[8];

        HitResolver resolver;
        AttackDefinition asset;
        bool armed;

        void Awake()
        {
            attacks.ActiveStarted += OnActiveStarted;
            attacks.AttackEnded += OnAttackEnded;
        }

        void OnDestroy()
        {
            attacks.ActiveStarted -= OnActiveStarted;
            attacks.AttackEnded -= OnAttackEnded;
        }

        /// <summary>
        /// Starts the hazard at its current position. The resolver is the <em>spawner's</em>, so a
        /// hazard hit runs the same modifier pipeline as any other hit from that enemy.
        /// </summary>
        public void Arm(AttackDefinition definition, HitResolver hitResolver, LayerMask targetLayers)
        {
            asset = definition;
            resolver = hitResolver;
            hittableLayers = targetLayers;
            alreadyHit.Clear();

            if (asset == null || !attacks.TryStart(asset))
            {
                Destroy(gameObject);
                return;
            }

            armed = true;
        }

        void Update()
        {
            if (!armed)
                return;

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
                return;

            attacks.Tick(deltaTime);

            if (decal == null)
                return;

            if (attacks.Phase == AttackPhase.Windup)
            {
                // Fills from the centre outward, reaching the outline exactly as it erupts — the
                // same grammar the boss's own telegraphs use, so it needs no separate teaching.
                decal.Show(asset.Hitbox, transform.position, transform.forward,
                    asset.TelegraphColor, attacks.WindupProgress, transform.position.y);
            }
            else if (attacks.Phase == AttackPhase.Active)
            {
                decal.Show(asset.Hitbox, transform.position, transform.forward,
                    Color.white, 1f, transform.position.y);
            }
            else
            {
                decal.Hide();
            }
        }

        void OnActiveStarted(IAttackDefinition definition)
        {
            HitboxShape shape = definition.Hitbox;
            Vector3 center = transform.position + Vector3.up * queryHeight;

            int count = Physics.OverlapSphereNonAlloc(
                center, Mathf.Max(0.1f, shape.Radius), overlapResults, hittableLayers, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider collider = overlapResults[i];
                if (collider == null)
                    continue;

                var damageable = collider.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive || alreadyHit.Contains(damageable))
                    continue;

                alreadyHit.Add(damageable);

                // Push outward from the hazard's centre rather than along a facing it does not have.
                Vector3 direction = collider.transform.position - transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude <= 0.0001f)
                    direction = Vector3.forward;

                HitContext context = HitContext.FromAttack(
                    definition, damageable, direction.normalized, collider.ClosestPoint(center));
                resolver.Resolve(ref context);
            }

            GameLog.Debug(LogCategory.Enemy, $"hazard erupted at {transform.position} hitting {alreadyHit.Count}");
        }

        void OnAttackEnded(IAttackDefinition definition) => Destroy(gameObject);
    }
}
