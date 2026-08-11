using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// The one asset saying what each <see cref="DamageType"/> looks like.
    ///
    /// <para>Exists for the reason <see cref="TelegraphPalette"/> exists: a hue that means
    /// something must have exactly one definition. The hit spark and the floating damage number
    /// describe the same hit, and if each carried its own idea of "Fire" they would eventually
    /// disagree — which reads to the player as two different things happening.</para>
    ///
    /// <para>Defaults are the values the sparks shipped with in M12, so adopting this asset
    /// changes nothing on screen until someone deliberately retunes it.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Monk/Damage Type Palette", fileName = "DamageTypePalette")]
    public sealed class DamageTypePalette : ScriptableObject
    {
        [SerializeField, Tooltip("Ordinary damage with no element behind it. Light hits.")]
        Color physicalLight = new Color(1f, 0.85f, 0.45f, 0.9f);

        [SerializeField, Tooltip("Ordinary damage on a heavy hit — anything whose hitstop clears the heavy threshold.")]
        Color physicalHeavy = new Color(1f, 0.55f, 0.25f, 1f);

        [SerializeField, Tooltip("The Hadean.")] Color fire = new Color(1f, 0.45f, 0.15f, 1f);
        [SerializeField, Tooltip("The Long Winter.")] Color ice = new Color(0.45f, 0.8f, 1f, 1f);
        [SerializeField, Tooltip("The First Breath.")] Color wind = new Color(0.7f, 0.95f, 0.9f, 1f);
        [SerializeField, Tooltip("The Bedrock.")] Color earth = new Color(0.85f, 0.7f, 0.45f, 1f);
        [SerializeField, Tooltip("The Green Reach.")] Color nature = new Color(0.5f, 0.85f, 0.35f, 1f);
        [SerializeField, Tooltip("The Unspent.")] Color force = new Color(1f, 0.85f, 0.35f, 1f);

        [Header("Player-side")]
        [SerializeField, Tooltip("Damage the PLAYER takes. Deliberately outside the elemental set — whose health is moving matters more than what moved it.")]
        Color playerDamage = new Color(1f, 0.32f, 0.30f, 1f);

        [SerializeField, Tooltip("A hit an amber guard turned away. Drawn as a zero, in the hardened-time hue.")]
        Color refused = new Color(1f, 0.67f, 0.13f, 1f);

        /// <summary>Damage the player takes.</summary>
        public Color PlayerDamage => playerDamage;

        /// <summary>A hit refused by a damage gate.</summary>
        public Color Refused => refused;

        /// <summary>
        /// The colour for <paramref name="type"/>. Physical falls through to the light/heavy pair,
        /// because "how hard was that" is the only thing a Physical hit has to say.
        /// </summary>
        public Color Resolve(DamageType type, bool heavy = false)
        {
            switch (type)
            {
                case DamageType.Fire: return fire;
                case DamageType.Ice: return ice;
                case DamageType.Wind: return wind;
                case DamageType.Earth: return earth;
                case DamageType.Nature: return nature;
                case DamageType.Force: return force;
                default: return heavy ? physicalHeavy : physicalLight;
            }
        }

        /// <summary>
        /// Resolves through <paramref name="palette"/> when there is one, and falls back to
        /// <paramref name="authored"/> when there is not, so a prefab with the slot left empty
        /// still draws something rather than black.
        /// </summary>
        public static Color Resolve(DamageTypePalette palette, DamageType type, bool heavy, Color authored) =>
            palette != null ? palette.Resolve(type, heavy) : authored;
    }
}
