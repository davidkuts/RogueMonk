using System;
using Game.Core.Diagnostics;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// The one Stray slot (REWARDS.md §4): equip on pickup, replace-on-pickup, the replaced
    /// Stray is destroyed. Equipping installs the passive; replacing must remove the old
    /// passive COMPLETELY — a stale modifier left on the resolver would be an invisible,
    /// permanent buff, which is worse than a bug that shows.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerAttackController))]
    public sealed class StrayInventory : MonoBehaviour
    {
        [SerializeField] PlayerAttackController attacks;
        [SerializeField] PlayerHealth health;

        EraDamageModifier eraModifier;
        bool shieldHookActive;

        public StrayDefinition Equipped { get; private set; }

        /// <summary>Raised on every equip/replace/clear, for HUD and the debug overlay.</summary>
        public event Action Changed;

        /// <summary>
        /// Splice depth multiplier from the equipped Stray (Linen Scrap). 1 when none applies.
        /// Read by whoever resolves a Splice.
        /// </summary>
        public float SpliceDepthMultiplier =>
            Equipped != null && Equipped.Kind == StrayKind.SpliceDepthBonus
                ? 1f + Mathf.Max(0f, Equipped.Value)
                : 1f;

        void Awake()
        {
            if (attacks == null) attacks = GetComponent<PlayerAttackController>();
            if (health == null) health = GetComponent<PlayerHealth>();
        }

        void OnDestroy() => RemovePassive();

        public void Equip(StrayDefinition stray)
        {
            if (stray == null)
                return;

            StrayDefinition replaced = Equipped;
            RemovePassive();
            Equipped = stray;
            InstallPassive();

            GameLog.Info(LogCategory.Combat,
                replaced != null
                    ? $"STRAY SWAPPED  {replaced.DisplayName} -> {stray.DisplayName} ({stray.Kind}) - the old one is lost"
                    : $"STRAY EQUIPPED  {stray.DisplayName} ({stray.Kind})");

            Changed?.Invoke();
        }

        public void ClearForNewRun()
        {
            RemovePassive();
            Equipped = null;
            Changed?.Invoke();
        }

        void InstallPassive()
        {
            if (Equipped == null)
                return;

            switch (Equipped.Kind)
            {
                case StrayKind.EraDamageBonus:
                    if (attacks != null && Equipped.TargetEra != Era.None)
                    {
                        eraModifier = new EraDamageModifier(Equipped.TargetEra, 1f + Mathf.Max(0f, Equipped.Value));
                        attacks.Resolver.AddModifier(eraModifier);
                    }

                    break;

                case StrayKind.ShieldAfterPerfectDodge:
                    if (health != null)
                    {
                        health.PerfectDodged += OnPerfectDodge;
                        shieldHookActive = true;
                    }

                    break;

                // SpliceDepthBonus is pull-based: the splice resolver asks this inventory.
                // NotYetImplemented carries no logic by definition.
            }
        }

        void RemovePassive()
        {
            if (eraModifier != null && attacks != null)
            {
                attacks.Resolver.RemoveModifier(eraModifier);
                eraModifier = null;
            }

            if (shieldHookActive && health != null)
            {
                health.PerfectDodged -= OnPerfectDodge;
                shieldHookActive = false;
            }

            // A shield already armed by the outgoing Stray goes with it: "removes the old
            // passive completely" includes its stored charge.
            if (health != null && Equipped != null && Equipped.Kind == StrayKind.ShieldAfterPerfectDodge)
                health.RevokeOneHitShield();
        }

        void OnPerfectDodge()
        {
            if (health != null)
                health.GrantOneHitShield();
        }
    }
}
