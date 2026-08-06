namespace Game.Combat
{
    /// <summary>
    /// Frame data and payload for a single attack. Implemented by a ScriptableObject at
    /// runtime and by fakes in tests, so the combat simulation never depends on an asset type.
    /// </summary>
    public interface IAttackDefinition
    {
        /// <summary>Stable identifier for logs and test assertions.</summary>
        string Id { get; }

        // --- Frame data (DESIGN.md § Attacks & combo) ---

        /// <summary>Committed wind-up. Never cancellable.</summary>
        float WindupSeconds { get; }

        /// <summary>Hitbox-live window.</summary>
        float ActiveSeconds { get; }

        /// <summary>Trailing recovery. Dash-cancellable when <see cref="CancellableOnRecovery"/>.</summary>
        float RecoverySeconds { get; }

        /// <summary>True for all player attacks — the skill-ceiling rule.</summary>
        bool CancellableOnRecovery { get; }

        /// <summary>How long after this attack ends the combo stays alive.</summary>
        float ComboWindowSeconds { get; }

        // --- Payload ---

        HitboxShape Hitbox { get; }
        float Damage { get; }
        DamageType DamageType { get; }
        float PoiseDamage { get; }
        float Knockback { get; }
        float HitstopSeconds { get; }

        // --- Aiming (Hades-style soft auto-aim) ---

        /// <summary>Half-angle is this over two: a target must lie inside this cone around facing.</summary>
        float AutoAimConeDegrees { get; }

        /// <summary>Maximum distance a target may be to attract auto-aim.</summary>
        float AutoAimRangeMeters { get; }

        /// <summary>Facing rotates toward the target at this rate during wind-up — never an instant snap.</summary>
        float AimSnapSpeedDegPerSec { get; }

        // --- Movement ---

        /// <summary>Walk speed multiplier while this attack runs. 0 roots the attacker.</summary>
        float MoveSpeedMultiplier { get; }
    }
}
