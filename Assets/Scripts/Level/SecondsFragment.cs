using System;
using Game.Core.Economy;
using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// One loose second shed by a kill: a small glowing sphere that drifts into the Second
    /// Hand once the player comes near (REWARDS.md §1 — Seconds auto-collect, no pickup
    /// friction). Drawn in the reserved dash hue because it is the Second Hand's doing, the
    /// same colour family as the whole time-kit.
    /// </summary>
    public sealed class SecondsFragment : MonoBehaviour
    {
        static readonly Color FragmentColor = new Color(0.3f, 0.9f, 1f);
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        EconomySettings settings;
        Transform player;
        Action<int> deliver;
        int amount;
        float speed;
        float age;

        public static SecondsFragment Spawn(
            Vector3 position, int amount, EconomySettings settings, Transform player,
            Material material, Action<int> deliver)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "SecondsFragment";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = position + Vector3.up * 0.5f;
            go.transform.localScale = Vector3.one * 0.28f;

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (material != null)
                renderer.sharedMaterial = material;

            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, FragmentColor);
            renderer.SetPropertyBlock(block);

            var fragment = go.AddComponent<SecondsFragment>();
            fragment.settings = settings;
            fragment.player = player;
            fragment.deliver = deliver;
            fragment.amount = amount;
            return fragment;
        }

        void Update()
        {
            if (settings == null || player == null)
                return;

            float deltaTime = Time.deltaTime;
            age += deltaTime;

            Vector3 toPlayer = player.position + Vector3.up * 0.8f - transform.position;
            float distance = toPlayer.magnitude;

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
            deliver?.Invoke(amount);
            Destroy(gameObject);
        }
    }
}
