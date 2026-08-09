using Game.Combat;
using Game.Core.Diagnostics;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// The carnotaurus that hits twice: the game's thesis as an enemy.
    ///
    /// <para>Every attack it throws is recorded and replayed half a second later by a ghost of
    /// itself, from where it stood, with a real hitbox. ENEMIES_BIOME1.md § 3.2: "dodging the attack
    /// is not enough — you cannot dodge into where it just was."</para>
    ///
    /// <para>The controller itself is tiny, because the mechanic lives in
    /// <see cref="EchoPlayback"/> and the moveset lives in data. All this does is decide *when* to
    /// record and when to cancel — and both of those are one line each.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TwiceStruckController : MovesetEnemyController
    {
        [SerializeField, Tooltip("The delayed replay. Left empty, taken from this GameObject.")]
        EchoPlayback echo;

        /// <summary>Echoes recorded and not yet played. Exposed for tests and the HUD.</summary>
        public int PendingEchoes => echo != null ? echo.PendingCount : 0;

        protected override void Awake()
        {
            base.Awake();

            if (echo == null)
                echo = GetComponent<EchoPlayback>();

            if (echo == null)
            {
                GameLog.Warn(LogCategory.Enemy,
                    $"{name} has no {nameof(EchoPlayback)} - it will fight as an ordinary carnotaur, which is the one thing it must not be");
                return;
            }

            // The echo resolves through the body's own pipeline, so a boon or debuff that changes
            // this enemy's damage changes its echo identically. Two different numbers for the same
            // swing would be unreadable.
            echo.Bind(Resolver);
        }

        /// <summary>
        /// Records the swing at the moment it commits.
        ///
        /// <para>At commit, not at impact: the echo has to start from where the body <em>was</em>,
        /// because reproducing the original's line is the whole read. Recording later would have
        /// the ghost chase the player instead of retracing the past.</para>
        /// </summary>
        protected override void OnAttackStarted(IAttackDefinition attack)
        {
            base.OnAttackStarted(attack);

            if (echo == null)
                return;

            float lunge = CurrentMove?.LungeDistance ?? Definition.LungeDistance;
            echo.Record(attack, transform.position, transform.rotation, lunge);
        }

        /// <summary>
        /// Breaking the real body cancels what it owed.
        ///
        /// <para>§ 3.2 makes this the reward for aggression — "the loop is interrupted". Without it
        /// the player would eat a hit half a second after successfully staggering the thing that
        /// threw it, which teaches that interrupting it is pointless.</para>
        /// </summary>
        protected override void OnStaggered()
        {
            base.OnStaggered();

            if (echo == null)
                return;

            GameLog.Info(LogCategory.Enemy, $"{Definition.Id} staggered - pending echo despawned");
            echo.ClearPending();
        }

        protected override void OnDeathStarted()
        {
            base.OnDeathStarted();

            if (echo != null)
                echo.ClearPending();
        }
    }
}
