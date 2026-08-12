#ifndef MONK_OCCLUSION_FADE_INCLUDED
#define MONK_OCCLUSION_FADE_INCLUDED

// ---------------------------------------------------------------------------------------------
// Occlusion fade: a dithered hole punched through geometry that is hiding an actor the player has
// to be able to read (DESIGN.md's promise is that every attack can be answered, and a wind-up
// nobody can see cannot be).
//
// Deliberately an ALPHA-CLIP in the opaque pass rather than real transparency. Going transparent
// would mean a render-queue move, sorting against every other transparent effect in the room, and
// a wall that no longer writes depth at all — three new classes of artifact to fix a visibility
// problem. Clipping keeps the wall exactly where it was in the frame and simply removes pixels.
//
// The mask is three terms multiplied:
//   fade    - per-renderer, eased over ~0.2s by OcclusionFadeDirector. 0 for a wall that is not
//             occluding anything, which is what stops a reveal disc from punching a hole in a wall
//             the actor is standing in FRONT of.
//   reveal  - a soft-edged disc around each tracked actor, so only the covering patch dissolves
//             rather than the whole 20 m slab. This is the per-pixel granularity that made
//             splitting the wall meshes unnecessary.
//   depth   - the fragment must be nearer the camera than the actor it is being cut for.
//
// Everything is computed in VIEW space on purpose. Screen-space UVs would drag in render-scale and
// the platform's UV-origin flip, both of which are easy to get subtly wrong and invisible until a
// build; view space has neither, and dividing x/y by depth gives angular coordinates in which a
// constant radius is genuinely a circle on screen.
// ---------------------------------------------------------------------------------------------

#define MONK_MAX_OCCLUSION_REVEALS 8

// Globals, written once per frame by OcclusionFadeDirector. Outside UnityPerMaterial on purpose:
// they are shared by every wall in the scene, and a Shader.SetGlobal value inside the per-material
// CBUFFER breaks the layout contract the SRP batcher relies on.
float4 _OcclusionReveals[MONK_MAX_OCCLUSION_REVEALS];    // xyz = actor view-space position, w = radius
float _OcclusionRevealCount;
float _OcclusionRevealSoftness;

// Ordered 4x4 Bayer. Ordered rather than blue noise or a hashed value because the threshold has to
// be a function of the PIXEL, not of time or of world position: a pattern that reseeds per frame
// crawls, and a pattern anchored to the surface swims as the camera moves. This one is nailed to
// the screen, so a fading wall reads as a stable screen-door instead of static.
static const float MonkBayer4x4[16] =
{
     0.03125, 0.53125, 0.15625, 0.65625,
     0.78125, 0.28125, 0.90625, 0.40625,
     0.21875, 0.71875, 0.09375, 0.59375,
     0.96875, 0.46875, 0.84375, 0.34375
};

float MonkDitherThreshold(float2 positionSS)
{
    uint2 cell = uint2(positionSS) % 4u;
    return MonkBayer4x4[cell.y * 4u + cell.x];
}

/// <summary>
/// How strongly this fragment sits inside some tracked actor's reveal disc: 1 dead centre, 0
/// outside every one of them. Only actors FURTHER from the camera than the fragment count — a wall
/// behind the player is not hiding them and must never dissolve.
/// </summary>
float MonkOcclusionReveal(float3 positionVS)
{
    // View space looks down -Z, so eye distance is the negated component.
    float fragmentDepth = max(0.0001, -positionVS.z);
    float2 fragmentAngular = positionVS.xy / fragmentDepth;

    float reveal = 0.0;
    int count = (int)_OcclusionRevealCount;

    [loop]
    for (int i = 0; i < MONK_MAX_OCCLUSION_REVEALS; i++)
    {
        if (i >= count)
            break;

        float4 actor = _OcclusionReveals[i];
        float actorDepth = max(0.0001, -actor.z);

        if (fragmentDepth >= actorDepth)
            continue;

        float2 actorAngular = actor.xy / actorDepth;
        float distance = length(fragmentAngular - actorAngular);

        reveal = max(reveal, 1.0 - smoothstep(max(0.0, actor.w - _OcclusionRevealSoftness), actor.w, distance));
    }

    return saturate(reveal);
}

/// <summary>
/// Discards the dithered share of this fragment. Called from every pass that writes depth or
/// colour — fill, outline and the depth prepass — from one place so the three cannot drift: an
/// outline that survived its own wall would draw a floating rectangle around the hole, and a depth
/// prepass that kept the wall would hide the actor behind a surface that is no longer on screen.
/// </summary>
void ApplyOcclusionFade(float4 positionHCS, float3 positionVS, float fade, float keepFraction)
{
    if (fade <= 0.001)
        return;

    float amount = fade * MonkOcclusionReveal(positionVS);
    if (amount <= 0.001)
        return;

    // keepFraction is the share of pixels that SURVIVE at full fade, so 0.25 leaves a quarter of
    // the wall standing and the actor reads through the rest.
    float keep = lerp(1.0, keepFraction, amount);
    clip(keep - MonkDitherThreshold(positionHCS.xy));
}

#endif
