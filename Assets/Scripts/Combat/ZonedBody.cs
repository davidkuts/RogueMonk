using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Marks a body whose damage is decided entirely by <see cref="DamageZone"/> colliders.
    ///
    /// <para>It exists to close a hole that would otherwise make armour meaningless. An enemy body
    /// carries its own collider — the CharacterController — as well as its plates, and a hitbox
    /// query returns whichever it happens to find first. Since a hit is deduplicated per
    /// <see cref="IDamageable"/>, landing on the root capsule would resolve as an <em>unzoned</em>
    /// hit at full damage: the player could bypass an Ambershell's plating by standing anywhere at
    /// all, and "where you hit matters" would be a lie that only sometimes showed up.</para>
    ///
    /// <para>With this present, non-zone colliders on the body are skipped, so the only way to
    /// damage it is to hit a plate or a soft spot. Bodies without it are unaffected — every
    /// existing enemy keeps being hittable exactly as before.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ZonedBody : MonoBehaviour
    {
    }
}
