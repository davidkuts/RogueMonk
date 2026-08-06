using Game.Core.Diagnostics;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// A punching bag for tuning the combo before real enemies exist (M4). Implements the
    /// full <see cref="IDamageable"/> contract — including the status container — so the
    /// resolver is exercised the same way it will be against enemies, but it has no AI,
    /// no poise and never dies.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrainingDummy : MonoBehaviour, IDamageable
    {
        [Header("Reaction")]
        [SerializeField, Tooltip("How long the hit flash lasts.")]
        float flashSeconds = 0.12f;
        [SerializeField] Color flashColor = new Color(1f, 0.35f, 0.25f);
        [SerializeField, Tooltip("Knockback decay rate. Higher = stops sooner.")]
        float knockbackDamping = 9f;
        [SerializeField, Tooltip("How quickly the dummy drifts back to where it started.")]
        float returnSpeed = 1.5f;

        [Header("Debug")]
        [SerializeField, Tooltip("Log every hit's resolved damage to the console.")]
        bool logHits = true;

        Renderer[] renderers;
        MaterialPropertyBlock propertyBlock;
        Color[] baseColors;
        Vector3 spawnPosition;
        Vector3 knockbackVelocity;
        float flashRemaining;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public bool IsAlive => true;

        public StatusEffectContainer Statuses { get; } = new StatusEffectContainer();

        /// <summary>Running total, so a playtest can sanity-check damage numbers.</summary>
        public float TotalDamageTaken { get; private set; }

        /// <summary>Hits landed since the last reset.</summary>
        public int HitCount { get; private set; }

        void Awake()
        {
            spawnPosition = transform.position;
            renderers = GetComponentsInChildren<Renderer>();
            propertyBlock = new MaterialPropertyBlock();
            baseColors = new Color[renderers.Length];

            for (int i = 0; i < renderers.Length; i++)
            {
                Material material = renderers[i].sharedMaterial;
                baseColors[i] = material != null && material.HasProperty(BaseColorId)
                    ? material.GetColor(BaseColorId)
                    : Color.white;
            }
        }

        public void ApplyHit(in HitContext context)
        {
            TotalDamageTaken += context.Damage;
            HitCount++;

            knockbackVelocity += context.Direction * context.Knockback;
            flashRemaining = flashSeconds;

            if (logHits)
            {
                // Through GameLog, not Debug.Log: otherwise these bypass the filter, never
                // reach the overlay, and clutter Player.log outside the game's own stream.
                GameLog.Debug(LogCategory.Combat,
                    $"dummy hit     {context.Attack.Id}  {context.Damage:0.##} dmg ({context.DamageType})  " +
                    $"poise {context.PoiseDamage:0.##}  knock {context.Knockback:0.##}  " +
                    $"total {TotalDamageTaken:0.##} over {HitCount} hits");
            }
        }

        public void ResetStats()
        {
            TotalDamageTaken = 0f;
            HitCount = 0;
        }

        void Update()
        {
            float deltaTime = Time.deltaTime;
            Statuses.Tick(deltaTime);

            if (deltaTime > 0f)
            {
                knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, knockbackDamping * deltaTime);

                Vector3 position = transform.position + knockbackVelocity * deltaTime;
                if (knockbackVelocity.sqrMagnitude < 0.01f)
                    position = Vector3.MoveTowards(position, spawnPosition, returnSpeed * deltaTime);

                position.y = spawnPosition.y;
                transform.position = position;

                if (flashRemaining > 0f)
                    flashRemaining -= deltaTime;
            }

            ApplyFlash();
        }

        void ApplyFlash()
        {
            if (renderers == null)
                return;

            float t = flashSeconds > 0f ? Mathf.Clamp01(flashRemaining / flashSeconds) : 0f;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;

                renderers[i].GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(BaseColorId, Color.Lerp(baseColors[i], flashColor, t));
                renderers[i].SetPropertyBlock(propertyBlock);
            }
        }
    }
}
