using System.Collections.Generic;
using Game.Combat;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// One entry in an ordinary enemy's repertoire. A plain serializable class rather than its own
    /// asset, for the same reason <see cref="BossMove"/> is: a move is only ever meaningful inside
    /// the enemy that owns it.
    /// </summary>
    [System.Serializable]
    public sealed class EnemyMove : IEnemyMove
    {
        [SerializeField, Tooltip("Identifier for logs and telegraph debugging.")]
        string id = "Move";

        [SerializeField, Tooltip("Played in order. One = a single swing. Two or more = a scripted chain that always completes.")]
        AttackDefinition[] links = new AttackDefinition[0];

        [SerializeField, Tooltip("Gap between one link ending and the next starting. Swiftjaw's second snap is deliberately longer — the rhythm trap.")]
        float linkDelaySeconds = 0.18f;

        [Header("When this move is legal")]
        [SerializeField, Tooltip("Closest distance at which this move may be chosen.")]
        float minRange;
        [SerializeField, Tooltip("Furthest distance at which this move may be chosen.")]
        float maxRange = 2.4f;
        [SerializeField, Tooltip("Relative likelihood among the legal moves. 0 disables it.")]
        float selectionWeight = 1f;
        [SerializeField, Tooltip("Enforced gap before this specific move may be chosen again.")]
        float moveCooldownSeconds = 2f;

        [Header("Delivery")]
        [SerializeField, Tooltip("How far the attacker travels across each link's active frames. 0 roots it.")]
        float lungeDistance;

        [Header("Projectiles (0 count = a melee move)")]
        [SerializeField, Tooltip("What this move throws. Per-move rather than per-enemy, because Sailspit's glob and its spines are different objects with different payloads.")]
        Projectile projectilePrefab;

        [SerializeField, Tooltip("How many go out when the active window opens. 1 is a single shot; 3-5 is a fan whose GAPS are the dash lanes.")]
        int projectileCount;

        [SerializeField, Tooltip("Total fan width, centred on facing. Ignored for a single shot.")]
        float projectileSpreadDegrees = 36f;

        [SerializeField, Tooltip("Overrides the enemy's RangedProfile speed for this move only. 0 uses the profile. A lobbed glob and a flicked spine should not travel at the same speed.")]
        float projectileSpeedOverride;

        [Header("Lob (area denial)")]
        [SerializeField, Tooltip("ON turns this move from a shot AT the player into a shot at the GROUND near them: no contact damage, all the threat in whatever it leaves behind. Off leaves the ordinary aimed projectile untouched.")]
        bool lobAtGround;

        [SerializeField, Tooltip("Closest the landing point may be to the player. Above zero so the lob cannot simply be a point-blank hit wearing a different name.")]
        float lobMinRadius = 1.6f;

        [SerializeField, Tooltip("Furthest the landing point may be. Wide enough that the player cannot stand still and predict it; tight enough that it still costs them the ground they wanted.")]
        float lobMaxRadius = 4.5f;

        [SerializeField, Tooltip("Seconds in the air. This is the reaction window — the landing ring is drawn for the whole of it, so this is how long the player has to leave.")]
        float lobAirtimeSeconds = 1.1f;

        [SerializeField, Tooltip("How many times to re-roll a landing point that is off the floor or inside a wall before giving up and using the last ring point anyway. It never falls back to the player's own position — a glob clipping scenery is cosmetic, a glob landing on the player is a guaranteed hit.")]
        int lobWalkableAttempts = 8;

        public bool LobAtGround => lobAtGround;
        public float LobMinRadius => Mathf.Max(0f, lobMinRadius);
        public float LobMaxRadius => Mathf.Max(LobMinRadius, lobMaxRadius);
        public float LobAirtimeSeconds => Mathf.Max(0.05f, lobAirtimeSeconds);
        public int LobWalkableAttempts => Mathf.Max(1, lobWalkableAttempts);

        IReadOnlyList<IAttackDefinition> cachedLinks;

        public string Id => id;
        public float LinkDelaySeconds => linkDelaySeconds;
        public float MinRange => minRange;
        public float MaxRange => maxRange;
        public float SelectionWeight => selectionWeight;
        public float MoveCooldownSeconds => moveCooldownSeconds;
        public float LungeDistance => lungeDistance;

        /// <summary>Null for a melee move. Deliberately off <see cref="IEnemyMove"/>: the brain
        /// decides <em>which</em> move, and how one is delivered is the adapter's business.</summary>
        public Projectile ProjectilePrefab => projectilePrefab;

        public int ProjectileCount => projectileCount;

        public float ProjectileSpreadDegrees => projectileSpreadDegrees;

        /// <summary>True when this move throws something rather than swinging.</summary>
        public bool IsRanged => projectilePrefab != null && projectileCount > 0;

        /// <summary>
        /// This move's <see cref="RangedProfile"/>, with the per-move speed folded in. The rest of
        /// the profile — radius, lifetime, preferred bands — stays the enemy's, since those are
        /// properties of the creature rather than of one attack.
        /// </summary>
        public RangedProfile ResolveProfile(RangedProfile enemyProfile)
        {
            if (projectileSpeedOverride <= 0f)
                return enemyProfile;

            enemyProfile.ProjectileSpeed = projectileSpeedOverride;
            return enemyProfile;
        }

        public IReadOnlyList<IAttackDefinition> Links
        {
            get
            {
                if (cachedLinks == null)
                {
                    // Drop null entries here rather than making every consumer null-check a link.
                    var built = new List<IAttackDefinition>(links.Length);
                    for (int i = 0; i < links.Length; i++)
                    {
                        if (links[i] != null)
                            built.Add(links[i]);
                    }
                    cachedLinks = built;
                }

                return cachedLinks;
            }
        }

        /// <summary>Drops the cached projection so inspector edits take effect without a reload.</summary>
        public void InvalidateCache() => cachedLinks = null;
    }

    /// <summary>
    /// Tuning for a Biome 1 archetype: an <see cref="EnemyDefinition"/> plus a moveset and the
    /// attack-token settings that decide how many of them may swing at once.
    ///
    /// <para>Subclasses rather than widens, exactly as <see cref="BossDefinition"/> does. The two
    /// shipped trash assets keep the single-<c>Attack</c> shape they have always had and are not
    /// touched; anything with more than one move opts in by being this type instead.</para>
    ///
    /// <para>The inherited <c>Attack</c> field stays meaningful: it is the fallback for a body
    /// with no moves authored yet, so a half-built enemy still telegraphs and swings rather than
    /// standing inert and looking like a bug.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Monk/Biome Enemy Definition", fileName = "BiomeEnemyDefinition")]
    public class BiomeEnemyDefinition : EnemyDefinition
    {
        [Header("Moveset")]
        [SerializeField] EnemyMove[] moves = new EnemyMove[0];

        [SerializeField, Range(0f, 1f), Tooltip("Weight applied to the move just used. Discourages repeats without ever forbidding them. 1 disables the effect.")]
        float repeatWeightMultiplier = 0.45f;

        [Header("Attack tokens")]
        [SerializeField, Tooltip("Which global queue this archetype competes in. Melee and ranged are capped separately.")]
        AttackTokenKind tokenKind = AttackTokenKind.Melee;

        [SerializeField, Tooltip("How many of THIS archetype may attack at once, on top of the global cap. ENEMIES_BIOME1.md caps Swiftjaw at 2 whatever the pack size. 0 means no per-archetype limit.")]
        int concurrentAttackerCap;

        [SerializeField, Tooltip("Random delay before this body's FIRST attack, on top of the spawn grace. Keeps a pack spawned in one frame from running identical timers forever. 0 disables it.")]
        float initialCooldownJitterSeconds;

        [Header("Steering")]
        [SerializeField, Tooltip("Turn rate while chasing. Facing locks when a wind-up starts, so this never lets a telegraph track the player.")]
        float turnSpeedDegPerSec = 540f;

        [SerializeField, Tooltip("Distance this enemy WANTS to fight at. 0 keeps the default, which is to close to its shortest-reach move. Set it to a ranged move's band and the enemy holds there, only using its close-range moves when the player forces the issue.")]
        float preferredRange;

        [Header("Circling")]
        [SerializeField, Range(0f, 1f), Tooltip("Speed used while in range but not allowed to attack — waiting on a token or a cooldown. 0 makes it stand still, which reads as broken on a pack animal.")]
        float circleSpeedFraction;

        [SerializeField, Tooltip("Distance it tries to hold while circling. Wants to sit outside its own attack range, so closing is a decision rather than a drift.")]
        float circleRadius = 3.2f;

        IReadOnlyList<IEnemyMove> cachedMoves;

        public float RepeatWeightMultiplier => repeatWeightMultiplier;
        public AttackTokenKind TokenKind => tokenKind;
        public int ConcurrentAttackerCap => concurrentAttackerCap;
        public float InitialCooldownJitterSeconds => initialCooldownJitterSeconds;
        public float TurnSpeedDegPerSec => turnSpeedDegPerSec;
        public float PreferredRange => preferredRange;
        public float CircleSpeedFraction => circleSpeedFraction;
        public float CircleRadius => circleRadius;

        public IReadOnlyList<IEnemyMove> Moves
        {
            get
            {
                if (cachedMoves == null)
                {
                    var built = new List<IEnemyMove>(moves.Length);

                    for (int i = 0; i < moves.Length; i++)
                    {
                        // A move with no links could be chosen and then do nothing, hanging the
                        // brain on an attack that never starts. Drop those here, once.
                        if (moves[i] != null && moves[i].Links.Count > 0)
                            built.Add(moves[i]);
                    }

                    // Nothing authored yet: fall back to the inherited single attack so a
                    // part-built enemy still fights instead of standing there looking broken.
                    if (built.Count == 0 && Attack != null)
                        built.Add(new FallbackMove(this));

                    cachedMoves = built;
                }

                return cachedMoves;
            }
        }

        void OnValidate()
        {
            cachedMoves = null;
            for (int i = 0; i < moves.Length; i++)
                moves[i]?.InvalidateCache();
        }

        /// <summary>
        /// Wraps the inherited single <c>Attack</c> as a one-move repertoire, using the
        /// definition's own attack range as its band. Exists so <see cref="BiomeEnemyDefinition"/>
        /// is usable the moment it is created, before any move has been authored.
        /// </summary>
        sealed class FallbackMove : IEnemyMove
        {
            readonly BiomeEnemyDefinition owner;
            readonly IAttackDefinition[] links;

            public FallbackMove(BiomeEnemyDefinition owner)
            {
                this.owner = owner;
                links = new[] { owner.Attack };
            }

            public string Id => "fallback";
            public IReadOnlyList<IAttackDefinition> Links => links;
            public float LinkDelaySeconds => 0f;
            public float MinRange => 0f;
            public float MaxRange => owner.AttackRange;
            public float SelectionWeight => 1f;
            public float MoveCooldownSeconds => 0f;
            public float LungeDistance => owner.LungeDistance;
        }
    }
}
