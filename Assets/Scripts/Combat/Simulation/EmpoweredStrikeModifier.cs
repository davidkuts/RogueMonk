using System;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Charges exactly one hit, then spends itself.
    ///
    /// This is the first real user of the modifier pipeline, which has been architected and tested
    /// but empty since M3 — and it is deliberately shaped the way an elemental boon will be, so the
    /// seam gets exercised by something real before boons arrive.
    ///
    /// Engine-free and armed for a bounded window, so a charge earned by a perfect dodge cannot be
    /// carried across a room and cashed in on an unrelated fight.
    /// </summary>
    public sealed class EmpoweredStrikeModifier : IHitModifier
    {
        readonly float damageMultiplier;
        readonly float hitstopBonus;
        readonly float knockbackMultiplier;

        float remaining;

        public EmpoweredStrikeModifier(float damageMultiplier, float hitstopBonus, float knockbackMultiplier)
        {
            this.damageMultiplier = Mathf.Max(1f, damageMultiplier);
            this.hitstopBonus = Mathf.Max(0f, hitstopBonus);
            this.knockbackMultiplier = Mathf.Max(1f, knockbackMultiplier);
        }

        /// <summary>
        /// Runs late, so it multiplies whatever earlier stages settled on rather than being scaled
        /// by them. A boon that halves damage should halve the empowered number too, not the other
        /// way round.
        /// </summary>
        public int Order => 100;

        public bool IsArmed => remaining > 0f;

        public float Remaining => remaining;

        /// <summary>Raised when the charge is spent on a hit, and when it expires unused.</summary>
        public event Action<bool> Resolved;

        public void Arm(float windowSeconds)
        {
            remaining = Mathf.Max(0f, windowSeconds);
        }

        public void Tick(float deltaTime)
        {
            if (remaining <= 0f || deltaTime <= 0f)
                return;

            remaining = Mathf.Max(0f, remaining - deltaTime);
            if (remaining <= 0f)
                Resolved?.Invoke(false);
        }

        public void Modify(ref HitContext context)
        {
            if (remaining <= 0f)
                return;

            context.Damage *= damageMultiplier;
            context.Knockback *= knockbackMultiplier;
            context.HitstopSeconds += hitstopBonus;

            // Spent on the first hit it touches, not on the whole active window: a sweep that
            // catches three enemies should empower one of them, not all three.
            remaining = 0f;
            Resolved?.Invoke(true);
        }
    }
}
