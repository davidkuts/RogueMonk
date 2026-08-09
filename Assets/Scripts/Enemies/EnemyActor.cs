using System;
using System.Collections.Generic;
using Game.Combat;
using Game.Core.Diagnostics;
using Game.Core.Timing;
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

        [Header("Telegraph")]
        [SerializeField, Tooltip("Pulse rate of the wind-up flash. Without animation this is the only tell, so it has to be unmissable.")]
        float telegraphPulseHz = 6f;
        [SerializeField, Tooltip("How saturated the telegraph is at the very start of the wind-up.")]
        float telegraphMinIntensity = 0.45f;

        [Header("Reaction")]
        [SerializeField] Color hitFlashColor = new Color(1f, 0.95f, 0.9f);
        [SerializeField, Tooltip("Colour held for the whole stagger, so the punish window is unmistakable.")]
        Color staggeredColor = new Color(0.55f, 0.4f, 1f);
        [SerializeField] float hitFlashSeconds = 0.09f;
        [SerializeField, Tooltip("How fast knockback bleeds off. Higher = stops sooner.")]
        float knockbackDamping = 8f;

        [Header("Statuses")]
        [SerializeField, Tooltip("What Burning/Chilled/Rooted actually do. Shared by every enemy so a status means the same thing whatever inflicted it.")]
        StatusSettings statusSettings;

        [Header("Death")]
        [SerializeField, Tooltip("Seconds the body stays for a death beat before it is removed. 0 removes it immediately, which is what trash does.")]
        float deathSequenceSeconds;
        [SerializeField, Tooltip("Freeze on the killing blow. 0 for trash; a boss earns a longer one than any ordinary hit.")]
        float deathHitstopSeconds;
        [SerializeField, Tooltip("How far the body sinks into the floor across its death beat.")]
        float deathSinkDistance = 1.1f;
        [SerializeField, Tooltip("Scale the body collapses to across its death beat.")]
        float deathEndScale = 0.75f;

        Renderer[] renderers;
        MaterialPropertyBlock propertyBlock;
        Color[] baseColors;
        DamageZone[] zones;
        CharacterController controller;
        Vector3 knockbackVelocity;
        float flashRemaining;

        float burnTickRemaining;
        float deathRemaining;
        float deathTotal;
        Vector3 deathStartScale;
        Vector3 deathStartPosition;
        bool deathResolved;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public IEnemyDefinition Definition => definition;

        public Health Health { get; private set; }

        public PoiseSystem Poise { get; private set; }

        public StatusEffectContainer Statuses { get; } = new StatusEffectContainer();

        public bool IsAlive => Health != null && Health.IsAlive;

        /// <summary>True while poise is broken — the enemy cannot act and takes the punish.</summary>
        public bool IsStaggered => Poise != null && Poise.IsStaggered;

        /// <summary>The per-collider armour plates on this body. Empty for an unzoned enemy.</summary>
        public IReadOnlyList<DamageZone> Zones => zones ?? System.Array.Empty<DamageZone>();

        /// <summary>
        /// True while at least one amber plate is up. Drives pull resistance, so a zoned body
        /// holds its ground against the Undertow for the same reason a bar-armoured one does.
        /// </summary>
        public bool HasIntactArmorZone
        {
            get
            {
                if (zones == null)
                    return false;

                for (int i = 0; i < zones.Length; i++)
                {
                    if (zones[i] != null && zones[i].IsIntactArmor)
                        return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Movement scaling from active statuses. Read by the controllers each frame rather than
        /// baked into the definition, because it changes while the enemy is alive.
        /// </summary>
        public float StatusMoveSpeedMultiplier =>
            statusSettings != null ? statusSettings.MoveSpeedMultiplier(Statuses) : 1f;

        /// <summary>
        /// True between the killing blow and the body actually being removed. Anything tracking
        /// live enemies must treat a dying body as still present: <c>IsAlive</c> goes false the
        /// instant health hits zero, so without this a room would clear over a standing corpse.
        /// </summary>
        public bool IsDying { get; private set; }

        /// <summary>Raised when poise breaks, so the controller can interrupt whatever it was doing.</summary>
        public event Action Staggered;

        /// <summary>
        /// Raised when a pull failed to move this enemy because its amber is still intact. The
        /// failed drag is meant to flare gold, so the tier stays readable in the one frame the
        /// player has to read it.
        /// </summary>
        public event Action PullResisted;

        /// <summary>Raised the moment health reaches zero, before the death beat plays out.</summary>
        public event Action DeathSequenceStarted;

        /// <summary>Raised once the body is actually gone. Always fires exactly once.</summary>
        public event Action Died;

        /// <summary>Overridden colour while a telegraph is running. Null when not telegraphing.</summary>
        public Color? TelegraphOverride { get; set; }

        /// <summary>0..1 through the wind-up. Drives how hard the telegraph reads.</summary>
        public float TelegraphProgress { get; set; }

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

            // Zones are colliders on the body, so they are found once here rather than walked per
            // hit — a hit already knows its own collider, and this list exists only for the
            // whole-body questions (is any amber still up, crack all of it).
            zones = GetComponentsInChildren<DamageZone>(includeInactive: true);

            renderers = GetComponentsInChildren<Renderer>();
            propertyBlock = new MaterialPropertyBlock();
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                baseColors[i] = ReadBaseColor(renderers[i]);
            }
        }

        /// <summary>
        /// The colour this renderer sits at when nothing is happening to it.
        ///
        /// <para>The property block is checked <em>first</em>, and that is load-bearing for the
        /// capsule kit: seven archetypes share one material and get their identity hue from a
        /// per-renderer block, so reading the shared material would give every enemy in the biome
        /// the same base colour — and then the first hit flash would repaint them all to it
        /// permanently. A body with no block set falls back to the material as before.</para>
        /// </summary>
        Color ReadBaseColor(Renderer renderer)
        {
            if (renderer == null)
                return Color.white;

            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            if (block.HasColor(BaseColorId))
                return block.GetColor(BaseColorId);

            Material material = renderer.sharedMaterial;
            return material != null && material.HasProperty(BaseColorId)
                ? material.GetColor(BaseColorId)
                : Color.white;
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

            // The zone is a property of the *target*, so it lands after every attacker-side
            // modifier has had its say — an amber plate reduces whatever actually arrived, rather
            // than a number a boon might still have multiplied afterwards. DESIGN.md wires boons
            // on the attacker's resolver, so there is no target-side stage for it to sit in.
            HitZone zone = context.Zone;

            // The damage gate: an enemy that declares onlyDamagedBy takes health damage from
            // exactly that attack and nothing else. Poise, knockback and every piece of hit
            // feedback still land, so the fight stays interactive — only the health bar waits
            // for the earned counter.
            bool guarded = definition.OnlyDamagedBy != null &&
                (context.Attack == null || context.Attack.Id != definition.OnlyDamagedBy.Id);

            float applied = guarded ? 0f : Health.TakeDamage(context.Damage * zone.DamageMultiplier);

            // Poise only matters to something still standing. Applying it after a lethal hit
            // would open a "punish window" on a corpse, fire Staggered at the controller and
            // put a stagger status on a dead enemy.
            //
            // Intact amber declines to have a poise bar at all rather than absorbing into one: a
            // plated zone beaten on forever must never accumulate toward a stagger, or "no stagger"
            // becomes "a slower stagger".
            PoiseResult poiseResult = Health.IsAlive && !zone.BlocksStagger
                ? Poise.ApplyPoiseDamage(context.PoiseDamage)
                : PoiseResult.Absorbed;

            // Immune-tier enemies get the full feedback but are never moved or interrupted.
            // A negative impulse is a pull (the Undertow) rather than a knockback, and uncracked
            // amber resists it: it takes the ticks and the armour damage but does not slide, so a
            // single frame of the spin tells the player which tier is which by who moved. Once the
            // armour is off, an Armored enemy behaves as tier 1 in this as in everything else.
            //
            // A zoned body resists on the same rule for the same reason, even at Staggerable tier
            // with no armour bar at all: what holds an Ambershell in place is the amber on its
            // back, and that is a collider rather than a number.
            bool isPull = context.Knockback < 0f;
            bool resistsPull = isPull &&
                ((definition.Tier == StaggerTier.Armored && !Poise.IsArmorStripped) || HasIntactArmorZone);

            if (definition.Tier != StaggerTier.Immune && !resistsPull)
                knockbackVelocity += context.Direction * context.Knockback;
            else if (resistsPull)
                PullResisted?.Invoke();

            flashRemaining = hitFlashSeconds;

            GameLog.Info(LogCategory.Enemy,
                $"hit {definition.Id}  -{applied:0.##} hp ({Health.Current:0.##}/{Health.Max:0.##})  " +
                $"poise {Poise.Poise:0.##}/{definition.PoiseMax:0.##} -> {poiseResult}" +
                (guarded ? $"  GUARDED - only '{definition.OnlyDamagedBy.Id}' damages this enemy" : string.Empty) +
                (definition.Tier == StaggerTier.Armored ? $"  armor {Poise.Armor:0.##}" : string.Empty) +
                (zone.IsNeutral ? string.Empty : $"  zone '{zone.Id}' x{zone.DamageMultiplier:0.00}"));
        }

        /// <summary>
        /// Cracks every armored zone on this body for <paramref name="seconds"/>. The wall-bait
        /// payoff: one call opens the whole shell rather than the one plate that touched the wall,
        /// because the player earned the opening with a manoeuvre and not with an aiming problem.
        /// </summary>
        public void CrackArmorZones(float seconds)
        {
            if (zones == null)
                return;

            for (int i = 0; i < zones.Length; i++)
            {
                if (zones[i] != null)
                    zones[i].Crack(seconds);
            }
        }

        /// <summary>
        /// The Undertow's arrival stagger. Eligibility is decided here rather than by the caller,
        /// because it is a tier question: Immune is never interrupted, and amber that was never
        /// pulled never arrived, so there is nothing to interrupt it out of.
        /// </summary>
        public void ApplyStagger(float seconds)
        {
            if (!IsAlive || IsDying || seconds <= 0f)
                return;

            if (definition.Tier == StaggerTier.Immune)
                return;

            if (definition.Tier == StaggerTier.Armored && !Poise.IsArmorStripped)
                return;

            Poise.ForceStagger(seconds);

            // Applied unconditionally so an extended stagger keeps its status in step with the
            // poise timer; Broke only fires on the transition into one.
            Statuses.Apply(StatusEffect.Stagger, Poise.StaggerRemaining);
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

            IsDying = true;
            DeathSequenceStarted?.Invoke();

            if (deathHitstopSeconds > 0f && GameClock.Instance != null)
                GameClock.Instance.RequestFreeze(deathHitstopSeconds);

            if (deathSequenceSeconds <= 0f)
            {
                // Trash: gone on the frame it dies, exactly as before this beat existed.
                FinishDeath();
                return;
            }

            deathTotal = deathSequenceSeconds;
            deathRemaining = deathSequenceSeconds;
            deathStartScale = transform.localScale;
            deathStartPosition = transform.position;

            // The body is no longer a participant; let it sink through its own capsule.
            if (controller != null)
                controller.enabled = false;
        }

        /// <summary>
        /// Ends the beat and hands the body back. Guarded so <see cref="Died"/> fires exactly once
        /// however the body leaves — anything that swallowed it would lock the room shut.
        /// </summary>
        void FinishDeath()
        {
            if (deathResolved)
                return;

            deathResolved = true;
            IsDying = false;
            Died?.Invoke();

            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        void OnDisable()
        {
            // Safety hatch. Being disabled or destroyed part-way through the beat must still
            // resolve the death, or the runner waits forever for an enemy that is already gone.
            if (IsDying)
                FinishDeath();
        }

        void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
                return;

            if (IsDying)
            {
                TickDeath(deltaTime);
                return;
            }

            Poise.Tick(deltaTime);
            Statuses.Tick(deltaTime);
            TickBurn(deltaTime);

            if (flashRemaining > 0f)
                flashRemaining -= deltaTime;

            ApplyKnockback(deltaTime);
            ApplyColor();
        }

        /// <summary>
        /// The death beat: sink and collapse rather than blink out. Runs on the scaled clock on
        /// purpose — the killing blow's hitstop should hold the first frame of it, and GameClock
        /// already zeroes the scale while paused so a paused beat does not drain away.
        /// </summary>
        void TickDeath(float deltaTime)
        {
            deathRemaining -= deltaTime;

            float t = deathTotal > 0f ? 1f - Mathf.Clamp01(deathRemaining / deathTotal) : 1f;

            transform.localScale = deathStartScale * Mathf.Lerp(1f, Mathf.Max(0.01f, deathEndScale), t);
            transform.position = deathStartPosition + Vector3.down * (deathSinkDistance * t);

            if (flashRemaining > 0f)
                flashRemaining -= deltaTime;

            ApplyColor();

            if (deathRemaining <= 0f)
                FinishDeath();
        }

        /// <summary>
        /// Burns in discrete ticks rather than continuously, so each one is its own damage number
        /// and its own flash. A smooth drain would be invisible — the same mistake the passive
        /// empowered strike made.
        ///
        /// Burn damage does not go through the hit resolver: it is the *consequence* of a hit that
        /// already resolved, and running it back through the pipeline would let a modifier that
        /// applies Burning re-apply it from its own damage, forever.
        /// </summary>
        void TickBurn(float deltaTime)
        {
            // A damage-gated enemy does not burn: the gate promises that only the earned counter
            // moves the health bar, and a boon's DoT trickling past it would quietly break that
            // promise (burn bypasses the hit resolver, so the ApplyHit gate never sees it).
            if (statusSettings == null || !Statuses.Has(StatusEffect.Burning) || !Health.IsAlive
                || definition.OnlyDamagedBy != null)
            {
                burnTickRemaining = 0f;
                return;
            }

            burnTickRemaining -= deltaTime;
            if (burnTickRemaining > 0f)
                return;

            burnTickRemaining = statusSettings.BurnTickSeconds;

            float applied = Health.TakeDamage(statusSettings.BurnDamagePerTick);
            flashRemaining = hitFlashSeconds;

            GameLog.Debug(LogCategory.Enemy,
                $"burn {definition.Id}  -{applied:0.##} hp ({Health.Current:0.##}/{Health.Max:0.##})  " +
                $"{Statuses.Remaining(StatusEffect.Burning):0.0}s left");
        }

        /// <summary>
        /// Integrates the knockback impulse, then decays it.
        ///
        /// <para>Move first, damp second, and damp exponentially. The previous order — damp with
        /// <c>Vector3.Lerp(v, 0, damping * deltaTime)</c>, then move — had a hole: <c>Lerp</c>
        /// clamps its factor at 1, so any frame longer than <c>1 / damping</c> (0.125 s here)
        /// zeroed the velocity and *then* moved by zero. The whole impulse vanished without
        /// displacing the body at all. A hitch would silently eat a knockback, and the vortex's
        /// pull — three impulses that a single long frame can legitimately deliver at once — made
        /// it reproducible rather than rare.</para>
        ///
        /// <para><c>Exp(-damping * dt)</c> is the same curve sampled exactly instead of
        /// approximated, so it is stable at any frame length and matches the old feel at 60 fps
        /// (0.875 against 0.867 per frame).</para>
        /// </summary>
        void ApplyKnockback(float deltaTime)
        {
            if (knockbackVelocity.sqrMagnitude < 0.0001f)
            {
                knockbackVelocity = Vector3.zero;
                return;
            }

            Vector3 step = knockbackVelocity * deltaTime;

            if (controller != null && controller.enabled)
                controller.Move(step);
            else
                transform.position += step;

            knockbackVelocity *= Mathf.Exp(-knockbackDamping * deltaTime);
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
                {
                    // Pulsing, and ramping toward fully saturated as the wind-up completes, so
                    // the moment of the strike is the brightest. A static tint is far too easy
                    // to miss when the enemy has no attack animation to read.
                    float pulse = Mathf.PingPong(Time.unscaledTime * telegraphPulseHz, 1f);
                    float ramp = Mathf.Lerp(telegraphMinIntensity, 1f, Mathf.Clamp01(TelegraphProgress));
                    float intensity = ramp * Mathf.Lerp(0.55f, 1f, pulse);
                    color = Color.Lerp(baseColors[i], TelegraphOverride.Value, intensity);
                }

                // A status the player inflicted has to be visible on the target, or a boon that
                // applies one is as invisible as the empowered strike was.
                Color? statusTint = statusSettings != null ? statusSettings.Tint(Statuses) : null;
                if (statusTint.HasValue && !TelegraphOverride.HasValue)
                    color = Color.Lerp(color, statusTint.Value, 0.7f);

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
