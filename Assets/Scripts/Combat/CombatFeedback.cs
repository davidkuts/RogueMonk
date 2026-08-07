using System.Collections.Generic;
using Game.Core.Audio;
using Game.Core.Feedback;
using Game.Core.Player;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Turns combat events into sound, rumble and sparks.
    ///
    /// Kept in one place rather than sprinkled through the systems that raise the events, so
    /// the presentation layer stays removable and the simulation never learns about audio. It
    /// only subscribes; it never feeds anything back into gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatFeedback : MonoBehaviour
    {
        [SerializeField] PlayerAttackController attacks;
        [SerializeField] PlayerHealth health;
        [SerializeField] PlayerMotor motor;

        [Header("Sparks")]
        [SerializeField] HitSpark sparkPrefab;
        [SerializeField] int sparkPoolSize = 12;
        [SerializeField] Color lightHitColor = new Color(1f, 0.85f, 0.45f, 0.9f);
        [SerializeField] Color heavyHitColor = new Color(1f, 0.55f, 0.25f, 1f);

        [Header("Rumble")]
        [SerializeField, Tooltip("Hitstop above this counts as a heavy hit for feedback purposes.")]
        float heavyHitstopThreshold = 0.08f;
        [SerializeField] Vector2 lightRumble = new Vector2(0.25f, 0.45f);
        [SerializeField] Vector2 heavyRumble = new Vector2(0.6f, 0.8f);
        [SerializeField] Vector2 hurtRumble = new Vector2(0.7f, 0.3f);
        [SerializeField] Vector2 perfectDodgeRumble = new Vector2(0.15f, 0.7f);

        readonly List<HitSpark> sparks = new List<HitSpark>();
        bool wasDashing;

        void Awake()
        {
            if (attacks == null) attacks = GetComponent<PlayerAttackController>();
            if (health == null) health = GetComponent<PlayerHealth>();
            if (motor == null) motor = GetComponent<PlayerMotor>();

            if (sparkPrefab != null)
            {
                for (int i = 0; i < sparkPoolSize; i++)
                {
                    HitSpark spark = Instantiate(sparkPrefab);
                    spark.gameObject.SetActive(false);
                    sparks.Add(spark);
                }
            }

            if (attacks != null)
            {
                attacks.Hit += OnHit;
                attacks.Whiffed += OnWhiff;
                attacks.ComboLanded += OnComboLanded;
            }

            if (health != null)
            {
                health.Damaged += OnPlayerDamaged;
                health.PerfectDodged += OnPerfectDodge;
            }
        }

        void OnDestroy()
        {
            if (attacks != null)
            {
                attacks.Hit -= OnHit;
                attacks.Whiffed -= OnWhiff;
                attacks.ComboLanded -= OnComboLanded;
            }

            if (health != null)
            {
                health.Damaged -= OnPlayerDamaged;
                health.PerfectDodged -= OnPerfectDodge;
            }
        }

        void Update()
        {
            // The dash has no event of its own, so watch for the edge.
            bool dashing = motor != null && motor.Dash != null && motor.Dash.IsDashing;
            if (dashing && !wasDashing)
            {
                AudioDirector.PlaySound(GameSound.Dash);
                RumbleDirector.Rumble(0.15f, 0.25f);
            }

            wasDashing = dashing;
        }

        void OnHit(HitContext context)
        {
            bool heavy = context.HitstopSeconds >= heavyHitstopThreshold;

            AudioDirector.PlaySound(heavy ? GameSound.HitHeavy : GameSound.HitLight);

            Vector2 rumble = heavy ? heavyRumble : lightRumble;
            RumbleDirector.Rumble(rumble.x, rumble.y);

            SpawnSpark(context.Point, -context.Direction, heavy ? heavyHitColor : lightHitColor, heavy ? 1.4f : 1f);
        }

        void OnWhiff(IAttackDefinition attack) => AudioDirector.PlaySound(GameSound.Whiff);

        void OnComboLanded(int connectedSteps)
        {
            // Only a fully connected chain is worth celebrating.
            if (connectedSteps >= 3)
                RumbleDirector.Rumble(0.4f, 0.6f);
        }

        void OnPlayerDamaged(float amount)
        {
            AudioDirector.PlaySound(GameSound.PlayerHurt);
            RumbleDirector.Rumble(hurtRumble.x, hurtRumble.y);
        }

        void OnPerfectDodge()
        {
            AudioDirector.PlaySound(GameSound.PerfectDodge);
            RumbleDirector.Rumble(perfectDodgeRumble.x, perfectDodgeRumble.y);
        }

        void SpawnSpark(Vector3 position, Vector3 normal, Color tint, float scale)
        {
            for (int i = 0; i < sparks.Count; i++)
            {
                if (sparks[i] != null && !sparks[i].gameObject.activeSelf)
                {
                    sparks[i].Play(position, normal, tint, scale);
                    return;
                }
            }
            // Pool exhausted: dropping the spark beats allocating during a flurry of hits.
        }
    }
}
