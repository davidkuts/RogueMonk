using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Turns the collider an attack overlapped into the <see cref="HitZone"/> that describes it,
    /// in one place — the same reason <see cref="HitboxQuery"/> exists.
    ///
    /// Every attacker in the game already holds the exact collider it struck and then throws it
    /// away, keeping only the <see cref="IDamageable"/> above it. That is the information a
    /// zoned body needs, so this is the one line each attacker adds to keep it.
    /// </summary>
    public static class HitZones
    {
        /// <summary>
        /// Describes <paramref name="collider"/>. Anything without a <see cref="DamageZone"/> is
        /// ordinary flesh and reports the neutral zone.
        ///
        /// <para>Deliberately <c>GetComponent</c> and not <c>GetComponentInParent</c>: a zone
        /// belongs to <em>its own</em> collider. Walking up the hierarchy would let one plate on
        /// the root silently armour every other collider on the body, which is the exact bug that
        /// makes a soft spot stop being soft.</para>
        /// </summary>
        public static HitZone Resolve(Collider collider)
        {
            if (collider == null)
                return default;

            return collider.TryGetComponent(out DamageZone zone) ? zone.Describe() : default;
        }
    }
}
