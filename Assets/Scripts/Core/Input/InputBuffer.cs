using UnityEngine;

namespace Game.Core.Input
{
    /// <summary>
    /// Engine-free press buffer: holds a button press for a short window so an input made
    /// slightly too early still fires. Mandatory on attack and dash inputs (DESIGN.md).
    /// The window is supplied by the caller — never hardcode it here.
    /// </summary>
    public sealed class InputBuffer
    {
        float age;

        /// <summary>True while a press is queued and has not yet expired.</summary>
        public bool HasInput { get; private set; }

        /// <summary>Seconds since the queued press. Meaningless when <see cref="HasInput"/> is false.</summary>
        public float Age => age;

        public void Press()
        {
            HasInput = true;
            age = 0f;
        }

        /// <summary>Ages the queued press and drops it once it passes <paramref name="windowSeconds"/>.</summary>
        public void Tick(float deltaTime, float windowSeconds)
        {
            if (!HasInput || deltaTime <= 0f)
                return;

            age += deltaTime;
            if (age > Mathf.Max(0f, windowSeconds))
                Clear();
        }

        /// <summary>Consumes the queued press. Returns false when nothing was queued.</summary>
        public bool TryConsume()
        {
            if (!HasInput)
                return false;

            Clear();
            return true;
        }

        public void Clear()
        {
            HasInput = false;
            age = 0f;
        }
    }
}
