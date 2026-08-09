using Game.Core.Diagnostics;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Scene-side owner of the one <see cref="AttackTokenPool"/> every enemy competes in.
    ///
    /// <para>A thin adapter, exactly as CLAUDE.md rule 1 asks: the arbitration is engine-free and
    /// tested; this holds the caps as serialized data and hands the pool out. One broker per
    /// scene, found through <see cref="Current"/> the way <c>GameClock</c> is.</para>
    ///
    /// <para>Enemies must survive its absence. A room with no broker simply has no cap, which is
    /// the pre-token behaviour — never a room where nothing attacks.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttackTokenBroker : MonoBehaviour
    {
        [SerializeField, Tooltip("How many enemies may be mid-melee-attack at once, across the whole room.")]
        int meleeCap = 2;

        [SerializeField, Tooltip("How many may be mid-ranged-attack at once. Counted separately because two archers plus two rushers is legible where four rushers is a pile-on.")]
        int rangedCap = 2;

        public static AttackTokenBroker Current { get; private set; }

        public AttackTokenPool Pool { get; private set; }

        void Awake()
        {
            Pool = new AttackTokenPool(meleeCap, rangedCap);
        }

        void OnEnable()
        {
            if (Current != null && Current != this)
            {
                GameLog.Warn(LogCategory.Enemy,
                    $"a second {nameof(AttackTokenBroker)} appeared on '{name}' - the newest one wins, " +
                    "but two brokers means two independent caps and neither is the room's");
            }

            Current = this;
        }

        void OnDisable()
        {
            if (Current == this)
                Current = null;

            Pool?.ReleaseAll();
        }

        /// <summary>
        /// Asks the active broker for permission. Static so a controller does not have to hold a
        /// reference, and permissive when there is no broker at all.
        /// </summary>
        public static bool TryAcquire(object holder, AttackTokenKind kind, string groupId, int groupCap)
        {
            AttackTokenPool pool = Current != null ? Current.Pool : null;
            return pool == null || pool.TryAcquire(holder, kind, groupId, groupCap);
        }

        /// <summary>Non-mutating check, so a brain can treat "no token" like any other cooldown.</summary>
        public static bool CanAcquire(object holder, AttackTokenKind kind, string groupId, int groupCap)
        {
            AttackTokenPool pool = Current != null ? Current.Pool : null;
            return pool == null || pool.CanAcquire(holder, kind, groupId, groupCap);
        }

        public static void Release(object holder)
        {
            AttackTokenPool pool = Current != null ? Current.Pool : null;
            pool?.Release(holder);
        }

        /// <summary>Live counts, for the debug overlay — a leaked token is otherwise invisible.</summary>
        public static string DescribeUsage()
        {
            AttackTokenPool pool = Current != null ? Current.Pool : null;
            return pool == null
                ? "tokens: no broker (uncapped)"
                : $"tokens: melee {pool.ActiveMelee}/{pool.MeleeCap}  ranged {pool.ActiveRanged}/{pool.RangedCap}";
        }
    }
}
