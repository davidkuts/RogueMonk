using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Draws the Undertow's ACTUAL pull radius as a ground ring in the reserved dash hue while
    /// the spin runs. The foot-traced smear sells the kick but sweeps only a leg's length, so
    /// on its own it under-promised the range by metres — this ring is the honest circle, and
    /// like the enemy ground decals it can never lie because it reads the same radius the
    /// overlap query uses. Presentation only; switching it off changes nothing about play.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VortexRangeRing : MonoBehaviour
    {
        [SerializeField] PlayerVortex vortex;
        [SerializeField, Tooltip("Material for the ring. Monk/Smear keeps it in the same family as the ribbons; null builds a URP Unlit fallback.")]
        Material ringMaterial;

        [Header("Look")]
        [SerializeField, Tooltip("The reserved dash hue, matching the smear and the Blink.")]
        Color ringColor = new Color(0.29f, 0.85f, 0.92f, 0.55f);
        [SerializeField, Tooltip("Line width at full presence.")]
        float ringWidth = 0.14f;
        [SerializeField, Tooltip("Height above the feet, so the ring never z-fights the floor.")]
        float groundOffset = 0.06f;
        [SerializeField, Range(8, 96)] int segments = 56;

        [Header("Feel")]
        [SerializeField, Tooltip("How fast the ring sweeps out from the inner ring to the full radius when the spin starts.")]
        float expandSeconds = 0.12f;
        [SerializeField, Tooltip("How fast the ring dies once the spin ends. Width fades to nothing, so it works on an opaque material too.")]
        float fadeSeconds = 0.2f;

        LineRenderer line;
        float presence;

        void Awake()
        {
            if (vortex == null)
                vortex = GetComponent<PlayerVortex>();

            if (vortex == null)
            {
                enabled = false;
                return;
            }

            var go = new GameObject("VortexRangeRing");
            go.transform.SetParent(transform, false);

            line = go.AddComponent<LineRenderer>();
            line.loop = true;
            line.positionCount = segments;
            line.useWorldSpace = false;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.startColor = ringColor;
            line.endColor = ringColor;

            if (ringMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader != null)
                {
                    ringMaterial = new Material(shader);
                    ringMaterial.SetColor(Shader.PropertyToID("_BaseColor"), ringColor);
                }
            }

            if (ringMaterial != null)
                line.sharedMaterial = ringMaterial;

            line.enabled = false;
        }

        void LateUpdate()
        {
            if (line == null || vortex.Definition == null)
                return;

            float deltaTime = Time.deltaTime;
            bool spinning = vortex.IsSpinning;

            presence = spinning
                ? Mathf.Min(1f, presence + (expandSeconds > 0f ? deltaTime / expandSeconds : 1f))
                : Mathf.Max(0f, presence - (fadeSeconds > 0f ? deltaTime / fadeSeconds : 1f));

            if (presence <= 0f)
            {
                line.enabled = false;
                return;
            }

            line.enabled = true;

            // Sweep out from the delivery ring to the true reach, then die by thinning — width
            // carries the fade so an opaque material fades just as well as a transparent one.
            float radius = Mathf.Lerp(vortex.Definition.InnerRingMeters, vortex.Definition.RadiusMeters, presence);
            float width = ringWidth * presence;
            line.startWidth = width;
            line.endWidth = width;

            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                line.SetPosition(i, new Vector3(
                    Mathf.Cos(angle) * radius, groundOffset, Mathf.Sin(angle) * radius));
            }
        }
    }
}
