using System;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>What a chunk of poise damage did.</summary>
    public enum PoiseResult
    {
        /// <summary>Poise (or armour) absorbed it and the enemy keeps acting.</summary>
        Absorbed = 0,

        /// <summary>The armour bar just ran out. Poise damage counts from the next hit on.</summary>
        ArmorStripped = 1,

        /// <summary>Poise broke: interrupt, hit reaction, brief vulnerability.</summary>
        Broken = 2,

        /// <summary>Immune tier. Feedback still plays, but nothing is interrupted.</summary>
        Immune = 3,

        /// <summary>Already staggered — further poise damage is not double-counted.</summary>
        AlreadyStaggered = 4,
    }

    /// <summary>
    /// Engine-free poise, armour and stagger, implementing the three tiers from
    /// DESIGN.md § Stagger tiers.
    ///
    /// Armour deliberately does <em>not</em> regenerate: stripping it is a one-off opening
    /// cost, so an Armored enemy is "tough to crack, normal thereafter" rather than a wall
    /// that resets every few seconds.
    /// </summary>
    public sealed class PoiseSystem
    {
        readonly IEnemyDefinition definition;

        float regenDelayRemaining;

        public PoiseSystem(IEnemyDefinition definition)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Poise = Mathf.Max(0f, definition.PoiseMax);
            Armor = definition.Tier == StaggerTier.Armored ? Mathf.Max(0f, definition.ArmorMax) : 0f;
        }

        public float Poise { get; private set; }

        public float Armor { get; private set; }

        public float StaggerRemaining { get; private set; }

        public bool IsStaggered => StaggerRemaining > 0f;

        /// <summary>True once an Armored enemy has had its armour broken off for good.</summary>
        public bool IsArmorStripped => definition.Tier != StaggerTier.Armored || Armor <= 0f;

        public float PoiseFraction => definition.PoiseMax <= 0f ? 0f : Mathf.Clamp01(Poise / definition.PoiseMax);

        public float ArmorFraction => definition.ArmorMax <= 0f ? 0f : Mathf.Clamp01(Armor / definition.ArmorMax);

        /// <summary>Raised when poise breaks, with the stagger duration that follows.</summary>
        public event Action<float> Broke;

        /// <summary>Raised the moment armour is stripped.</summary>
        public event Action ArmorStripped;

        /// <summary>Raised when a stagger ends and the enemy may act again.</summary>
        public event Action Recovered;

        public PoiseResult ApplyPoiseDamage(float amount)
        {
            if (definition.Tier == StaggerTier.Immune)
                return PoiseResult.Immune;

            if (IsStaggered)
                return PoiseResult.AlreadyStaggered;

            if (amount <= 0f)
                return PoiseResult.Absorbed;

            regenDelayRemaining = Mathf.Max(0f, definition.PoiseRegenDelay);

            if (definition.Tier == StaggerTier.Armored && Armor > 0f)
            {
                Armor -= amount;
                if (Armor > 0f)
                    return PoiseResult.Absorbed;

                // Overkill is absorbed by the armour break rather than carrying into poise:
                // stripping armour should be a distinct beat, not a stagger in disguise.
                Armor = 0f;
                ArmorStripped?.Invoke();
                return PoiseResult.ArmorStripped;
            }

            Poise -= amount;
            if (Poise > 0f)
                return PoiseResult.Absorbed;

            Poise = 0f;
            StaggerRemaining = Mathf.Max(0f, definition.StaggerDurationSeconds);
            Broke?.Invoke(StaggerRemaining);
            return PoiseResult.Broken;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            if (IsStaggered)
            {
                StaggerRemaining -= deltaTime;
                if (StaggerRemaining <= 0f)
                {
                    StaggerRemaining = 0f;
                    // The regen delay starts only once the enemy is back on its feet, so the
                    // vulnerability window is the stagger plus the delay, not one hidden inside
                    // the other.
                    regenDelayRemaining = Mathf.Max(0f, definition.PoiseRegenDelay);
                    Recovered?.Invoke();
                }

                return;
            }

            if (regenDelayRemaining > 0f)
            {
                if (regenDelayRemaining >= deltaTime)
                {
                    regenDelayRemaining -= deltaTime;
                    return;
                }

                // Carry the unused remainder into regeneration. Discarding it would make a
                // long frame silently swallow regen time.
                deltaTime -= regenDelayRemaining;
                regenDelayRemaining = 0f;
            }

            if (Poise < definition.PoiseMax)
                Poise = Mathf.Min(definition.PoiseMax, Poise + definition.PoiseRegenRate * deltaTime);
        }

        /// <summary>
        /// Forces a stagger regardless of the poise bar — the Undertow delivers what it pulled in
        /// already interrupted, whatever that enemy's poise happened to be.
        ///
        /// <para>Extends rather than restarts, so a second source can never <em>shorten</em> a
        /// stagger already running. <see cref="Broke"/> is raised only on the transition into a
        /// stagger: it means "you have just been interrupted", and firing it at something already
        /// on the floor would have controllers interrupt an action they are not taking.</para>
        /// </summary>
        /// <returns>True when this call is what put the enemy on the floor.</returns>
        public bool ForceStagger(float seconds)
        {
            if (definition.Tier == StaggerTier.Immune || seconds <= 0f)
                return false;

            bool wasStaggered = IsStaggered;
            StaggerRemaining = Mathf.Max(StaggerRemaining, seconds);

            if (wasStaggered)
                return false;

            Broke?.Invoke(StaggerRemaining);
            return true;
        }

        /// <summary>Ends a stagger early — used when the enemy dies mid-stagger.</summary>
        public void ClearStagger() => StaggerRemaining = 0f;
    }
}
