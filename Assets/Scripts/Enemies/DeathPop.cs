using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// A burst of fragments on death, in the body's own identity colour.
    ///
    /// <para>ASSETS_BIOME1.md § 4.4 specifies the Scrapfeather death as <c>[C]</c> — "particle
    /// burst + mesh despawn, no death animation" — and that is deliberate: the swarm exists to be
    /// killed in numbers, so it must never spend a clip or a second dying. Before this they simply
    /// vanished, which reads as a bug rather than a kill.</para>
    ///
    /// <para>Works on anything, not just the swarm: an enemy with a real death beat gets the pop
    /// on top of it, on the frame the killing blow lands.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeathPop : MonoBehaviour
    {
        [SerializeField, Tooltip("The body whose death this answers. Found on this GameObject when left empty.")]
        EnemyActor actor;

        [SerializeField, Tooltip("Material for the fragments. Monk/Ghost keeps them unlit and bright against the muted floor.")]
        Material pieceMaterial;

        [SerializeField, Tooltip("Fragments thrown. Small counts read as a pop; large ones read as an explosion, which is the boss's language.")]
        int pieceCount = 8;

        [SerializeField, Tooltip("Edge length of a fragment cube.")]
        float pieceSize = 0.13f;

        [SerializeField, Tooltip("How fast fragments leave the body.")]
        float speed = 3.2f;

        [SerializeField, Tooltip("How long a fragment lives before it has shrunk and faded away.")]
        float lifetimeSeconds = 0.45f;

        [SerializeField, Tooltip("Height the burst is thrown from, relative to the body's feet.")]
        float burstHeight = 0.45f;

        [SerializeField, Tooltip("Leave black to use the body's own identity colour from its CapsuleRecipe — a kill should look like the thing that died.")]
        Color overrideColor = Color.clear;

        DeathPopPiece[] pieces;
        Color tint = Color.white;

        void Awake()
        {
            if (actor == null)
                actor = GetComponent<EnemyActor>();

            var capsule = GetComponent<CapsuleBody>();
            tint = overrideColor.a > 0f ? overrideColor
                : capsule != null ? capsule.IdentityColor
                : Color.white;
            tint.a = 1f;

            BuildPieces();

            if (actor != null)
                actor.DeathSequenceStarted += Pop;
        }

        void OnDestroy()
        {
            if (actor != null)
                actor.DeathSequenceStarted -= Pop;

            if (pieces == null)
                return;

            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] != null)
                    Destroy(pieces[i].gameObject);
            }
        }

        void BuildPieces()
        {
            if (pieceCount <= 0)
                return;

            pieces = new DeathPopPiece[pieceCount];
            for (int i = 0; i < pieceCount; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"{name}_DeathPiece_{i}";
                Destroy(go.GetComponent<Collider>());

                var renderer = go.GetComponent<Renderer>();
                if (pieceMaterial != null)
                    renderer.sharedMaterial = pieceMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                pieces[i] = go.AddComponent<DeathPopPiece>();
                go.SetActive(false);
            }
        }

        /// <summary>
        /// Throws the burst on a fixed ring. Not a random scatter: cosmetic draws must never come
        /// from the run RNG, so a seed reproduces the same fight however many things die in it.
        /// </summary>
        void Pop()
        {
            if (pieces == null)
                return;

            Vector3 origin = transform.position + Vector3.up * burstHeight;

            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] == null)
                    continue;

                float angle = i * (Mathf.PI * 2f / pieces.Length);
                float rise = (i % 3 == 0) ? 0.9f : 0.35f;
                var direction = new Vector3(Mathf.Cos(angle), rise, Mathf.Sin(angle)).normalized;

                pieces[i].Play(origin + direction * 0.15f, direction * speed, tint, pieceSize, lifetimeSeconds);
            }
        }
    }
}
