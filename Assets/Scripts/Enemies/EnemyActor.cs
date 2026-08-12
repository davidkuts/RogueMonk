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
    public sealed class EnemyActor : MonoBehaviour, IDamageable, IEraTagged, IEchoRepeatTarget
    {
        static readonly List<EnemyActor> live = new List<EnemyActor>();

        /// <summary>
        /// Every enemy body currently in the scene, alive or playing out a death beat.
        ///
        /// <para>A live list maintained on enable/disable rather than a <c>FindObjectsByType</c>
        /// sweep, for the reason M21D recorded when it built the fragment sweep the same way: rooms
        /// are torn down and rebuilt whole, which is exactly when a scene-wide search is least
        /// trustworthy and most expensive. Callers filter on <see cref="IsAlive"/> themselves —
        /// what counts as "active" is the caller's question, not the body's.</para>
        /// </summary>
        public static IReadOnlyList<EnemyActor> Live => live;

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

        [Header("Plating")]
        [SerializeField, Tooltip("Where the amber hue comes from. Never author a plate colour — the telegraph grammar is enforced in one asset.")]
        TelegraphPalette platingPalette;
        [SerializeField, Range(0f, 1f), Tooltip("How far an intact plate is pushed toward amber. Strong: where the armour is, is the thing the player has to read.")]
        float intactPlateBlend = 0.85f;
        [SerializeField, Tooltip("Colour of an open plate. Dull and dead — a cracked plate reports itself as ordinary flesh, and it should stop looking like time at all.")]
        Color crackedPlateColor = new Color(0.34f, 0.27f, 0.22f, 1f);
        [SerializeField, Range(0f, 1f), Tooltip("How far a cracked plate is pushed toward the cracked colour.")]
        float crackedPlateBlend = 0.7f;
        [SerializeField, Tooltip("How long a plate flares white as it breaks open, so the earned weak point is a beat and not a fade.")]
        float crackFlashSeconds = 0.25f;

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

        /// <summary>
        /// The plate each renderer belongs to, parallel to <see cref="renderers"/> and null where
        /// the renderer is ordinary hide. Built once so the tint stack can paint the armour without
        /// walking the hierarchy every frame — and so plating stays a layer in the ONE ordered
        /// stack rather than a second component writing the same property block from Update, which
        /// would race it.
        /// </summary>
        DamageZone[] plateForRenderer;
        float crackFlashRemaining;

        DamageZone[] zones;
        CharacterController controller;
        Vector3 knockbackVelocity;
        float flashRemaining;

        /// <summary>
        /// True once <see cref="EnemyDefinition"/>'s damage gate has been broken by its key
        /// attack. Never resets: the guard is a lesson taught once per body, not a shield that
        /// regrows mid-fight.
        /// </summary>
        bool damageGateBroken;

        float deathRemaining;
        float deathTotal;
        Vector3 deathStartScale;
        Vector3 deathStartPosition;
        bool deathResolved;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public IEnemyDefinition Definition => definition;

        /// <summary>The era this body belongs to, for era-scoped hit modifiers (Displaced Tooth).</summary>
        public Era Era => definition != null ? definition.Era : Era.None;

        /// <summary>Seconds this kill sheds (proportional to toughness — see the definition's tooltip).</summary>
        public int SecondsOnKill => definition != null ? definition.SecondsOnKill : 0;

        /// <summary>Minutes this kill trickles.</summary>
        public int MinutesOnKill => definition != null ? definition.MinutesOnKill : 0;

        /// <summary>Hours this kill guarantees. Zero for everything but bosses.</summary>
        public int HoursOnKill => definition != null ? definition.HoursOnKill : 0;

        /// <summary>Amber this kill guarantees. Bosses only.</summary>
        public int AmberOnKill => definition != null ? definition.AmberOnKill : 0;

        public Health Health { get; private set; }

        public PoiseSystem Poise { get; private set; }

        public StatusEffectContainer Statuses { get; } = new StatusEffectContainer();

        /// <summary>Stacking damage-over-time instances, applied by boons through the hit pipeline.</summary>
        public DotContainer Dots { get; } = new DotContainer();

        public bool IsAlive => Health != null && Health.IsAlive;

        /// <summary>True while poise is broken — the enemy cannot act and takes the punish.</summary>
        public bool IsStaggered => Poise != null && Poise.IsStaggered;

        /// <summary>
        /// True while this body's damage gate stands: nothing but its key attack moves the health
        /// bar. Read by <see cref="AmberSheen"/>, because a guard whose only tell is a health bar
        /// refusing to move is a mechanic the player cannot see.
        /// </summary>
        public bool IsGuarded =>
            definition != null && DamageGate.IsUp(definition.OnlyDamagedBy, damageGateBroken);

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

        /// <summary>
        /// Raised when the damage gate refused a hit. The player has to be told that the hit did
        /// nothing at the moment it does nothing — a health bar that fails to move is a tell only
        /// for someone already watching it.
        /// </summary>
        public event Action HitRefused;

        /// <summary>
        /// Raised by the one hit that breaks the guard. Fires exactly once per body, because the
        /// gate never re-arms.
        /// </summary>
        public event Action GuardBroken;

        /// <summary>
        /// Raised with what the hit actually cost this body — after zone reduction and the damage
        /// gate, never with what the attacker intended. A floating number quoting the intended
        /// damage over an enemy that took none would be a lie in the one place the player looks.
        /// </summary>
        public event Action<DamageReport> DamageResolved;

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
            plateForRenderer = new DamageZone[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                baseColors[i] = ReadBaseColor(renderers[i]);
                plateForRenderer[i] = renderers[i] != null
                    ? renderers[i].GetComponentInParent<DamageZone>()
                    : null;
            }

            // ENEMIES_BIOME1.md § 3.1's whole point is that an Ambershell's skull and dome answer
            // a swing differently from its tail base. That has resolved correctly since M14.1 and
            // the body said nothing about it — the plate opened for eight seconds with no visual.
            // CrackedChanged was written for this ("so the body can retint") and had no subscriber.
            for (int i = 0; i < zones.Length; i++)
            {
                if (zones[i] != null)
                    zones[i].CrackedChanged += OnZoneCrackedChanged;
            }
        }

        void OnZoneCrackedChanged(bool cracked)
        {
            if (cracked)
                crackFlashRemaining = crackFlashSeconds;
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

            if (zones == null)
                return;

            for (int i = 0; i < zones.Length; i++)
            {
                if (zones[i] != null)
                    zones[i].CrackedChanged -= OnZoneCrackedChanged;
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

            // The damage gate: an enemy that declares onlyDamagedBy refuses all health damage
            // until that attack lands ONCE — the first hit breaks the guard for good, and from
            // then on ordinary attacks damage it normally (human call 2026-08-09; previously
            // every point of damage had to come from the counter). Poise, knockback and every
            // piece of hit feedback still land while guarded, so the fight stays interactive —
            // only the health bar waits for the earned counter.
            DamageGate.Verdict gate = DamageGate.Resolve(definition.OnlyDamagedBy, damageGateBroken, context.Attack);
            if (gate.Breaking)
                damageGateBroken = true;

            bool guarded = gate.Guarded;
            float applied = guarded ? 0f : Health.TakeDamage(context.Damage * zone.DamageMultiplier);

            // Fired before the rest of the hit resolves so the sheen's flash and its shatter land
            // on the same frame as the numbers they are describing.
            if (guarded)
                HitRefused?.Invoke();
            else if (gate.Breaking)
                GuardBroken?.Invoke();

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

            // A combo or riposte hit landing INSIDE a wind-up cancels it (human call
            // 2026-08-11): reading a tell and answering with your own attack is the reward.
            // ApplyStagger still owns the tier questions — Immune bosses ignore this, an
            // Armored body with its plating intact shrugs it off, and a hit on an intact amber
            // zone was already filtered by the rule.
            if (Health.IsAlive &&
                WindupInterrupt.ShouldInterrupt(context.Attack, TelegraphOverride.HasValue, zone.BlocksStagger))
                ApplyStagger(definition.WindupInterruptStaggerSeconds);

            flashRemaining = hitFlashSeconds;

            DamageResolved?.Invoke(new DamageReport(
                applied, context.Point, context.DamageType, guarded, context.HitstopSeconds));

            GameLog.Info(LogCategory.Enemy,
                $"hit {definition.Id}  -{applied:0.##} hp ({Health.Current:0.##}/{Health.Max:0.##})  " +
                $"poise {Poise.Poise:0.##}/{definition.PoiseMax:0.##} -> {poiseResult}" +
                (guarded ? $"  GUARDED - only '{definition.OnlyDamagedBy.Id}' can break the guard" : string.Empty) +
                (gate.Breaking ? "  GUARD BROKEN - ordinary attacks now damage this enemy" : string.Empty) +
                (definition.Tier == StaggerTier.Armored ? $"  armor {Poise.Armor:0.##}" : string.Empty) +
                (zone.IsNeutral ? string.Empty : $"  zone '{zone.Id}' x{zone.DamageMultiplier:0.00}"));
        }

        /// <summary>
        /// Takes an Echo repeat: damage arriving outside the hit resolver, from a hit that already
        /// landed (Denny's lane, BOONS.md §5).
        ///
        /// <para>Refused while the damage gate is up, for exactly the reason burn is: the guard
        /// promises nothing moves the health bar before the counter lands, and a repeat trickling
        /// past it would break that promise from a direction the gate never sees.</para>
        ///
        /// <para>Poise, knockback and stagger are all deliberately absent. A repeat is an echo of
        /// damage, not a second swing — giving it poise would let one combo stagger a body twice
        /// off a single read.</para>
        /// </summary>
        public void ApplyEchoRepeat(float damage, DamageType damageType)
        {
            if (!IsAlive || IsDying || damage <= 0f || IsGuarded)
                return;

            float applied = Health.TakeDamage(damage);
            flashRemaining = hitFlashSeconds;

            DamageResolved?.Invoke(new DamageReport(
                applied, transform.position + Vector3.up, damageType, false, 0f));

            GameLog.Debug(LogCategory.Enemy,
                $"echo repeat {definition.Id}  -{applied:0.##} hp ({Health.Current:0.##}/{Health.Max:0.##})");
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

        void OnEnable() => live.Add(this);

        void OnDisable()
        {
            live.Remove(this);

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
            TickDots(deltaTime);

            if (flashRemaining > 0f)
                flashRemaining -= deltaTime;
            if (crackFlashRemaining > 0f)
                crackFlashRemaining -= deltaTime;

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
            if (crackFlashRemaining > 0f)
                crackFlashRemaining -= deltaTime;

            ApplyColor();

            if (deathRemaining <= 0f)
                FinishDeath();
        }

        /// <summary>
        /// Advances every damage-over-time instance and pays out whatever whole damage came due.
        ///
        /// <para>Replaces the old single-duration burn. That model stored one timer that
        /// reapplication refreshed, so a second application of burn bought nothing, and rarity
        /// scaled how <em>long</em> it burned rather than how hard. Instances stack independently
        /// now, which is what makes one Undertow cast worth three of them.</para>
        /// </summary>
        void TickDots(float deltaTime)
        {
            if (Dots.Types.Count == 0)
                return;

            Dots.Tick(deltaTime);

            IReadOnlyList<IDotDefinition> types = Dots.Types;
            for (int i = 0; i < types.Count; i++)
            {
                IDotDefinition dot = types[i];

                // Hold the status flag in step with the stack. The flag carries no damage — it is
                // there so "bonus damage vs burning" boons and the body tint keep reading the same
                // answer they always did — and driving it from the longest live instance means the
                // DoT stays the single source of truth for when it ends.
                Statuses.Apply(dot.StatusFlag, Dots.LongestRemaining(dot));

                int due = Dots.DueWhole(dot);
                if (due > 0)
                    ApplyDotDamage(dot, due);
            }
        }

        /// <summary>
        /// Takes damage-over-time damage. Built to be INERT, and deliberately shaped like
        /// <see cref="ApplyEchoRepeat"/> rather than like a hit.
        ///
        /// <para>It never touches <see cref="HitResolver"/>, which is where per-hit cooldown
        /// reduction, the Echo cadence counter, the Flux crit roll and the Ward shield counter all
        /// listen — so a DoT cannot feed any of them. It never touches <see cref="PoiseSystem"/>,
        /// so it builds no stagger and chips no armour. It applies no status of its own, so burn
        /// can never apply burn. Those are not four checks that could be forgotten; they are four
        /// systems this method simply cannot reach.</para>
        ///
        /// <para>Refused while the damage gate is up, for the same reason burn always was: the
        /// guard promises nothing moves the health bar before the counter lands, and a DoT
        /// trickling past it would break that promise from a direction the gate never sees. The
        /// instances still burn down on their own clocks while it stands.</para>
        ///
        /// <para><b>No hit flash.</b> The flash means "a hit just landed". A body carrying several
        /// stacks pays out several times a second, and flashing on each would strobe; the status
        /// tint already colours the body for the whole duration, which is the continuous read this
        /// wants.</para>
        /// </summary>
        public void ApplyDotDamage(IDotDefinition dot, int damage)
        {
            if (dot == null || damage <= 0 || !IsAlive || IsDying || IsGuarded)
                return;

            float applied = Health.TakeDamage(damage);

            DamageResolved?.Invoke(new DamageReport(
                applied, transform.position + Vector3.up, dot.DamageType, false, 0f, dot));

            GameLog.Debug(LogCategory.Enemy,
                $"dot {dot.Id} on {definition.Id}  -{applied:0.##} hp ({Health.Current:0.##}/{Health.Max:0.##})  " +
                $"{Dots.StackCount(dot)} stack(s), {Dots.LongestRemaining(dot):0.0}s left");
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

        /// <summary>
        /// What one plated renderer looks like: amber and breathing while the plate is up, dull and
        /// dead once it is open.
        ///
        /// <para>Reads <see cref="DamageZone.IsIntactArmor"/> — the same property the hit pipeline
        /// reads — so a plate physically cannot look intact while resolving as soft. That is the
        /// reason cracking is a timer on the zone rather than a second component.</para>
        /// </summary>
        Color PlateColor(Color rest, DamageZone plate, float pulse, float crackFlash)
        {
            if (plate.IsIntactArmor)
            {
                Color amber = TelegraphPalette.Resolve(
                    platingPalette, TelegraphChannel.HardenedTime, new Color(1f, 0.67f, 0.13f, 1f));
                amber *= Mathf.Lerp(0.85f, 1f, pulse);
                amber.a = rest.a;
                return Color.Lerp(rest, amber, intactPlateBlend);
            }

            Color open = Color.Lerp(rest, crackedPlateColor, crackedPlateBlend);

            // The break flares white before settling, so the moment the weak point opens is a beat.
            return crackFlash > 0f ? Color.Lerp(open, Color.white, crackFlash) : open;
        }

        void ApplyColor()
        {
            if (renderers == null)
                return;

            float flash = hitFlashSeconds > 0f ? Mathf.Clamp01(flashRemaining / hitFlashSeconds) : 0f;
            float platePulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 0.8f * Mathf.PI * 2f);
            float crackFlash = crackFlashSeconds > 0f ? Mathf.Clamp01(crackFlashRemaining / crackFlashSeconds) : 0f;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;

                Color color = baseColors[i];

                // Plating is a property of the body's material, so it sits at the bottom of the
                // stack: a telegraph, a hit flash and a stagger all still read over the top of an
                // amber plate, exactly as they do over hide.
                DamageZone plate = plateForRenderer != null ? plateForRenderer[i] : null;
                if (plate != null)
                    color = PlateColor(color, plate, platePulse, crackFlash);

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
