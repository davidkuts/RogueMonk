using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Engine-free projectile travel: constant velocity along a fixed planar heading with a
    /// lifetime. Fired straight rather than homing — DESIGN.md's read-and-react rule means a
    /// projectile must be dodgeable by moving, which a homing shot is not.
    /// </summary>
    public sealed class ProjectileMotion
    {
        public ProjectileMotion(Vector3 origin, Vector3 direction, float speed, float lifetimeSeconds)
        {
            direction.y = 0f;
            Direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.forward;
            Position = origin;
            Speed = Mathf.Max(0f, speed);
            LifetimeRemaining = Mathf.Max(0f, lifetimeSeconds);
        }

        public Vector3 Position { get; private set; }

        public Vector3 Direction { get; }

        public float Speed { get; }

        public float LifetimeRemaining { get; private set; }

        public float DistanceTravelled { get; private set; }

        public bool Expired => LifetimeRemaining <= 0f;

        /// <summary>Advances the projectile and returns the step taken, for sweep tests.</summary>
        public Vector3 Tick(float deltaTime)
        {
            if (deltaTime <= 0f || Expired)
                return Vector3.zero;

            float step = Mathf.Min(deltaTime, LifetimeRemaining) * Speed;
            Vector3 delta = Direction * step;

            Position += delta;
            DistanceTravelled += step;
            LifetimeRemaining = Mathf.Max(0f, LifetimeRemaining - deltaTime);

            return delta;
        }

        public void Expire() => LifetimeRemaining = 0f;
    }
}
