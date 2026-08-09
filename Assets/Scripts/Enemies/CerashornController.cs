using Game.Core.Diagnostics;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// The juvenile ceratopsian: a charge you beat by standing somewhere else.
    ///
    /// <para>Almost all of it is data. "Cannot steer once committed" is the base controller's
    /// existing rule that facing locks at commit and an attacking body travels on its lunge
    /// velocity — a long lunge over a long active window <em>is</em> a line-locked charge, so no
    /// bespoke movement mode was needed. The charge physics themselves are shared with Ambershell
    /// in <see cref="ChargingEnemyController"/>.</para>
    ///
    /// <para>What is left here is the one thing that is Cerashorn's alone: running into a wall
    /// hurts it. ENEMIES_BIOME1.md § 2.2 gives the player three answers — sidestep and it slams and
    /// self-stuns, dash through it for the Split Second, or bait it into the rest of the room — and
    /// the third is explicitly "intended, encouraged, fun".</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CerashornController : ChargingEnemyController
    {
        [Header("Wall slam")]
        [SerializeField, Tooltip("Self-stun after running into a wall. ENEMIES_BIOME1.md 2.2 asks for 1.5s — this is the punish window a sidestep earns.")]
        float wallSlamStunSeconds = 1.5f;

        protected override void OnWallSlam()
        {
            GameLog.Info(LogCategory.Enemy,
                $"WALL SLAM {Definition.Id} - self-stunned {wallSlamStunSeconds:0.0}s (punish window open)");

            // Goes through the ordinary stagger path, so the interrupt, the colour change, the
            // status and the brain's recovery are the same ones a poise break produces. A bespoke
            // "dazed" state would be a second thing that means the same thing.
            actor.ApplyStagger(wallSlamStunSeconds);
        }
    }
}
