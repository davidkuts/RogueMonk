using Game.Core.Audio;
using Game.Core.Diagnostics;
using Game.Core.Feedback;
using Game.Core.Player;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Spends carried Stopgaps (REWARDS.md §5) — the activation half that M16 deliberately left
    /// out, with the D-pad reserved for it ever since.
    ///
    /// <para>Panic buttons, so every design decision here leans the same way: the press always
    /// does something if anything is carried, it fires on the frame it is pressed, and it is never
    /// refused for a reason the player cannot see. The one thing it will not do is spend an item
    /// on nothing — a Stopgap that cannot help is kept, not burned.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerStopgaps : MonoBehaviour
    {
        [SerializeField] StopgapInventory inventory;
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerHealth health;
        [SerializeField] PlayerMotor motor;
        [SerializeField] PlayerVortex vortex;

        [Header("Stored Rewind")]
        [SerializeField, Tooltip("How far back the history buffer can reach. Must be at least the longest rewind any Stopgap asks for.")]
        float rewindWindowSeconds = 3f;
        [SerializeField, Tooltip("Gap between recorded samples. Finer costs memory for precision nobody can perceive.")]
        float rewindSampleIntervalSeconds = 0.05f;

        [Header("Pocket Freeze")]
        [SerializeField, Tooltip("Radius of the stasis burst around Cole.")]
        float freezeRadiusMeters = 6f;
        [SerializeField, Tooltip("Layers the burst searches for enemies.")]
        LayerMask freezeLayers = ~0;

        [Header("Feedback")]
        [SerializeField, Tooltip("Colour of the float-up text naming the spent Stopgap.")]
        Color spentTextColor = new Color(0.55f, 0.9f, 1f, 1f);

        readonly Collider[] freezeResults = new Collider[32];

        RewindHistory history;

        /// <summary>The rolling record Stored Rewind reads. Exposed for tests and the debug overlay.</summary>
        public RewindHistory History => history;

        void Awake()
        {
            if (inventory == null) inventory = GetComponent<StopgapInventory>();
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (health == null) health = GetComponent<PlayerHealth>();
            if (motor == null) motor = GetComponent<PlayerMotor>();
            if (vortex == null) vortex = GetComponent<PlayerVortex>();

            history = new RewindHistory(rewindWindowSeconds, rewindSampleIntervalSeconds);
        }

        void Update()
        {
            // Recorded on the scaled clock: a rewind should measure gameplay time, so hitstop and
            // a paused menu must not age the buffer out from under the player.
            if (health != null)
                history.Tick(Time.deltaTime, transform.position, health.CurrentHealth);

            if (input != null && input.StopgapPressedThisFrame)
                TryActivate();
        }

        /// <summary>Spends the next carried Stopgap, if there is one and it can do anything.</summary>
        public bool TryActivate()
        {
            if (inventory == null || inventory.Count == 0)
                return false;

            StopgapDefinition next = inventory.Next;
            if (next == null)
                return false;

            // Checked BEFORE consuming: a panic button that eats the item and does nothing is
            // worse than one that refuses out loud.
            if (!CanApply(next))
            {
                GameLog.Info(LogCategory.Combat,
                    $"STOPGAP held  {next.DisplayName} would do nothing right now - not spent");
                return false;
            }

            if (!inventory.TryConsume(out StopgapDefinition stopgap))
                return false;

            Apply(stopgap);

            AudioDirector.PlaySound(GameSound.PerfectDodge);
            RumbleDirector.Rumble(0.5f, 0.7f);
            DamageNumberDirector.Show(transform.position + Vector3.up * 2.4f, 0f, spentTextColor, 1f, allowZero: true);
            return true;
        }

        bool CanApply(StopgapDefinition stopgap)
        {
            switch (stopgap.Kind)
            {
                case StopgapKind.WoundSpring:
                    // Nothing to give back when the vortex is already up.
                    return vortex != null && !vortex.IsReady;

                case StopgapKind.StoredRewind:
                    return health != null && health.IsAlive && history.Count > 0;

                default:
                    return true;
            }
        }

        void Apply(StopgapDefinition stopgap)
        {
            switch (stopgap.Kind)
            {
                case StopgapKind.StoredRewind:
                    ApplyStoredRewind(stopgap.EffectSeconds);
                    break;

                case StopgapKind.PocketFreeze:
                    ApplyPocketFreeze(stopgap.EffectSeconds);
                    break;

                case StopgapKind.WoundSpring:
                    vortex.RefreshCooldown();
                    GameLog.Info(LogCategory.Combat, "WOUND SPRING  the Undertow is ready again");
                    break;
            }
        }

        /// <summary>
        /// Puts the body back where it was and as hurt as it was. Position first, then health, so
        /// a rewind out of a hazard does not immediately re-take the damage it just undid.
        /// </summary>
        void ApplyStoredRewind(float seconds)
        {
            if (!history.TrySample(seconds, out RewindHistory.Sample sample))
                return;

            if (motor != null)
                motor.Teleport(sample.Position);
            else
                transform.position = sample.Position;

            health.RewindTo(sample.Health);

            GameLog.Info(LogCategory.Combat,
                $"STORED REWIND  {seconds:0.0}s back to {sample.Position} at {sample.Health:0.##} hp");
        }

        /// <summary>
        /// A stasis burst: everything in radius is rooted, and everything that CAN be interrupted
        /// is. The split is deliberate — the root is a status and lands on anyone, the stagger goes
        /// through the tier rules, so an Immune boss is pinned without being stunned. A consumable
        /// that stunned a boss would be a better answer than the fight it was in.
        /// </summary>
        void ApplyPocketFreeze(float seconds)
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position, freezeRadiusMeters, freezeResults, freezeLayers);

            int frozen = 0;
            for (int i = 0; i < count; i++)
            {
                Collider collider = freezeResults[i];
                if (collider == null)
                    continue;

                var damageable = collider.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive || ReferenceEquals(damageable, health))
                    continue;

                damageable.Statuses.Apply(StatusEffect.Rooted, seconds);
                damageable.ApplyStagger(seconds);
                frozen++;
            }

            GameLog.Info(LogCategory.Combat,
                $"POCKET FREEZE  {frozen} caught in {seconds:0.0}s of stasis within {freezeRadiusMeters:0.#}m");
        }

        /// <summary>Drops the recorded past. A new room is not somewhere the player can rewind to.</summary>
        public void ClearHistory() => history?.Clear();
    }
}
