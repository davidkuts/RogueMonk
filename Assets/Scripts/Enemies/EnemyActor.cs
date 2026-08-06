using System;
using Game.Combat;
using Game.Core.Diagnostics;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// The damageable body of an enemy: health, poise/armour/stagger, hit reaction and death.
    /// Knowing nothing about AI, it can back any archetype; <see cref="MeleeEnemyController"/>
    /// reads its state to decide whether it may act.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyActor : MonoBehaviour, IDamageable
    {
        [SerializeField] EnemyDefinition definition;

        [Header("Reaction")]
        [SerializeField] Color hitFlashColor = new Color(1f, 0.95f, 0.9f);
        [SerializeField, Tooltip("Colour held for the whole stagger, so the punish window is unmistakable.")]
        Color staggeredColor = new Color(0.55f, 0.4f, 1f);
        [SerializeField] float hitFlashSeconds = 0.09f;
        [SerializeField, Tooltip("How fast knockback bleeds off. Higher = stops sooner.")]
        float knockbackDamping = 8f;

        Renderer[] renderers;
        MaterialPropertyBlock propertyBlock;
        Color[] baseColors;
        CharacterController controller;
        Vector3 knockbackVelocity;
        float flashRemaining;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public IEnemyDefinition Definition => definition;

        public Health Health { get; private set; }

        public PoiseSystem Poise { get; private set; }

        public StatusEffectContainer Statuses { get; } = new StatusEffectContainer();

        public bool IsAlive => Health != null && Health.IsAlive;

        /// <summary>True while poise is broken — the enemy cannot act and takes the punish.</summary>
        public bool IsStaggered => Poise != null && Poise.IsStaggered;

        /// <summary>Raised when poise breaks, so the controller can interrupt whatever it was doing.</summary>
        public event Action Staggered;

        public event Action Died;

        /// <summary>Overridden colour while a telegraph is running. Null when not telegraphing.</summary>
        public Color? TelegraphOverride { get; set; }

        void Awake()
        {
            if (definition == null)
            {
                Debug.LogError($"{nameof(EnemyActor)} on '{name}' has no {nameof(EnemyDefinition)}.", this);
                enabled = false;
                return;
            }

            controller = GetComponent<CharacterController>();
            Health = new Health(definition.MaxHealth);
            Poise = new PoiseSystem(definition);

            Health.Died += OnDied;
            Poise.Broke += OnPoiseBroke;
            Poise.ArmorStripped += OnArmorStripped;

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

        void OnDestroy()
        {
            if (Health != null) Health.Died -= OnDied;
            if (Poise != null)
            {
                Poise.Broke -= OnPoiseBroke;
                Poise.ArmorStripped -= OnArmorStripped;
            }
        }

        public void ApplyHit(in HitContext context)
        {
            if (!IsAlive)
                return;

            float applied = Health.TakeDamage(context.Damage);

            // Poise only matters to something still standing. Applying it after a lethal hit
            // would open a "punish window" on a corpse, fire Staggered at the controller and
            // put a stagger status on a dead enemy.
            PoiseResult poiseResult = Health.IsAlive
                ? Poise.ApplyPoiseDamage(context.PoiseDamage)
                : PoiseResult.Absorbed;

            // Immune-tier enemies get the full feedback but are never moved or interrupted.
            if (definition.Tier != StaggerTier.Immune)
                knockbackVelocity += context.Direction * context.Knockback;

            flashRemaining = hitFlashSeconds;

            GameLog.Info(LogCategory.Enemy,
                $"hit {definition.Id}  -{applied:0.##} hp ({Health.Current:0.##}/{Health.Max:0.##})  " +
                $"poise {Poise.Poise:0.##}/{definition.PoiseMax:0.##} -> {poiseResult}" +
                (definition.Tier == StaggerTier.Armored ? $"  armor {Poise.Armor:0.##}" : string.Empty));
        }

        void OnPoiseBroke(float duration)
        {
            Statuses.Apply(StatusEffect.Stagger, duration);
            GameLog.Info(LogCategory.Enemy, $"POISE BREAK {definition.Id}  staggered {duration:0.00}s - punish window open");
            Staggered?.Invoke();
        }

        void OnArmorStripped() =>
            GameLog.Info(LogCategory.Enemy, $"ARMOR STRIPPED {definition.Id} - poise damage now counts");

        void OnDied()
        {
            Poise.ClearStagger();
            GameLog.Info(LogCategory.Enemy, $"DEATH {definition.Id}");
            Died?.Invoke();
            gameObject.SetActive(false);
        }

        void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
                return;

            Poise.Tick(deltaTime);
            Statuses.Tick(deltaTime);

            if (flashRemaining > 0f)
                flashRemaining -= deltaTime;

            ApplyKnockback(deltaTime);
            ApplyColor();
        }

        void ApplyKnockback(float deltaTime)
        {
            if (knockbackVelocity.sqrMagnitude < 0.0001f)
            {
                knockbackVelocity = Vector3.zero;
                return;
            }

            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, knockbackDamping * deltaTime);

            if (controller != null && controller.enabled)
                controller.Move(knockbackVelocity * deltaTime);
            else
                transform.position += knockbackVelocity * deltaTime;
        }

        void ApplyColor()
        {
            if (renderers == null)
                return;

            float flash = hitFlashSeconds > 0f ? Mathf.Clamp01(flashRemaining / hitFlashSeconds) : 0f;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;

                Color color = baseColors[i];
                if (TelegraphOverride.HasValue)
                    color = TelegraphOverride.Value;
                if (IsStaggered)
                    color = staggeredColor;
                if (flash > 0f)
                    color = Color.Lerp(color, hitFlashColor, flash);

                renderers[i].GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(BaseColorId, color);
                renderers[i].SetPropertyBlock(propertyBlock);
            }
        }
    }
}
