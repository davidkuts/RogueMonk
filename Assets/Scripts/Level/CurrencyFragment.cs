using System;
using System.Collections.Generic;
using Game.Core.Diagnostics;
using Game.Core.Economy;
using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// One loose piece of time shed by a kill: a small glowing sphere that drifts into the
    /// Second Hand once the player comes near (REWARDS.md §1 — auto-collect, no pickup
    /// friction). The colour says the denomination: dash-blue Seconds from everything, and the
    /// gold Hours / amber Amber a boss sheds — all delivered through the same drift.
    /// </summary>
    public sealed class CurrencyFragment : MonoBehaviour
    {
        /// <summary>Seconds wear the reserved dash hue — the Second Hand's own colour family.</summary>
        public static readonly Color SecondsColor = new Color(0.3f, 0.9f, 1f);

        /// <summary>Hours read as stabilized gold.</summary>
        public static readonly Color HoursColor = new Color(0.98f, 0.8f, 0.3f);

        /// <summary>Amber is literally time preserved solid — the semantic amber channel, correctly.</summary>
        public static readonly Color AmberColor = new Color(0.93f, 0.58f, 0.16f);

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>
        /// Every fragment currently in the air.
        ///
        /// <para>A live list rather than a <c>FindObjectsByType</c> sweep, because the room-exit
        /// collect happens on the exact frame a room is being torn down and rebuilt — the moment
        /// when a scene-wide search is least trustworthy and most expensive.</para>
        /// </summary>
        static readonly List<CurrencyFragment> live = new List<CurrencyFragment>();

        static readonly List<CurrencyFragment> sweepBuffer = new List<CurrencyFragment>();

        EconomySettings settings;
        Transform player;
        Action<int> deliver;
        int amount;
        float speed;
        float age;

        // --- room-exit sweep ---
        bool rushing;
        float rushElapsed;
        float rushSpeedMultiplier = 1f;
        float rushHardGrantSeconds;
        bool delivered;

        public static CurrencyFragment Spawn(
            Vector3 position, int amount, Color color, float scale, EconomySettings settings,
            Transform player, Material material, Action<int> deliver)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "CurrencyFragment";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = position + Vector3.up * 0.5f;
            go.transform.localScale = Vector3.one * scale;

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (material != null)
                renderer.sharedMaterial = material;

            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(block);

            var fragment = go.AddComponent<CurrencyFragment>();
            fragment.settings = settings;
            fragment.player = player;
            fragment.deliver = deliver;
            fragment.amount = amount;
            return fragment;
        }

        /// <summary>
        /// Sends every loose fragment home, fast, because the player is leaving the room.
        ///
        /// <para><b>The income was never actually at risk</b> — a fragment already delivers itself
        /// on its lifetime timer, so nothing was being lost. What was missing is that the player had
        /// no way to <em>know</em> that. Walking out on a floor still littered with time you earned
        /// reads as leaving money behind, and a reward the player believes they lost is a reward
        /// that did not land. So this is a legibility feature first: the fragments visibly streak
        /// after you as the door closes.</para>
        ///
        /// <para>The grant is guaranteed regardless of whether the visual finishes — see
        /// <see cref="Update"/> for the hard-grant deadline and <see cref="OnDestroy"/> for the case
        /// where the transition removes a fragment mid-flight.</para>
        /// </summary>
        public static int CollectAllForRoomExit(EconomySettings economy)
        {
            if (economy == null || live.Count == 0)
                return 0;

            // Iterated over a copy: delivering can destroy a fragment, which mutates the live list.
            sweepBuffer.Clear();
            sweepBuffer.AddRange(live);

            int swept = 0;
            for (int i = 0; i < sweepBuffer.Count; i++)
            {
                CurrencyFragment fragment = sweepBuffer[i];
                if (fragment == null || fragment.delivered || fragment.rushing)
                    continue;

                fragment.rushing = true;
                fragment.rushElapsed = 0f;
                fragment.rushSpeedMultiplier = economy.AutoCollectSpeedMultiplier;
                fragment.rushHardGrantSeconds = economy.AutoCollectHardGrantSeconds;
                swept++;
            }

            if (swept > 0)
            {
                GameLog.Info(LogCategory.Level,
                    $"room exit - sweeping {swept} loose fragment(s) home at x{economy.AutoCollectSpeedMultiplier:0.#} " +
                    $"(granted within {economy.AutoCollectHardGrantSeconds:0.00}s regardless)");
            }

            return swept;
        }

        /// <summary>How many fragments are still in the air. For tests and the debug overlay.</summary>
        public static int LiveCount => live.Count;

        void Awake() => live.Add(this);

        void OnDestroy()
        {
            live.Remove(this);

            // The transition tore this fragment down before it reached the player. The visual is
            // best-effort; the income is not, so pay it out on the way out. Only while rushing —
            // an ordinary fragment destroyed for any other reason has not been promised to anybody.
            if (rushing && !delivered)
            {
                GameLog.Debug(LogCategory.Level, "swept fragment removed mid-flight - granting anyway");
                Deliver();
            }
        }

        void Update()
        {
            if (settings == null || player == null)
                return;

            float deltaTime = Time.deltaTime;
            age += deltaTime;

            Vector3 toPlayer = player.position + Vector3.up * 0.8f - transform.position;
            float distance = toPlayer.magnitude;

            if (rushing)
            {
                rushElapsed += deltaTime;

                // The deadline. Whatever the visual managed, the player is paid.
                if (rushElapsed >= rushHardGrantSeconds || distance <= settings.FragmentCollectDistance)
                {
                    Collect();
                    return;
                }

                // Homes from any distance: the magnet radius is what governs an idle fragment, and
                // the whole point of the sweep is that being out of range no longer matters.
                speed = Mathf.Min(
                    settings.FragmentDriftSpeed * rushSpeedMultiplier,
                    speed + settings.FragmentDriftAcceleration * rushSpeedMultiplier * deltaTime);

                if (distance > 0.0001f)
                    transform.position += toPlayer / distance * (speed * deltaTime);

                return;
            }

            // A fragment left behind (killed through a door, knocked into a corner) delivers
            // itself rather than being lost: income the player earned must not depend on
            // walking back through a cleared room.
            if (age >= settings.FragmentMaxLifetimeSeconds || distance <= settings.FragmentCollectDistance)
            {
                Collect();
                return;
            }

            if (distance <= settings.SecondsMagnetRadius)
            {
                speed = Mathf.Min(
                    settings.FragmentDriftSpeed,
                    speed + settings.FragmentDriftAcceleration * deltaTime);
                transform.position += toPlayer / distance * (speed * deltaTime);
            }
            else
            {
                speed = 0f;

                // A gentle idle bob so an uncollected fragment still reads as alive.
                transform.position += Vector3.up * (Mathf.Sin(age * 3f) * 0.15f * deltaTime);
            }
        }

        void Collect()
        {
            Deliver();
            Destroy(gameObject);
        }

        /// <summary>Pays out exactly once, however many paths reach it.</summary>
        void Deliver()
        {
            if (delivered)
                return;

            delivered = true;
            deliver?.Invoke(amount);
        }
    }
}
