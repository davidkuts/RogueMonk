using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Materialises a piece of debris high in the air and drops it onto this hazard's impact point,
    /// arriving as the hazard goes live.
    ///
    /// <para><b>This exists to obey the project-wide no-ceilings rule</b> (ENEMIES_BIOME1.md § 1,
    /// now recorded in DESIGN.md). Every vertical threat must be <em>sky-sourced</em> — it falls
    /// from above the camera or phases into existence — and <em>ground-telegraphed</em>. The
    /// telegraph is the hazard's own floor decal, which already draws the real hitbox; this adds
    /// only the thing falling into it.</para>
    ///
    /// <para>It is deliberately visual-only. The damage, the timing and the warning all belong to
    /// the <c>FloorHazard</c> this rides on, which runs on the shared attack state machine — so the
    /// junk cannot land early, land late, or hit somewhere the circle did not promise.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkyDropVisual : MonoBehaviour
    {
        [SerializeField, Tooltip("The debris that falls. Visual only — it carries no collider and no damage.")]
        Transform prop;

        [SerializeField, Tooltip("How high above the impact point it materialises. Must start above the camera's view so it reads as coming from the sky rather than from a roof.")]
        float dropHeight = 14f;

        [SerializeField, Tooltip("Seconds of fall. Set this to the hazard's wind-up so the junk lands exactly as the circle fills.")]
        float fallSeconds = 0.9f;

        [SerializeField, Tooltip("Seconds the landed prop lingers before it fades out.")]
        float lingerSeconds = 1.2f;

        [SerializeField, Tooltip("Shimmer as it phases into existence: the prop scales up from this fraction over the first part of the fall.")]
        [Range(0f, 1f)] float materialiseFraction = 0.25f;

        float elapsed;
        Vector3 groundLocal;
        Vector3 propScale;

        void Awake()
        {
            if (prop == null)
                return;

            groundLocal = prop.localPosition;
            propScale = prop.localScale;

            prop.localPosition = groundLocal + Vector3.up * dropHeight;
            prop.localScale = Vector3.zero;
        }

        void Update()
        {
            if (prop == null)
                return;

            // Unscaled: a hazard's wind-up must not stretch in hitstop, or the junk would land
            // after the circle it was promised to. The decal already runs on the same clock.
            elapsed += Time.unscaledDeltaTime;

            float fall = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, fallSeconds));

            // Accelerating, so it reads as falling rather than descending.
            float eased = fall * fall;
            prop.localPosition = groundLocal + Vector3.up * (dropHeight * (1f - eased));

            // Phases in over the first part of the drop: it is arriving from another second, not
            // being dropped off a ledge.
            float materialise = materialiseFraction <= 0f ? 1f : Mathf.Clamp01(fall / materialiseFraction);
            prop.localScale = propScale * materialise;

            if (fall < 1f)
                return;

            float afterLanding = elapsed - fallSeconds;
            if (afterLanding < lingerSeconds)
                return;

            // Shrink away rather than blink out, so a cluster of them clears legibly.
            float fade = Mathf.Clamp01((afterLanding - lingerSeconds) / 0.4f);
            prop.localScale = propScale * (1f - fade);

            if (fade >= 1f)
                prop.gameObject.SetActive(false);
        }
    }
}
