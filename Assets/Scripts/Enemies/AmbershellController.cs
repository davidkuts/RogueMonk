using Game.Combat;
using Game.Core.Diagnostics;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// The ankylosaur: the biome's Armored showcase, and a positioning tax rather than a killer.
    ///
    /// <para>ENEMIES_BIOME1.md § 3.1 is blunt about its role — "the Ambershell never kills you. The
    /// Swiftjaws that catch you mid-circle do." It teaches that <em>where</em> you hit matters,
    /// which is why almost all of it is zones rather than behaviour: the plating is
    /// <see cref="DamageZone"/> colliders baked from its capsule recipe, and the base controller
    /// already routes every hit through them.</para>
    ///
    /// <para>Two things are its own. Its charge cracks its <em>own</em> shell against a wall rather
    /// than merely stunning it — the wall bait is how the player opens a body that otherwise has no
    /// opening. And it answers being camped: standing under it long enough provokes a delayed
    /// self-slam, which is the anti-degenerate-strategy valve, not a damage move.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AmbershellController : ChargingEnemyController
    {
        [Header("Wall crack")]
        [SerializeField, Tooltip("How long the plating stays open after a wall-baited roll. ENEMIES_BIOME1.md 3.1 asks for 8s, then it re-hardens on its own.")]
        float crackedSeconds = 8f;

        [SerializeField, Tooltip("Dazed window on the slam itself, on top of the cracked plating. The plating is the reward; this is the opening to use it.")]
        float wallSlamStunSeconds = 1.6f;

        [Header("Anti-camp stomp")]
        [SerializeField, Tooltip("Move id of the compression stomp. Chosen out of turn when the player camps, never in the ordinary rotation.")]
        string stompMoveId = "CompressionStomp";

        [SerializeField, Tooltip("Inside this radius the player counts as camping. Roughly its own footprint — the underside.")]
        float campRadius = 2.2f;

        [SerializeField, Tooltip("Seconds of continuous camping before it answers. Long enough that passing through is free.")]
        float campToleranceSeconds = 2.5f;

        float campTimer;

        /// <summary>How long the player has been camped underneath, for tuning and tests.</summary>
        public float CampTimer => campTimer;

        /// <summary>True while at least one amber plate is still up.</summary>
        public bool IsPlated => actor != null && actor.HasIntactArmorZone;

        protected override void Update()
        {
            base.Update();
            TickCampTimer();
        }

        /// <summary>
        /// Cracks the whole shell open.
        ///
        /// <para>Every plate, not just the one that touched the wall. The player earned the opening
        /// with a manoeuvre — baiting a committed roll into geometry — and making them then hunt
        /// for which specific plate happened to make contact would turn a positioning reward into
        /// an aiming problem.</para>
        /// </summary>
        protected override void OnWallSlam()
        {
            GameLog.Info(LogCategory.Enemy,
                $"SHELL CRACKED {Definition.Id} - plating open {crackedSeconds:0.0}s, dazed {wallSlamStunSeconds:0.0}s");

            actor.CrackArmorZones(crackedSeconds);
            actor.ApplyStagger(wallSlamStunSeconds);
        }

        /// <summary>
        /// Watches for the player parking under it.
        ///
        /// <para>§ 3.1 calls the stomp an "anti-degenerate-strategy valve", and that is exactly what
        /// it is: a body whose soft spots are its underside and tail base invites the player to
        /// stand in one place and grind, which is neither interesting nor readable. The tolerance
        /// is deliberately generous so that <em>passing</em> underneath — which is how you reach
        /// the soft zones at all — never provokes it.</para>
        /// </summary>
        void TickCampTimer()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f || !actor.IsAlive || target == null)
                return;

            // A staggered body is not policing anything, and the timer must not run while the
            // player is taking the punish window the design just handed them.
            if (actor.IsStaggered || attacks.IsAttacking)
            {
                campTimer = 0f;
                return;
            }

            Vector3 delta = target.position - transform.position;
            delta.y = 0f;

            if (delta.sqrMagnitude > campRadius * campRadius)
            {
                campTimer = 0f;
                return;
            }

            campTimer += deltaTime;

            if (campTimer < campToleranceSeconds)
                return;

            // Asked for rather than rolled. The stomp carries selection weight 0 so it can never
            // appear in the ordinary rotation — seeing it must always mean "I overstayed".
            if (Brain.RequestMove(stompMoveId))
            {
                GameLog.Info(LogCategory.Enemy,
                    $"{Definition.Id} answers camping after {campTimer:0.0}s - compression stomp requested");
                campTimer = 0f;
            }
        }

        /// <summary>
        /// The stomp is thrown when the camp timer expires, and is otherwise excluded from the
        /// rotation entirely — the same reasoning as the boss's retaliations. If it turned up on a
        /// weighted draw the player could never learn that seeing it means "I overstayed"; it would
        /// just be another move.
        /// </summary>
        protected override void OnMoveChosen(IEnemyMove move)
        {
            base.OnMoveChosen(move);

            if (move.Id == stompMoveId)
                campTimer = 0f;
        }

        /// <summary>True when the player has overstayed and the stomp is owed.</summary>
        public bool StompOwed => campTimer >= campToleranceSeconds;
    }
}
