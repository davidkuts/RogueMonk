using System;
using System.Collections.Generic;
using Game.Combat;
using Game.Core.Diagnostics;
using Game.Core.Economy;
using Game.Core.Player;
using Game.Core.Rng;
using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// Owns the run economy's moment-to-moment flow: seconds fragments and the minutes trickle
    /// on kills, the reward pickup a cleared room holds, the door that waits for it, and what
    /// each reward type actually does on the collecting press. The level director drives the
    /// room lifecycle and calls in; this component never advances rooms itself.
    ///
    /// <para>Content rolls (which giver, which Stray, which Stopgap) draw from the run's
    /// dedicated reward stream, never the run stream — their count depends on the doors the
    /// player happens to choose, and the run stream must stay reproducible from the seed.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RewardDirector : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField, Tooltip("The one asset holding every reward-system knob.")]
        RewardGenerationConfig config;
        [SerializeField, Tooltip("Per-kill currency tuning.")]
        EconomySettings economy;
        [SerializeField, Tooltip("Stopgap carry cap and reward pool.")]
        StopgapSettings stopgaps;
        [SerializeField, Tooltip("Strays a Stray reward can hand out. SO-only entries (no logic yet) are legal and simply inert when equipped.")]
        StrayDefinition[] strayPool = new StrayDefinition[0];
        [SerializeField, Tooltip("The transmission boon offering pool and draft rule.")]
        TransmissionCatalog transmissionCatalog;
        [SerializeField, Tooltip("Material for placeholder primitives (URP). Null builds one from the URP Unlit shader at runtime.")]
        Material primitiveMaterial;

        [Header("Player")]
        [SerializeField] PlayerWallet wallet;
        [SerializeField] PlayerHealth playerHealth;
        [SerializeField] StrayInventory strays;
        [SerializeField] StopgapInventory stopgapInventory;
        [SerializeField] TransmissionBoons transmissionBoons;
        [SerializeField] PlayerInputReader input;

        LevelDirector director;
        RoomRunner currentRunner;
        RoomInstance currentRoom;
        RewardChoice? currentReward;
        bool rewardBeforeEnemies;
        RewardPickup activePickup;

        /// <summary>Health at biome (level) entry — the Splice's rewind ceiling (REWARDS.md §3).</summary>
        float spliceCeiling;

        /// <summary>
        /// Set by the draft UI when it exists. Null falls back to an automatic first pick so a
        /// scene without the UI (the lab, a test) can still run a whole loop.
        /// </summary>
        public ITransmissionDraftPresenter DraftPresenter { get; set; }

        /// <summary>HUD feedback line ("+25 MINUTES"), consumed by the wallet HUD's float text.</summary>
        public event Action<string> RewardFeedback;

        public PlayerWallet Wallet => wallet;
        public StopgapInventory Stopgaps => stopgapInventory;
        public StrayInventory Strays => strays;
        public TransmissionBoons Transmissions => transmissionBoons;

        Material Primitive
        {
            get
            {
                if (primitiveMaterial == null)
                {
                    // Same shader the health bars survived URP with; building it here beats
                    // shipping magenta if the scene reference was forgotten.
                    var shader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (shader != null)
                        primitiveMaterial = new Material(shader);
                }

                return primitiveMaterial;
            }
        }

        /// <summary>Run setup: run-scoped currencies and inventories all reset. Meta survives.</summary>
        public void OnRunBegan(LevelDirector owner)
        {
            director = owner;

            if (wallet != null)
                wallet.Wallet.ResetRunScoped();

            if (strays != null)
                strays.ClearForNewRun();

            if (stopgapInventory != null)
                stopgapInventory.ClearForNewRun();

            if (transmissionBoons != null)
            {
                transmissionBoons.ClearForNewRun();

                // Flux rolls get their own stream off the run seed. It must be a DERIVED stream:
                // how often a crit is rolled depends on how the player fights, so spending run
                // draws on it would mean the same seed produced a different level for a different
                // player. Same rule as the reward stream beside it.
                if (owner != null && owner.Run != null)
                    transmissionBoons.BindRunStream(owner.Run.DeriveStream());
            }
        }

        /// <summary>Asks the policy asset what greets the player in the run's first room.</summary>
        public RewardChoice? DecideFirstRoomReward(IRandomSource rewardStream)
        {
            if (config == null || config.FirstRoomPolicy == null)
                return null;

            return config.FirstRoomPolicy.Decide(rewardStream);
        }

        /// <summary>
        /// Called by the level director as each room starts. Owns everything reward-shaped
        /// about the room from here: kill income, the pickup, and the held door.
        /// </summary>
        public void OnRoomStarted(
            RoomPlan plan, RoomInstance room, RoomRunner runner, RewardChoice? reward, bool preCombatReward)
        {
            DetachRunner();

            currentRunner = runner;
            currentRoom = room;
            currentReward = reward;
            rewardBeforeEnemies = preCombatReward;
            activePickup = null;

            if (runner != null)
            {
                runner.EnemyKilledActor += OnEnemyKilled;
                runner.Cleared += OnRoomCleared;
            }

            // Biome entry: the Splice's ceiling snapshots here. Levels are biomes for now, so
            // every level's first room re-snapshots — carried damage stays carried.
            if (plan != null && plan.Index == 0 && playerHealth != null)
            {
                spliceCeiling = playerHealth.CurrentHealth;
                GameLog.Info(LogCategory.Level, $"biome entry - splice ceiling {spliceCeiling:0.##} hp");
            }

            // The first room of a run: the reward is already waiting when the player walks in,
            // and the fight waits for the collect.
            if (preCombatReward && reward.HasValue)
                SpawnPickup(reward.Value);
        }

        void DetachRunner()
        {
            if (currentRunner == null)
                return;

            currentRunner.EnemyKilledActor -= OnEnemyKilled;
            currentRunner.Cleared -= OnRoomCleared;
            currentRunner = null;
        }

        void OnDestroy() => DetachRunner();

        /// <summary>
        /// The player is leaving the room, so every loose fragment on the floor comes with them.
        ///
        /// <para>Called by the level director on the transition, keeping the ownership rule this
        /// component is built on: the director drives the room lifecycle, the economy lives here.
        /// </para>
        /// </summary>
        public int SweepLooseFragmentsOnRoomExit() => CurrencyFragment.CollectAllForRoomExit(economy);

        readonly List<Game.Combat.GiverId> offerableGiverScratch = new List<Game.Combat.GiverId>();

        /// <summary>
        /// Snapshots what the player can currently make use of, for the door filter.
        ///
        /// <para>Sampled when a room's doors are built rather than when they are revealed, because
        /// the door COUNT has to be decided before the geometry goes in. In practice the player
        /// enters a room carrying the damage from the last one, so the healing test still reads a
        /// representative number — what it cannot see is damage taken inside this room.</para>
        /// </summary>
        public RewardOfferState BuildOfferState()
        {
            // Stopgaps: enabled pool entries whose own direction is still free. A disabled one is
            // not "available", so it must not count toward the ceiling.
            int takeable = 0;
            if (stopgaps != null && stopgapInventory != null)
            {
                IReadOnlyList<StopgapDefinition> grantable = stopgaps.Grantable;
                for (int i = 0; i < grantable.Count; i++)
                {
                    if (grantable[i] != null && !stopgapInventory.Has(grantable[i].Slot))
                        takeable++;
                }
            }

            float missing = 0f;
            if (playerHealth != null)
                missing = Mathf.Max(0f, playerHealth.MaxHealth - playerHealth.CurrentHealth);

            // What a Splice would actually restore right now, Stray multiplier included, so the
            // "would this be mostly wasted" test is measured against the real number.
            float spliceHeal = 0f;
            if (config != null)
            {
                RewardDefinition splice = config.FindDefinition(RewardType.Splice);
                if (splice != null)
                {
                    float depth = splice.Payload;
                    if (strays != null)
                        depth *= strays.SpliceDepthMultiplier;

                    // Payload is a fraction of the biome-entry ceiling (REWARDS.md §3).
                    //
                    // ⚠️ The ceiling snapshots in OnRoomStarted, which the level director calls
                    // AFTER it builds the room's doors — so on the first room of a level it is still
                    // 0 here. Left unguarded that makes the heal amount 0, `missing >= 0` trivially
                    // true, and the heal door survives at full health, which is exactly the offer
                    // this filter exists to remove. Max health is the right stand-in until the real
                    // snapshot lands.
                    float ceiling = spliceCeiling > 0f
                        ? spliceCeiling
                        : (playerHealth != null ? playerHealth.MaxHealth : 0f);

                    spliceHeal = depth * Mathf.Max(0f, ceiling);
                }
            }

            offerableGiverScratch.Clear();
            if (transmissionCatalog != null && transmissionBoons != null && config != null)
            {
                IReadOnlyList<Game.Combat.GiverId> givers = config.BoonGivers;
                for (int i = 0; givers != null && i < givers.Count; i++)
                {
                    if (transmissionCatalog.HasOfferableFor(givers[i], transmissionBoons.OwnedDefinitions))
                        offerableGiverScratch.Add(givers[i]);
                }
            }

            return new RewardOfferState(takeable, missing, spliceHeal, offerableGiverScratch);
        }

        /// <summary>
        /// Removes offers this player could get nothing from, and guarantees a door survives.
        /// Called by the level director as a room's exits are built.
        /// </summary>
        public void FilterOffers(List<RewardChoice> choices)
        {
            if (choices == null || config == null)
                return;

            int before = choices.Count;
            RewardOfferFilter.Filter(choices, BuildOfferState(), config.SpliceOfferThreshold);

            if (choices.Count != before)
            {
                GameLog.Info(LogCategory.Level,
                    $"door filter - {before} offer(s) -> {choices.Count} after dropping what this player " +
                    "could not use");
            }
        }

        void OnEnemyKilled(Game.Enemies.EnemyActor actor)
        {
            if (economy == null || wallet == null || actor == null || playerHealth == null)
                return;

            Vector3 position = actor.transform.position;

            // A big kill sheds several fragments in a deterministic ring rather than one fat
            // orb — the payout should look like what it is. No RNG, so seeds are untouched.
            int seconds = actor.SecondsOnKill;
            if (seconds > 0)
            {
                int perFragment = economy.SecondsPerFragment;
                int fragments = Mathf.Max(1, Mathf.CeilToInt(seconds / (float)perFragment));
                int remaining = seconds;

                for (int i = 0; i < fragments; i++)
                {
                    int carry = Mathf.Min(perFragment, remaining);
                    remaining -= carry;

                    float angle = i * 137.5f * Mathf.Deg2Rad;
                    Vector3 offset = i == 0
                        ? Vector3.zero
                        : new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.5f;

                    CurrencyFragment.Spawn(
                        position + offset, carry, CurrencyFragment.SecondsColor, 0.28f,
                        economy, playerHealth.transform, Primitive,
                        amount => wallet.Wallet.Add(CurrencyType.Seconds, amount));
                }
            }

            if (actor.MinutesOnKill > 0)
                wallet.Wallet.Add(CurrencyType.Minutes, actor.MinutesOnKill);

            // The Hades pattern: a beaten boss guarantees meta currency, dropped on the kill
            // itself — bigger, distinctly coloured fragments so the moment reads as the prize.
            if (actor.HoursOnKill > 0)
            {
                CurrencyFragment.Spawn(
                    position + new Vector3(-0.6f, 0f, 0f), actor.HoursOnKill,
                    CurrencyFragment.HoursColor, 0.45f, economy, playerHealth.transform, Primitive,
                    amount =>
                    {
                        wallet.Wallet.Add(CurrencyType.Hours, amount);
                        RewardFeedback?.Invoke($"+{amount} HOURS");
                    });
            }

            if (actor.AmberOnKill > 0)
            {
                CurrencyFragment.Spawn(
                    position + new Vector3(0.6f, 0f, 0f), actor.AmberOnKill,
                    CurrencyFragment.AmberColor, 0.45f, economy, playerHealth.transform, Primitive,
                    amount =>
                    {
                        wallet.Wallet.Add(CurrencyType.Amber, amount);
                        RewardFeedback?.Invoke($"+{amount} AMBER");
                    });
            }
        }

        void OnRoomCleared()
        {
            // Pre-combat rewards were paid on entry; the clear owes nothing further.
            if (rewardBeforeEnemies || !currentReward.HasValue)
                return;

            SpawnPickup(currentReward.Value);
        }

        void SpawnPickup(RewardChoice choice)
        {
            if (currentRoom == null || input == null || playerHealth == null)
            {
                GameLog.Error(LogCategory.Level, "reward pickup could not spawn - missing wiring; opening the door instead");
                FinishRewardFlow();
                return;
            }

            RewardDefinition definition = config != null ? config.FindDefinition(choice.Type) : null;
            Color tint = choice.HasPinnedGiver
                ? GiverPalette.ColorOf(choice.PinnedGiver)
                : config != null ? config.BandTint(choice.Band) : Color.white;

            activePickup = RewardPickup.Spawn(
                currentRoom.RewardSpawnPosition, choice, definition, tint,
                config != null ? config.PickupRadius : 2.5f,
                Primitive, input, playerHealth.transform);

            // Parented to the room so a restart or room swap tears it down with everything else.
            activePickup.transform.SetParent(currentRoom.transform, true);
            activePickup.Collected += OnPickupCollected;

            GameLog.Info(LogCategory.Level, $"reward pickup waiting  {choice}");
        }

        void OnPickupCollected(RewardChoice choice)
        {
            if (activePickup != null)
            {
                activePickup.Collected -= OnPickupCollected;
                Destroy(activePickup.gameObject);
                activePickup = null;
            }

            // A reward pays out once. Without this, the first room's clear would re-spawn the
            // pickup its entry already paid — the collect consumed the room's reward.
            currentReward = null;

            ApplyReward(choice, FinishRewardFlow);
        }

        /// <summary>
        /// After the reward's consequences have landed: open the held door, or release the
        /// held first-room fight. Asynchronous rewards (the draft) call this when they close.
        /// </summary>
        void FinishRewardFlow()
        {
            if (rewardBeforeEnemies)
            {
                rewardBeforeEnemies = false;
                if (director != null)
                    director.BeginHeldRoom();
                return;
            }

            if (currentRunner != null)
                currentRunner.OpenDoor();
        }

        void ApplyReward(RewardChoice choice, Action onComplete)
        {
            RewardDefinition definition = config != null ? config.FindDefinition(choice.Type) : null;
            IRandomSource stream = director != null ? director.RewardStream : null;

            switch (choice.Type)
            {
                case RewardType.MinutesCache:
                {
                    int amount = Mathf.RoundToInt(definition != null ? definition.Payload : 0f);
                    if (wallet != null)
                        wallet.Wallet.Add(CurrencyType.Minutes, amount);
                    RewardFeedback?.Invoke($"+{amount} MINUTES");
                    onComplete();
                    break;
                }

                case RewardType.HoursCache:
                {
                    int amount = Mathf.RoundToInt(definition != null ? definition.Payload : 0f);
                    if (wallet != null)
                        wallet.Wallet.Add(CurrencyType.Hours, amount);
                    RewardFeedback?.Invoke($"+{amount} HOURS");
                    onComplete();
                    break;
                }

                case RewardType.Splice:
                {
                    float depth = definition != null ? definition.Payload : 0f;
                    if (strays != null)
                        depth *= strays.SpliceDepthMultiplier;
                    if (playerHealth != null)
                        playerHealth.ApplySplice(depth, spliceCeiling);
                    RewardFeedback?.Invoke("SPLICE");
                    onComplete();
                    break;
                }

                case RewardType.Stopgap:
                {
                    StopgapDefinition drawn = DrawStopgap(stream);
                    if (drawn != null && stopgapInventory != null)
                    {
                        bool taken = stopgapInventory.TryAdd(drawn);
                        RewardFeedback?.Invoke(taken ? $"STOPGAP: {drawn.DisplayName}" : "STOPGAP FULL");
                    }

                    onComplete();
                    break;
                }

                case RewardType.Stray:
                {
                    StrayDefinition drawn = DrawStray(stream);
                    if (drawn != null && strays != null)
                    {
                        strays.Equip(drawn);
                        RewardFeedback?.Invoke($"STRAY: {drawn.DisplayName}");
                    }

                    onComplete();
                    break;
                }

                case RewardType.Transmission:
                    BeginTransmissionDraft(
                        choice.Band == RewardBand.EliteBoon,
                        choice.HasPinnedGiver ? choice.PinnedGiver : (GiverId?)null,
                        stream, onComplete);
                    break;

                default:
                    GameLog.Warn(LogCategory.Level, $"reward type {choice.Type} has no effect yet - collected for nothing");
                    onComplete();
                    break;
            }
        }

        /// <summary>
        /// A uniform draw over the ENABLED pool. A Stopgap switched off on its asset — Wound
        /// Spring, while the vortex cooldown is 0 and it has nothing to refund — can never be
        /// handed out, but keeps its asset and its logic for the day it makes sense again.
        /// </summary>
        StopgapDefinition DrawStopgap(IRandomSource stream)
        {
            if (stopgaps == null)
                return null;

            System.Collections.Generic.IReadOnlyList<StopgapDefinition> grantable = stopgaps.Grantable;
            if (grantable.Count == 0)
                return null;

            int index = stream != null ? stream.NextInt(0, grantable.Count) : 0;
            return grantable[index];
        }

        /// <summary>
        /// A uniform draw over the pool minus whatever is already equipped — offering the
        /// player the Stray they are holding would be a swap with no decision in it.
        /// </summary>
        StrayDefinition DrawStray(IRandomSource stream)
        {
            var candidates = new System.Collections.Generic.List<StrayDefinition>();
            for (int i = 0; i < strayPool.Length; i++)
            {
                if (strayPool[i] != null && (strays == null || strays.Equipped != strayPool[i]))
                    candidates.Add(strayPool[i]);
            }

            if (candidates.Count == 0)
                return null;

            int index = stream != null ? stream.NextInt(0, candidates.Count) : 0;
            return candidates[index];
        }

        void BeginTransmissionDraft(bool elite, GiverId? pinnedGiver, IRandomSource stream, Action onComplete)
        {
            if (transmissionCatalog == null || transmissionBoons == null)
            {
                GameLog.Warn(LogCategory.Level, "transmission reward with no catalog/loadout wired - skipped");
                onComplete();
                return;
            }

            var offer = elite
                ? transmissionCatalog.RollEliteDraft(stream, transmissionBoons.OwnedDefinitions)
                : transmissionCatalog.RollDraft(stream, transmissionBoons.OwnedDefinitions, pinnedGiver);

            if (pinnedGiver.HasValue && offer.Count > 0 && offer[0].Definition.Giver != pinnedGiver.Value)
                GameLog.Warn(LogCategory.Level,
                    $"the {pinnedGiver.Value} door's channel was exhausted - {offer[0].Definition.Giver} answered instead");

            if (offer.Count == 0)
            {
                GameLog.Info(LogCategory.Level, "every transmission boon is owned - the channel has nothing left to send");
                RewardFeedback?.Invoke("SIGNAL EXHAUSTED");
                onComplete();
                return;
            }

            Action<TransmissionOffer> grant = chosen =>
            {
                transmissionBoons.Grant(chosen.Definition, chosen.Rarity);
                RewardFeedback?.Invoke($"{chosen.Definition.DisplayName} [{chosen.Rarity}]");
                onComplete();
            };

            if (DraftPresenter == null || !DraftPresenter.Present(offer, grant))
            {
                // No UI in this scene: pick the front of the shuffled offer so the run can
                // continue. Logged loudly, because a silent auto-pick in the real game would
                // mean the draft screen broke.
                GameLog.Warn(LogCategory.Level, $"no draft presenter - auto-installing {offer[0].Definition.DisplayName}");
                grant(offer[0]);
            }
        }
    }
}
