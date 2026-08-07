using System;
using Game.Core.Diagnostics;
using Game.Core.Player;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// The player as a target. Three DESIGN.md rules meet here:
    /// no healing at all during a run, a mandatory post-hit invulnerability window, and the
    /// perfect dodge — an attack that lands inside dash i-frames refunds the charge instead
    /// of dealing damage. This is where M2's perfect-dodge machinery finally gets exercised.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class PlayerHealth : MonoBehaviour, IDamageable
    {
        [SerializeField, Tooltip("Generous pool: DESIGN.md wants 10–15 mistakes to kill across a level.")]
        float maxHealth = 100f;
        [SerializeField, Tooltip("Mandatory invulnerability after taking a hit (DESIGN.md § Player health).")]
        float postHitInvulnerabilitySeconds = 0.5f;
        [SerializeField] PlayerMotor motor;
        [SerializeField, Tooltip("Optional. Recoloured on a perfect dodge.")]
        DashAfterimage afterimage;

        [Header("Feedback")]
        [SerializeField] Color hitFlashColor = new Color(1f, 0.3f, 0.3f);
        [SerializeField] float flashIntervalSeconds = 0.08f;

        Renderer[] renderers;
        MaterialPropertyBlock propertyBlock;
        Color[] baseColors;
        float invulnerabilityRemaining;
        float flashRemaining;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public float CurrentHealth { get; private set; }

        public float MaxHealth => maxHealth;

        public float HealthFraction => maxHealth <= 0f ? 0f : Mathf.Clamp01(CurrentHealth / maxHealth);

        public bool IsAlive => CurrentHealth > 0f;

        public StatusEffectContainer Statuses { get; } = new StatusEffectContainer();

        /// <summary>True during post-hit invulnerability or dash i-frames.</summary>
        public bool IsInvulnerable =>
            invulnerabilityRemaining > 0f || (motor != null && motor.IsInvulnerable);

        public event Action<float> Damaged;

        public event Action PerfectDodged;

        public event Action Died;

        void Awake()
        {
            if (motor == null)
                motor = GetComponent<PlayerMotor>();
            if (afterimage == null)
                afterimage = GetComponent<DashAfterimage>();

            CurrentHealth = maxHealth;

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
            if (!IsAlive)
                return;

            // Dash i-frames come first: a strict overlap is a perfect dodge, and refunds the charge.
            if (motor != null && motor.Dash != null && motor.Dash.IsProtected)
            {
                if (motor.Dash.TryRegisterPerfectDodge())
                {
                    GameLog.Info(LogCategory.Combat,
                        $"PERFECT DODGE  {context.Attack.Id} phased through dash i-frames - charge refunded");

                    // Recolour the dash trail in the same beat, so the reward is visible rather
                    // than only being a number on the HUD.
                    if (afterimage != null)
                        afterimage.FlagPerfectDodge();

                    PerfectDodged?.Invoke();
                }

                return;
            }

            if (invulnerabilityRemaining > 0f)
            {
                GameLog.Debug(LogCategory.Combat,
                    $"ignored {context.Attack.Id} - {invulnerabilityRemaining:0.00}s of post-hit invulnerability left");
                return;
            }

            float applied = Mathf.Min(context.Damage, CurrentHealth);
            CurrentHealth -= applied;
            invulnerabilityRemaining = Mathf.Max(0f, postHitInvulnerabilitySeconds);
            flashRemaining = invulnerabilityRemaining;

            GameLog.Warn(LogCategory.Combat,
                $"PLAYER HIT  {context.Attack.Id}  -{applied:0.##} hp  ({CurrentHealth:0.##}/{maxHealth:0.##})  " +
                $"i-frames {invulnerabilityRemaining:0.00}s");

            Damaged?.Invoke(applied);

            if (CurrentHealth <= 0f)
            {
                CurrentHealth = 0f;
                GameLog.Error(LogCategory.Combat, "PLAYER DIED");
                Died?.Invoke();
            }
        }

        /// <summary>Full restore. Run setup only — there is deliberately no in-run heal.</summary>
        public void ResetForNewRun()
        {
            CurrentHealth = maxHealth;
            invulnerabilityRemaining = 0f;
            Statuses.ClearAll();
        }

        void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
                return;

            if (invulnerabilityRemaining > 0f)
                invulnerabilityRemaining -= deltaTime;

            if (flashRemaining > 0f)
                flashRemaining -= deltaTime;

            Statuses.Tick(deltaTime);
            ApplyFlash();
        }

        void ApplyFlash()
        {
            if (renderers == null)
                return;

            // Blink rather than a steady tint, so invulnerability reads as a state, not a colour.
            bool on = flashRemaining > 0f &&
                      Mathf.Repeat(flashRemaining, flashIntervalSeconds * 2f) < flashIntervalSeconds;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;

                renderers[i].GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(BaseColorId, on ? hitFlashColor : baseColors[i]);
                renderers[i].SetPropertyBlock(propertyBlock);
            }
        }
    }
}
