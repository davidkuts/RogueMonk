using Game.Combat;
using Game.Core.Audio;
using Game.Core.Diagnostics;
using Game.Core.Feedback;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// The visual for solidified time on a body: the amber shell a riposte-gated elite wears, the
    /// flash when it turns a hit away, and the shatter when the counter finally breaks it.
    ///
    /// <para>Built because the damage gate had no visual at all. A guarded Ambershell looked
    /// identical to any other enemy and the only tells were a health bar that refused to move and
    /// a line in the console — a mechanic the player cannot see, in a game whose promise is
    /// read-and-react. DESIGN.md § Telegraph grammar already reserved amber for exactly this
    /// ("solidified time... the player's rule is 'that blocks stagger or it blocks movement'"), so
    /// the hue is not a choice made here; it is read from the palette.</para>
    ///
    /// <para>The shell also answers <see cref="EnemyActor.PullResisted"/>, which has been raised
    /// with nothing listening since M13 — DESIGN.md § Vortex asks for the failed drag to flare
    /// gold so the tier stays readable in the one frame the player has to read it. That flare is
    /// deliberately independent of the guard: what resists a pull is amber, and a plain Armored
    /// body has amber without having a guard.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AmberSheen : MonoBehaviour
    {
        [SerializeField, Tooltip("The body this sheen wraps. Found on this GameObject when left empty.")]
        EnemyActor actor;

        [SerializeField, Tooltip("Where the amber hue comes from. Never author a colour here — the grammar is enforced in one asset.")]
        TelegraphPalette palette;

        [SerializeField, Tooltip("Transparent shell material. Monk/Ghost: unlit, rim-boosted at the silhouette so the shell reads as a skin over the body rather than a blob covering it.")]
        Material sheenMaterial;

        [Header("Shell")]
        [SerializeField, Tooltip("How far the shell stands off the body. Enough to read as a coating, not enough to change the silhouette.")]
        float shellScale = 1.06f;
        [SerializeField, Range(0f, 1f), Tooltip("Resting opacity of the shell.")]
        float baseAlpha = 0.45f;
        [SerializeField, Tooltip("Breathing rate of the intact shell. Slow — a fast pulse is the telegraph's language, and the guard is a state rather than a warning.")]
        float pulseHz = 0.8f;
        [SerializeField, Range(0f, 1f), Tooltip("How much of the opacity the pulse swings.")]
        float pulseDepth = 0.3f;

        [Header("Refusal")]
        [SerializeField, Tooltip("How long the shell flares when it turns a hit away.")]
        float refuseFlashSeconds = 0.12f;
        [SerializeField, Tooltip("Opacity the refusal flash reaches. This is the 'that did nothing' read, so it has to beat the resting shell clearly.")]
        float refuseFlashAlpha = 0.95f;

        [Header("Shatter")]
        [SerializeField, Tooltip("Length of the break. Runs on the unscaled clock, so the Riposte's hitstop holds its first frame instead of eating the whole thing.")]
        float shatterSeconds = 0.3f;
        [SerializeField, Tooltip("Scale the shell blows out to as it dies.")]
        float shatterEndScale = 1.9f;
        [SerializeField, Tooltip("Shards thrown by the break.")]
        int shardCount = 12;
        [SerializeField, Tooltip("How fast shards leave the body.")]
        float shardSpeed = 4.5f;
        [SerializeField, Tooltip("Edge length of a shard cube.")]
        float shardSize = 0.14f;
        [SerializeField, Tooltip("Height the shard ring is thrown from, relative to the body's feet.")]
        float shardHeight = 0.9f;

        [Header("Pull resistance")]
        [SerializeField, Tooltip("How long the shell flares when amber refuses to be dragged.")]
        float resistFlashSeconds = 0.2f;
        [SerializeField, Tooltip("Gold, per DESIGN.md's 'the failed drag flares gold'. Distinct from the amber resting hue so 'it held' and 'it is guarded' are two readings.")]
        Color resistFlareColor = new Color(1f, 0.85f, 0.35f, 1f);

        static readonly int GhostColorId = Shader.PropertyToID("_GhostColor");

        Transform shell;
        Renderer[] shellRenderers;
        MaterialPropertyBlock block;
        Vector3 shellRestScale;

        Transform[] shards;
        Vector3[] shardVelocities;

        float refuseRemaining;
        float resistRemaining;
        float shatterRemaining;
        bool guardRetired;

        void Awake()
        {
            if (actor == null)
                actor = GetComponent<EnemyActor>();

            if (actor == null)
            {
                GameLog.Warn(LogCategory.Enemy, $"{name} has an {nameof(AmberSheen)} with no {nameof(EnemyActor)} - no amber will show");
                enabled = false;
                return;
            }

            block = new MaterialPropertyBlock();
            BuildShell();
            BuildShards();

            actor.HitRefused += OnHitRefused;
            actor.GuardBroken += OnGuardBroken;
            actor.PullResisted += OnPullResisted;
        }

        void OnDestroy()
        {
            if (actor != null)
            {
                actor.HitRefused -= OnHitRefused;
                actor.GuardBroken -= OnGuardBroken;
                actor.PullResisted -= OnPullResisted;
            }

            if (shell != null)
                Destroy(shell.gameObject);

            if (shards == null)
                return;

            for (int i = 0; i < shards.Length; i++)
            {
                if (shards[i] != null)
                    Destroy(shards[i].gameObject);
            }
        }

        /// <summary>
        /// Clones the body once and turns the copy into a transparent shell.
        ///
        /// <para>Cloning rather than authoring a second prefab is the same trick — and the same
        /// reasoning — as <see cref="EchoPlayback.BuildGhost"/>: the shell is guaranteed to match
        /// whatever the body currently looks like, including after the capsule is swapped for a
        /// mesh, so this file does not change when the art lands.</para>
        /// </summary>
        void BuildShell()
        {
            Transform body = transform.Find("Body");
            if (body == null)
            {
                GameLog.Warn(LogCategory.Enemy, $"{name} has no Body to coat - the amber sheen will be invisible");
                return;
            }

            GameObject clone = Instantiate(body.gameObject, body.parent);
            clone.name = "AmberSheen";
            shell = clone.transform;

            shell.SetLocalPositionAndRotation(body.localPosition, body.localRotation);
            shellRestScale = body.localScale * shellScale;
            shell.localScale = shellRestScale;

            // The shell is a coating, not a participant: nothing on it may be hit, block a hit, or
            // report itself as a damage zone.
            //
            // DestroyImmediate, not Destroy, and that is load-bearing. Ordinary Destroy defers to
            // the end of the frame, and EnemyActor.Awake enumerates zones with includeInactive —
            // so if this Awake happened to run first, the body would capture a plate list holding
            // six phantom copies of its own armour that go null moments later. Awake order between
            // components is undefined, so the clone has to be inert before this method returns.
            foreach (Collider collider in clone.GetComponentsInChildren<Collider>(true))
                DestroyImmediate(collider);
            foreach (DamageZone zone in clone.GetComponentsInChildren<DamageZone>(true))
                DestroyImmediate(zone);

            shellRenderers = clone.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < shellRenderers.Length; i++)
            {
                if (sheenMaterial != null)
                    shellRenderers[i].sharedMaterial = sheenMaterial;

                shellRenderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                shellRenderers[i].receiveShadows = false;
            }

            // Deactivated before this method returns, and that matters beyond tidiness:
            // EnemyActor.Awake caches GetComponentsInChildren<Renderer>() without inactive
            // objects, so whichever Awake runs first, the shell is either not built yet or
            // already hidden — the body's tint stack can never pick up its own coating.
            clone.SetActive(false);
        }

        void BuildShards()
        {
            if (shardCount <= 0)
                return;

            shards = new Transform[shardCount];
            shardVelocities = new Vector3[shardCount];

            for (int i = 0; i < shardCount; i++)
            {
                var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.name = $"AmberShard_{i}";
                Destroy(shard.GetComponent<Collider>());
                shard.transform.localScale = Vector3.one * shardSize;

                var renderer = shard.GetComponent<Renderer>();
                if (sheenMaterial != null)
                    renderer.sharedMaterial = sheenMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                shard.SetActive(false);
                shards[i] = shard.transform;
            }
        }

        Color AmberColor =>
            TelegraphPalette.Resolve(palette, TelegraphChannel.HardenedTime, new Color(1f, 0.67f, 0.13f, 1f));

        void OnHitRefused()
        {
            refuseRemaining = refuseFlashSeconds;
            AudioDirector.PlaySound(GameSound.GuardRefused);
        }

        void OnPullResisted() => resistRemaining = resistFlashSeconds;

        void OnGuardBroken()
        {
            guardRetired = true;
            shatterRemaining = shatterSeconds;
            refuseRemaining = 0f;

            AudioDirector.PlaySound(GameSound.GuardBreak);
            RumbleDirector.Rumble(0.8f, 0.9f);
            ThrowShards();
        }

        /// <summary>
        /// Throws the shard ring.
        ///
        /// <para>Positions are a fixed ring rather than a random scatter, following the same rule
        /// the kill fragments follow (M16.1): cosmetics must never draw from the run RNG, or simply
        /// seeing more effects would change the level.</para>
        /// </summary>
        void ThrowShards()
        {
            if (shards == null)
                return;

            Vector3 origin = transform.position + Vector3.up * shardHeight;

            for (int i = 0; i < shards.Length; i++)
            {
                if (shards[i] == null)
                    continue;

                float angle = i * (Mathf.PI * 2f / shards.Length);
                // Alternating pitch so the burst has volume instead of reading as a flat disc.
                float rise = (i % 2 == 0) ? 0.55f : 0.2f;
                var direction = new Vector3(Mathf.Cos(angle), rise, Mathf.Sin(angle)).normalized;

                // Left at the scene root on purpose: shards are debris the body has stopped
                // owning, so they must not drag along behind an enemy that keeps walking.
                shards[i].position = origin + direction * 0.3f;
                shards[i].localScale = Vector3.one * shardSize;
                shards[i].gameObject.SetActive(true);
                shardVelocities[i] = direction * shardSpeed;
            }
        }

        /// <summary>
        /// Runs on the unscaled clock throughout, for the reason <see cref="Game.Combat.HitSpark"/>
        /// documents: the breaking Riposte triggers hitstop, and a shatter on the scaled clock
        /// would freeze on its first frame for exactly the moment it exists to sell.
        /// </summary>
        void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;

            if (refuseRemaining > 0f) refuseRemaining -= deltaTime;
            if (resistRemaining > 0f) resistRemaining -= deltaTime;
            if (shatterRemaining > 0f) shatterRemaining -= deltaTime;

            TickShards(deltaTime);
            ApplyShell();
        }

        void TickShards(float deltaTime)
        {
            if (shards == null || shatterSeconds <= 0f)
                return;

            float t = 1f - Mathf.Clamp01(shatterRemaining / shatterSeconds);
            bool finished = shatterRemaining <= 0f;

            for (int i = 0; i < shards.Length; i++)
            {
                if (shards[i] == null || !shards[i].gameObject.activeSelf)
                    continue;

                if (finished)
                {
                    shards[i].gameObject.SetActive(false);
                    continue;
                }

                shardVelocities[i] += Vector3.down * (9.8f * deltaTime);
                shards[i].position += shardVelocities[i] * deltaTime;
                shards[i].Rotate(180f * deltaTime, 240f * deltaTime, 0f, Space.Self);
                shards[i].localScale = Vector3.one * (shardSize * (1f - t));
            }
        }

        void ApplyShell()
        {
            if (shell == null)
                return;

            bool dead = actor == null || !actor.IsAlive || actor.IsDying;
            bool guarded = !guardRetired && !dead && actor != null && actor.IsGuarded;
            bool shattering = shatterRemaining > 0f;
            bool resisting = resistRemaining > 0f && !dead;

            if (!guarded && !shattering && !resisting)
            {
                if (shell.gameObject.activeSelf)
                    shell.gameObject.SetActive(false);
                return;
            }

            if (!shell.gameObject.activeSelf)
                shell.gameObject.SetActive(true);

            Color color = AmberColor;
            float alpha;

            if (shattering)
            {
                // Blows outward and fades: the amber comes apart rather than switching off.
                float t = 1f - Mathf.Clamp01(shatterRemaining / Mathf.Max(0.0001f, shatterSeconds));
                shell.localScale = shellRestScale * Mathf.Lerp(1f, shatterEndScale, t);
                alpha = Mathf.Lerp(refuseFlashAlpha, 0f, t);
            }
            else
            {
                shell.localScale = shellRestScale;

                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseHz * Mathf.PI * 2f);
                alpha = baseAlpha * Mathf.Lerp(1f - pulseDepth, 1f, pulse);

                if (refuseRemaining > 0f && refuseFlashSeconds > 0f)
                {
                    float flash = Mathf.Clamp01(refuseRemaining / refuseFlashSeconds);
                    alpha = Mathf.Lerp(alpha, refuseFlashAlpha, flash);
                }

                if (resisting && resistFlashSeconds > 0f)
                {
                    float flare = Mathf.Clamp01(resistRemaining / resistFlashSeconds);
                    color = Color.Lerp(color, resistFlareColor, flare);
                    alpha = Mathf.Max(alpha, refuseFlashAlpha * flare);
                }
            }

            color.a = alpha;
            Tint(color);
        }

        void Tint(Color color)
        {
            if (shellRenderers == null)
                return;

            for (int i = 0; i < shellRenderers.Length; i++)
            {
                if (shellRenderers[i] == null)
                    continue;

                shellRenderers[i].GetPropertyBlock(block);
                block.SetColor(GhostColorId, color);
                shellRenderers[i].SetPropertyBlock(block);
            }

            if (shards == null)
                return;

            Color shardColor = color;
            shardColor.a = Mathf.Max(color.a, 0.85f);
            for (int i = 0; i < shards.Length; i++)
            {
                if (shards[i] == null || !shards[i].gameObject.activeSelf)
                    continue;

                var renderer = shards[i].GetComponent<Renderer>();
                renderer.GetPropertyBlock(block);
                block.SetColor(GhostColorId, shardColor);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
