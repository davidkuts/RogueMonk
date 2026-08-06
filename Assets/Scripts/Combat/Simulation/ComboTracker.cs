using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Engine-free combo cursor over an ordered attack sequence (punch → punch → kick).
    /// The window is measured from when an attack <em>ends</em>, not when it starts —
    /// measuring from the start would leave almost no window on a long attack.
    /// </summary>
    public sealed class ComboTracker
    {
        readonly IReadOnlyList<IAttackDefinition> sequence;

        float idleTime;
        float activeWindow;

        public ComboTracker(IReadOnlyList<IAttackDefinition> sequence)
        {
            if (sequence == null || sequence.Count == 0)
                throw new ArgumentException("A combo needs at least one attack.", nameof(sequence));

            this.sequence = sequence;
        }

        /// <summary>Position in the sequence that the next attack will use.</summary>
        public int Index { get; private set; }

        public int Length => sequence.Count;

        /// <summary>The attack a press would start right now.</summary>
        public IAttackDefinition Next => sequence[Index];

        /// <summary>Seconds left before the combo lapses back to the first attack. Zero when not counting.</summary>
        public float WindowRemaining => Index == 0 ? 0f : Mathf.Max(0f, activeWindow - idleTime);

        /// <summary>Length of the window currently being counted against, for HUD drain bars.</summary>
        public float WindowDuration => activeWindow;

        /// <summary>Raised when the final attack of the chain starts — the full combo went through.</summary>
        public event Action ChainCompleted;

        /// <summary>Raised when the window lapses mid-chain. Not raised by a manual <see cref="Reset"/>.</summary>
        public event Action ChainDropped;

        /// <summary>Call when an attack actually starts. Advances the cursor and arms the window.</summary>
        public IAttackDefinition Consume()
        {
            IAttackDefinition started = sequence[Index];
            activeWindow = Mathf.Max(0f, started.ComboWindowSeconds);
            idleTime = 0f;
            Index = (Index + 1) % sequence.Count;

            if (Index == 0)
                ChainCompleted?.Invoke();

            return started;
        }

        /// <summary>
        /// Ages the combo window. Only counts while no attack is running — a slow attack
        /// must not eat its own follow-up window.
        /// </summary>
        public void Tick(float deltaTime, bool isAttacking)
        {
            if (Index == 0 || isAttacking || deltaTime <= 0f)
                return;

            idleTime += deltaTime;
            if (idleTime > activeWindow)
            {
                Reset();
                ChainDropped?.Invoke();
            }
        }

        public void Reset()
        {
            Index = 0;
            idleTime = 0f;
            activeWindow = 0f;
        }
    }
}
