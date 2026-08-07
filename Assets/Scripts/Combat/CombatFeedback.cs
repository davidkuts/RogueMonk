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
        [SerializeField, Tooltip("Gold, matching the perfect-dodge trail that earned the counter — the two halves of one exchange should look like each other.")]
        Color riposteHitColor = new Color(1f, 0.9f, 0.4f, 1f);
        [SerializeField, Tooltip("The riposte's spark is deliberately the biggest in the game.")]
        float riposteSparkScale = 2.6f;
        [SerializeField] AttackDefinition riposte;

        [Header("Elements")]
        [SerializeField, Tooltip("Spark colour per damage type, indexed by the DamageType enum. Physical falls through to the light/heavy colours above.")]
        Color[] elementColors =
        {
            new Color(1f, 0.85f, 0.45f, 0.9f),   // Physical (unused — falls through)
            new Color(1f, 0.45f, 0.15f, 1f),     // Fire
            new Color(0.45f, 0.8f, 1f, 1f),      // Ice
            new Color(0.7f, 0.95f, 0.9f, 1f),    // Wind
            new Color(0.85f, 0.7f, 0.45f, 1f),   // Earth
            new Color(0.5f, 0.85f, 0.35f, 1f),   // Nature
            new Color(1f, 0.85f, 0.35f, 1f),     // Force
        };

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
            // The counter gets its own everything. It is the payoff for the hardest thing the
            // player can do, and the previous version failed precisely because it looked and
            // sounded like an ordinary punch.
            bool isRiposte = riposte != null && context.Attack != null && context.Attack.Id == riposte.Id;
            if (isRiposte)
            {
                AudioDirector.PlaySound(GameSound.Riposte);
                RumbleDirector.Rumble(1f, 1f);
                SpawnSpark(context.Point, -context.Direction, riposteHitColor, riposteSparkScale);
                return;
            }

            bool heavy = context.HitstopSeconds >= heavyHitstopThreshold;

            AudioDirector.PlaySound(heavy ? GameSound.HitHeavy : GameSound.HitLight);

            Vector2 rumble = heavy ? heavyRumble : lightRumble;
            RumbleDirector.Rumble(rumble.x, rumble.y);

            // DamageType has been carried on every hit since M3 with nothing reading it. An
            // elemental boon that did not change what a hit looks like would be another invisible
            // reward, so the spark takes the element's colour.
            Color tint = ResolveSparkColor(context.DamageType, heavy);
            SpawnSpark(context.Point, -context.Direction, tint, heavy ? 1.4f : 1f);
        }

        Color ResolveSparkColor(DamageType type, bool heavy)
        {
            int index = (int)type;
            if (type != DamageType.Physical && elementColors != null &&
                index >= 0 && index < elementColors.Length)
                return elementColors[index];

            return heavy ? heavyHitColor : lightHitColor;
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
