using Game.Core.Diagnostics;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Draws the Undertow's spin as ribbons traced by the feet.
    ///
    /// <para>The dash sells itself with whole-body afterimages because the body <em>travels</em>.
    /// A stationary spin does not: the torso stays where it is, so a stack of body ghosts piles up
    /// in the same spot and reads as a blob rather than a rotation. The only part genuinely sweeping
    /// a circle is the leg, so the leg is what gets drawn — which is also how the genre draws spin
    /// kicks, and it makes the arc of the attack legible instead of merely decorative.</para>
    ///
    /// <para>Same reserved dash hue as the Blink: it is the same wrist unit doing it, and the whole
    /// Second Hand kit reads as one colour family. Presentation only — this never touches the
    /// simulation, and switching it off changes nothing about how the ability plays.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VortexSmear : MonoBehaviour
    {
        [SerializeField] PlayerVortex vortex;
        [SerializeField, Tooltip("Bones that trace the smear. Left empty, both feet are found by name — they are what sweeps the circle.")]
        Transform[] emitters = new Transform[0];
        [SerializeField, Tooltip("Material using Monk/Smear.")]
        Material smearMaterial;

        [Header("Ribbon")]
        [SerializeField, Tooltip("How long a point on the trail survives. Roughly the fraction of the circle left hanging in the air behind the foot.")]
        float trailSeconds = 0.28f;
        [SerializeField, Tooltip("Width at the foot.")]
        float startWidth = 0.30f;
        [SerializeField, Tooltip("Width at the tail, where it thins to nothing.")]
        float endWidth = 0.02f;
        [SerializeField, Tooltip("The reserved dash hue. Matches the Blink's afterimages and the HUD pips.")]
        Color smearColor = new Color(0.29f, 0.85f, 0.92f, 0.9f);
        [SerializeField, Tooltip("Distance the foot must move before a new point is laid down. Small, so a fast sweep still draws a smooth arc.")]
        float minVertexDistance = 0.02f;

        TrailRenderer[] trails;
        bool wasSpinning;

        void Awake()
        {
            if (vortex == null)
                vortex = GetComponent<PlayerVortex>();

            if (emitters == null || emitters.Length == 0)
                emitters = FindFeet();

            if (vortex == null || smearMaterial == null || emitters.Length == 0)
            {
                enabled = false;
                return;
            }

            trails = new TrailRenderer[emitters.Length];
            for (int i = 0; i < emitters.Length; i++)
                trails[i] = CreateTrail(emitters[i]);
        }

        /// <summary>Both feet, by bone name. The planted foot pivots and the kicking foot sweeps wide, so the pair self-balances.</summary>
        Transform[] FindFeet()
        {
            var found = new System.Collections.Generic.List<Transform>();
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name.EndsWith("LeftFoot") || t.name.EndsWith("RightFoot"))
                    found.Add(t);
            }

            return found.ToArray();
        }

        TrailRenderer CreateTrail(Transform bone)
        {
            var go = new GameObject("VortexSmear");
            go.transform.SetParent(bone, false);
            go.transform.localPosition = Vector3.zero;

            var trail = go.AddComponent<TrailRenderer>();
            trail.time = trailSeconds;
            trail.minVertexDistance = minVertexDistance;
            trail.autodestruct = false;
            trail.emitting = false;
            trail.sharedMaterial = smearMaterial;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.alignment = LineAlignment.View;
            trail.numCapVertices = 4;

            trail.widthCurve = new AnimationCurve(
                new Keyframe(0f, startWidth),
                new Keyframe(1f, endWidth));

            // Fades out along its length, so the tail dissolves instead of ending in a hard edge.
            trail.colorGradient = new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(smearColor, 0f),
                    new GradientColorKey(smearColor, 1f),
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(smearColor.a, 0f),
                    new GradientAlphaKey(smearColor.a * 0.65f, 0.45f),
                    new GradientAlphaKey(0f, 1f),
                },
            };

            return trail;
        }

        void Update()
        {
            // IsBodySpinning, not IsSpinning: the pull ends with the active window but the body keeps
            // turning through recovery, and a smear that stopped early would cut the arc in half.
            bool spinning = vortex.IsBodySpinning;
            if (spinning == wasSpinning)
                return;

            for (int i = 0; i < trails.Length; i++)
            {
                if (trails[i] == null)
                    continue;

                trails[i].emitting = spinning;

                // Clearing on the way *in* rather than on the way out lets the tail of the last
                // spin fade away naturally instead of vanishing the instant the move ends.
                if (spinning)
                    trails[i].Clear();
            }

            if (spinning)
                GameLog.Debug(LogCategory.Combat, $"vortex smear   {trails.Length} ribbon(s) emitting");

            wasSpinning = spinning;
        }
    }
}
