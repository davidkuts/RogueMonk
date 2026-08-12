using System;
using Game.Core.Diagnostics;
using Game.Core.Locomotion;
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
        [SerializeField, Tooltip("Splice feedback: the reverse-tint flash while wounds play backwards. Placeholder for the capsule phase.")]
        Color healFlashColor = new Color(0.35f, 1f, 0.75f);
        [SerializeField, Tooltip("How long the splice flash lasts.")]
        float healFlashSeconds = 0.5f;

        Renderer[] renderers;
        MaterialPropertyBlock propertyBlock;
        Color[] baseColors;
        float invulnerabilityRemaining;
        float flashRemaining;
        float healFlashRemaining;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public float CurrentHealth { get; private set; }

        public float MaxHealth => maxHealth;

        public float HealthFraction => maxHealth <= 0f ? 0f : Mathf.Clamp01(CurrentHealth / maxHealth);

        public bool IsAlive => CurrentHealth > 0f;

        public StatusEffectContainer Statuses { get; } = new StatusEffectContainer();

        /// <summary>
        /// Present so the player is a complete <see cref="IDamageable"/>, and deliberately never
        /// ticked. Every DoT in the game is boon-applied, and boons only ever hit enemies; the one
        /// damage-over-time the player takes is the Sailspit's goo, which runs on its own dwell
        /// clock through <see cref="ApplyDamageOverTime"/> because it has to bypass the i-frame
        /// window. Wiring a tick here would be machinery serving nothing.
        /// </summary>
        public DotContainer Dots { get; } = new DotContainer();

        /// <summary>
        /// Debug: while true, no hit ever lands. Toggled by <c>DebugCheats</c> (R2 / G) so the
        /// whole run can be tested without health pressure. Deliberately checked *after* the
        /// dash i-frame branch, so perfect dodges still register and the Riposte loop — the
        /// only way to damage a gated elite — remains testable with god mode on.
        /// </summary>
        public bool GodMode { get; set; }

        /// <summary>True during post-hit invulnerability, dash i-frames, or debug god mode.</summary>
        public bool IsInvulnerable =>
            GodMode || invulnerabilityRemaining > 0f || (motor != null && motor.IsInvulnerable);

        /// <summary>
        /// True while a one-hit shield is armed (the Gauntlet Buckle Stray). The shield eats
        /// the next hit that would otherwise deal damage; hits that were dodged, godmoded or
        /// i-framed away do not consume it.
        /// </summary>
        public bool HasOneHitShield { get; private set; }

        public event Action<float> Damaged;

        /// <summary>Raised when health goes UP — the Splice, the one sanctioned heal.</summary>
        public event Action<float> Healed;

        /// <summary>Raised when an armed one-hit shield eats a hit.</summary>
        public event Action ShieldConsumed;

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

        /// <summary>
        /// Deliberately does nothing. The player has no poise or stagger system at all — being
        /// interrupted is something that happens to enemies, and the player's answer to pressure is
        /// the dash. Nothing in the game currently forces a stagger on the player, and if something
        /// ever should, it needs its own design pass rather than inheriting the enemies' one.
        /// </summary>
        public void ApplyStagger(float seconds)
        {
        }

        public void ApplyHit(in HitContext context)
        {
            if (!IsAlive)
                return;

            // Dash i-frames come first: a strict overlap is a perfect dodge, and refunds the charge.
            //
            // The threat class decides how much grace applies. A projectile gets the shorter
            // window because its hitbox travels into the player, so any instant of the protection
            // catches it; a melee swing also demands the player still be standing in the arc.
            ThreatType threat = context.Attack is AttackDefinition threatAsset
                ? threatAsset.Threat
                : ThreatType.Melee;

            if (motor != null && motor.Dash != null && motor.Dash.IsProtectedAgainst(threat))
            {
                if (motor.Dash.TryRegisterPerfectDodge(threat))
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

            if (GodMode)
            {
                GameLog.Debug(LogCategory.Combat, $"GOD MODE ignored {context.Attack.Id}");
                return;
            }

            if (invulnerabilityRemaining > 0f)
            {
                GameLog.Debug(LogCategory.Combat,
                    $"ignored {context.Attack.Id} - {invulnerabilityRemaining:0.00}s of post-hit invulnerability left");
                return;
            }

            // The one-hit shield (Gauntlet Buckle) sits last, after every free-avoidance path:
            // it must only be spent on a hit that would genuinely have cost health. Consuming
            // it grants the same mandatory post-hit window a real hit would, or a two-hit burst
            // would eat the shield and the health in the same beat.
            if (HasOneHitShield)
            {
                HasOneHitShield = false;
                invulnerabilityRemaining = Mathf.Max(0f, postHitInvulnerabilitySeconds);
                flashRemaining = invulnerabilityRemaining;
                GameLog.Info(LogCategory.Combat, $"SHIELD BROKE  {context.Attack.Id} absorbed - the buckle is spent");
                ShieldConsumed?.Invoke();
                return;
            }

            float applied = Mathf.Min(context.Damage, CurrentHealth);
            CurrentHealth -= applied;
            invulnerabilityRemaining = Mathf.Max(0f, postHitInvulnerabilitySeconds);
            flashRemaining = invulnerabilityRemaining;

            // Heavy hits land like they look: a brief loss of control and a throw. Only attacks
            // that ask for it — the value is 0 on everything ordinary, so a nibble stays a nibble.
            if (motor != null && context.Attack is AttackDefinition asset &&
                (asset.HitStaggerSeconds > 0f || asset.PlayerKnockback > 0f))
            {
                motor.ApplyHitStagger(asset.HitStaggerSeconds, context.Direction * asset.PlayerKnockback);

                GameLog.Warn(LogCategory.Combat,
                    $"HEAVY HIT  {asset.Id}  control lost {asset.HitStaggerSeconds:0.00}s, thrown {asset.PlayerKnockback:0.#}m/s");
            }

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

        /// <summary>
        /// Damage from standing in something, rather than from being hit by something.
        ///
        /// <para><b>It ignores the post-hit invulnerability window, and deliberately does not grant
        /// one.</b> Routing goo ticks through <see cref="ApplyHit"/> would mean standing in poison
        /// made the player periodically immune to everything else in the room — a defensive reward
        /// for bad positioning, and a way to walk into a puddle to survive a Cerashorn charge. It
        /// follows M12's rule for the enemy-side equivalent: damage-over-time is the consequence of
        /// a situation that has already resolved, so it does not re-enter the hit pipeline.</para>
        ///
        /// <para>God mode and death still apply. Dash i-frames deliberately do <em>not</em> — a
        /// Blink is an answer to an attack, not a way to stand in venom for free.</para>
        ///
        /// <para>This does not violate DESIGN.md's no-guaranteed-damage rule: the goo is telegraphed
        /// for its whole flight, the first second inside is free, and leaving resets the clock. A
        /// player who reads it and keeps moving pays nothing.</para>
        /// </summary>
        public void ApplyDamageOverTime(float amount, string source)
        {
            if (amount <= 0f || CurrentHealth <= 0f)
                return;

            if (GodMode)
            {
                GameLog.Debug(LogCategory.Combat, $"GOD MODE ignored {source} tick");
                return;
            }

            float applied = Mathf.Min(amount, CurrentHealth);
            CurrentHealth -= applied;

            GameLog.Warn(LogCategory.Combat,
                $"PLAYER DOT  {source}  -{applied:0.##} hp  ({CurrentHealth:0.##}/{maxHealth:0.##})");

            Damaged?.Invoke(applied);

            if (CurrentHealth <= 0f)
            {
                CurrentHealth = 0f;
                GameLog.Error(LogCategory.Combat, $"PLAYER DIED  ({source})");
                Died?.Invoke();
            }
        }

        /// <summary>Full restore. Run setup only — the Splice is the one in-run heal.</summary>
        public void ResetForNewRun()
        {
            CurrentHealth = maxHealth;
            invulnerabilityRemaining = 0f;
            HasOneHitShield = false;
            Statuses.ClearAll();
        }

        /// <summary>Arms the one-hit shield. Idempotent — shields do not stack.</summary>
        public void GrantOneHitShield()
        {
            if (HasOneHitShield || !IsAlive)
                return;

            HasOneHitShield = true;
            GameLog.Info(LogCategory.Combat, "SHIELD ARMED - the next hit is free");
        }

        /// <summary>Disarms the shield without consuming it — for the Stray that granted it being replaced.</summary>
        public void RevokeOneHitShield() => HasOneHitShield = false;

        /// <summary>
        /// Rewinds the body to a state it was actually in moments ago — the Stored Rewind Stopgap
        /// (REWARDS.md §5), position handled by the caller and health handled here.
        ///
        /// <para>⚠️ This is the SECOND sanctioned exception to DESIGN.md's "no healing at all",
        /// after the Splice. It is a narrower one: it cannot invent health, only restore a value
        /// this body genuinely held inside the rewind window, and it is gated behind a consumable
        /// capped at two. Recorded rather than hidden — DESIGN.md § Player health still has not
        /// been reconciled with REWARDS.md and this makes the gap one item wider.</para>
        ///
        /// <para>Never raises the ceiling: a rewind to a moment when the player was healthier than
        /// their maximum is impossible by construction, and a rewind DOWN is refused because a
        /// panic button that could hurt you is a trap rather than a tool.</para>
        /// </summary>
        public void RewindTo(float recordedHealth)
        {
            if (!IsAlive)
                return;

            float target = Mathf.Clamp(recordedHealth, 0f, maxHealth);
            if (target <= CurrentHealth)
            {
                GameLog.Info(LogCategory.Combat,
                    $"REWIND  health unchanged ({CurrentHealth:0.##}) - the recorded state was no healthier");
                return;
            }

            float before = CurrentHealth;
            CurrentHealth = target;
            healFlashRemaining = healFlashSeconds;

            GameLog.Info(LogCategory.Combat,
                $"REWIND  +{CurrentHealth - before:0.##} hp ({before:0.##} -> {CurrentHealth:0.##}/{maxHealth:0.##})");

            Healed?.Invoke(CurrentHealth - before);
        }

        /// <summary>
        /// DEBUG ONLY: a flat heal that ignores the Splice's biome-entry ceiling, so long runs
        /// can be tested without health pressure while god mode stays off. Never wire this to
        /// gameplay — the Splice is the one sanctioned in-run heal.
        /// </summary>
        public void DebugHeal(float amount)
        {
            if (!IsAlive || amount <= 0f)
                return;

            float before = CurrentHealth;
            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
            healFlashRemaining = healFlashSeconds;

            GameLog.Warn(LogCategory.Combat,
                $"DEBUG HEAL  +{CurrentHealth - before:0.##} hp ({before:0.##} -> {CurrentHealth:0.##}/{maxHealth:0.##})");

            if (CurrentHealth > before)
                Healed?.Invoke(CurrentHealth - before);
        }

        /// <summary>
        /// The Splice (REWARDS.md §3): rewinds the body toward a less-injured state, never past
        /// the biome-entry snapshot. This is the ONE exception to no-healing — sanctioned by
        /// the rewards spec, gated behind a room reward, and clamped so it can never undo more
        /// than the current biome inflicted.
        /// </summary>
        public void ApplySplice(float depthFraction, float biomeEntryCeiling)
        {
            if (!IsAlive)
                return;

            float before = CurrentHealth;
            CurrentHealth = SpliceMath.Heal(CurrentHealth, maxHealth, depthFraction, biomeEntryCeiling);

            float gained = CurrentHealth - before;
            healFlashRemaining = healFlashSeconds;

            GameLog.Info(LogCategory.Combat,
                $"SPLICE  +{gained:0.##} hp ({before:0.##} -> {CurrentHealth:0.##}/{maxHealth:0.##})  " +
                $"ceiling {biomeEntryCeiling:0.##}");

            if (gained > 0f)
                Healed?.Invoke(gained);
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

            if (healFlashRemaining > 0f)
                healFlashRemaining -= deltaTime;

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

            // The splice's reverse-tint is steady, deliberately unlike the hit blink: one says
            // "you were hurt", the other says "that is being undone". A hit flash wins if both
            // are somehow live at once.
            bool healing = !on && flashRemaining <= 0f && healFlashRemaining > 0f;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;

                Color color = on ? hitFlashColor : healing ? healFlashColor : baseColors[i];
                renderers[i].GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(BaseColorId, color);
                renderers[i].SetPropertyBlock(propertyBlock);
            }
        }
    }
}
