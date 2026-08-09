using System.Collections.Generic;
using Game.Combat;
using Game.Core.Diagnostics;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Replays an attack that already happened, from where it happened, a fixed delay later.
    ///
    /// <para>This is the Twice-Struck's entire design and, per ENEMIES_BIOME1.md § 3.2, the reason
    /// that elite is worth building at all: it doubles the effective attack density without
    /// doubling the animation count. The echo is not a second attack the brain chooses — it is a
    /// recording, so it cannot desynchronise from what the player just watched.</para>
    ///
    /// <para>Written generically because the theme wants echoes again: § 3.2 calls it training for
    /// "the delayed-read discipline the Custodian weaponizes in Biome 5", and the Tyrant's Phase 3
    /// stutter is the same family of runtime playback manipulation.</para>
    ///
    /// <para>The ghost carries <em>real hitboxes</em>. Dodging the attack is not enough — you
    /// cannot dodge into where it just was — and a decorative ghost would teach the opposite
    /// lesson. Hits resolve through the real body's own <see cref="HitResolver"/>, so the player's
    /// i-frames, the dodge grace and the perfect-dodge reward all apply to an echo exactly as they
    /// do to the original. That is what makes § 3.2's "perfect-dodging the echo also counts" true
    /// without a line of code to enforce it.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EchoPlayback : MonoBehaviour
    {
        [SerializeField, Tooltip("How far behind the real body the echo runs. ENEMIES_BIOME1.md 3.2 asks for 0.5s.")]
        float delaySeconds = 0.5f;

        [SerializeField, Tooltip("Hue of the ghost. Pale bone-cyan: close enough to the dash blue to read as the same phenomenon, far enough that the player's own colour stays theirs.")]
        TelegraphPalette palette;

        [SerializeField, Tooltip("How solid the ghost looks. It is information, not a second enemy, so it must never be mistaken for one.")]
        [Range(0.1f, 1f)] float ghostOpacity = 0.45f;

        [SerializeField, Tooltip("Layers an echo's hitbox can hit. Normally the player only — an echo is not friendly fire.")]
        LayerMask hittableLayers;

        [SerializeField, Tooltip("Height the echo's hitbox is measured from. Match the real body's.")]
        float hitboxHeightOffset = 0.9f;

        struct PendingEcho
        {
            public IAttackDefinition Attack;
            public Vector3 Position;
            public Quaternion Rotation;
            public float LungeDistance;
            public float DueAt;
        }

        readonly List<PendingEcho> pending = new List<PendingEcho>();
        readonly AttackStateMachine echoAttacks = new AttackStateMachine();
        readonly HashSet<IDamageable> alreadyHit = new HashSet<IDamageable>();
        readonly Collider[] overlapResults = new Collider[12];

        HitResolver resolver;
        Transform ghost;
        Vector3 ghostVelocity;

        /// <summary>Echoes recorded but not yet played. Zero means nothing is owed.</summary>
        public int PendingCount => pending.Count;

        /// <summary>True while an echo is actually replaying — its hitbox may be live.</summary>
        public bool IsReplaying => echoAttacks.IsAttacking;

        public float DelaySeconds => delaySeconds;

        /// <summary>Binds the real body's hit pipeline, so an echo's hits carry the same modifiers.</summary>
        public void Bind(HitResolver bodyResolver) => resolver = bodyResolver;

        void Awake()
        {
            resolver = resolver ?? new HitResolver();
            echoAttacks.ActiveStarted += OnEchoActiveStarted;
            echoAttacks.ActiveEnded += OnEchoActiveEnded;
            echoAttacks.AttackEnded += OnEchoEnded;

            BuildGhost();
        }

        void OnDestroy()
        {
            echoAttacks.ActiveStarted -= OnEchoActiveStarted;
            echoAttacks.ActiveEnded -= OnEchoActiveEnded;
            echoAttacks.AttackEnded -= OnEchoEnded;

            if (ghost != null)
                Destroy(ghost.gameObject);
        }

        /// <summary>
        /// Clones the real body's visual once, strips it of anything that could act, and tints it.
        ///
        /// <para>Cloning rather than authoring a second prefab is what § 3.2 means by "costs zero
        /// clips": the ghost is guaranteed to match whatever the body currently looks like,
        /// including after the capsule is swapped for a mesh.</para>
        /// </summary>
        void BuildGhost()
        {
            Transform body = transform.Find("Body");
            if (body == null)
            {
                GameLog.Warn(LogCategory.Enemy, $"{name} has no Body to echo - the ghost will be invisible");
                return;
            }

            GameObject clone = Instantiate(body.gameObject);
            clone.name = "Echo";
            ghost = clone.transform;

            // World-space, not a child: the echo is where the body *was*, and parenting would drag
            // it along with where the body is now — which is precisely the information it exists to
            // withhold.
            ghost.SetParent(null, true);

            foreach (Collider collider in clone.GetComponentsInChildren<Collider>(true))
                Destroy(collider);

            Color tint = TelegraphPalette.Resolve(palette, TelegraphChannel.Echo, new Color(0.78f, 0.95f, 0.98f));
            tint.a = ghostOpacity;

            var block = new MaterialPropertyBlock();
            foreach (Renderer renderer in clone.GetComponentsInChildren<Renderer>(true))
            {
                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", tint);
                renderer.SetPropertyBlock(block);
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            clone.SetActive(false);
        }

        /// <summary>
        /// Records an attack to be replayed. Called the moment the real body commits, so the echo
        /// starts from where the body <em>was</em>, not where it ends up.
        /// </summary>
        public void Record(IAttackDefinition attack, Vector3 position, Quaternion rotation, float lungeDistance)
        {
            if (attack == null)
                return;

            pending.Add(new PendingEcho
            {
                Attack = attack,
                Position = position,
                Rotation = rotation,
                LungeDistance = lungeDistance,
                DueAt = Time.time + Mathf.Max(0f, delaySeconds),
            });
        }

        /// <summary>
        /// Drops every echo not yet playing.
        ///
        /// <para>ENEMIES_BIOME1.md § 3.2: staggering the real body despawns the pending echo — "the
        /// loop is interrupted", and it is the reward for aggression. Without it the player would be
        /// punished half a second after successfully breaking the thing that threw the punch, which
        /// would teach them that interrupting it is pointless.</para>
        ///
        /// <para>An echo <em>already replaying</em> is deliberately left alone: its hitbox is live
        /// and the player is mid-dodge. Cancelling a swing someone is already reacting to is the
        /// same unfairness as cancelling a wind-up, from the other direction.</para>
        /// </summary>
        public void ClearPending()
        {
            if (pending.Count == 0)
                return;

            GameLog.Info(LogCategory.Enemy, $"echo cancelled - {pending.Count} pending replay(s) dropped");
            pending.Clear();
        }

        void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
                return;

            echoAttacks.Tick(deltaTime);

            if (echoAttacks.IsAttacking)
            {
                TickReplay(deltaTime);
            }
            else if (pending.Count > 0 && Time.time >= pending[0].DueAt)
            {
                BeginReplay(pending[0]);
                pending.RemoveAt(0);
            }
        }

        void BeginReplay(in PendingEcho echo)
        {
            if (ghost == null)
                return;

            ghost.SetPositionAndRotation(echo.Position, echo.Rotation);
            ghost.gameObject.SetActive(true);
            ghostVelocity = Vector3.zero;

            alreadyHit.Clear();
            echoAttacks.TryStart(echo.Attack);

            // Reproduces the original's travel from the original's frame data, so an echoed rush
            // runs the same line rather than a straight line to wherever the player now is.
            pendingLunge = echo.LungeDistance;
        }

        float pendingLunge;

        void TickReplay(float deltaTime)
        {
            if (ghost == null)
                return;

            ghost.position += ghostVelocity * deltaTime;

            if (echoAttacks.Phase == AttackPhase.Active)
                QueryEchoHitbox();
        }

        void OnEchoActiveStarted(IAttackDefinition attack)
        {
            alreadyHit.Clear();
            float active = Mathf.Max(0.0001f, attack.ActiveSeconds);
            ghostVelocity = ghost != null ? ghost.forward * (pendingLunge / active) : Vector3.zero;
        }

        void OnEchoActiveEnded(IAttackDefinition attack)
        {
            alreadyHit.Clear();
            ghostVelocity = Vector3.zero;
        }

        void OnEchoEnded(IAttackDefinition attack)
        {
            ghostVelocity = Vector3.zero;
            if (ghost != null)
                ghost.gameObject.SetActive(false);
        }

        void QueryEchoHitbox()
        {
            IAttackDefinition attack = echoAttacks.Current;
            if (attack == null || ghost == null)
                return;

            HitboxShape shape = attack.Hitbox;
            Vector3 origin = ghost.position + Vector3.up * hitboxHeightOffset;
            Vector3 forward = ghost.forward;
            Vector3 center = shape.WorldCenter(origin, forward);

            int count = HitboxQuery.Overlap(shape, origin, forward, hittableLayers, overlapResults);

            for (int i = 0; i < count; i++)
            {
                Collider collider = overlapResults[i];
                if (collider == null)
                    continue;

                if (!HitboxQuery.Contains(shape, origin, forward, collider.transform.position))
                    continue;

                var damageable = collider.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive || alreadyHit.Contains(damageable))
                    continue;

                if (HitZones.IsNonZoneColliderOfZonedBody(collider))
                    continue;

                alreadyHit.Add(damageable);

                Vector3 direction = collider.transform.position - ghost.position;
                HitContext context = HitContext.FromAttack(
                    attack, damageable, direction, collider.ClosestPoint(center), HitZones.Resolve(collider));

                resolver.Resolve(ref context);
            }
        }
    }
}
