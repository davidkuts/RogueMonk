using UnityEngine;

namespace Game.Level
{
    /// <summary>An enemy the spawner can place, with its selection weight and budget cost.</summary>
    [CreateAssetMenu(menuName = "Monk/Enemy Archetype", fileName = "EnemyArchetype")]
    public sealed class EnemyArchetypeDefinition : ScriptableObject, IEnemyArchetype
    {
        [SerializeField] GameObject prefab;
        [SerializeField] float selectionWeight = 1f;
        [SerializeField, Tooltip("Budget cost. A wave of tough enemies is smaller than a wave of weak ones.")]
        float cost = 1f;
        [SerializeField, Tooltip("Swarm archetypes only (Scrapfeathers): members of one wave may share a spawn point, so a swarm's size is authored rather than capped by the room's point count. The runner fans out same-point spawns on placement.")]
        bool allowsSharedSpawnPoints;

        public string Id => name;
        public GameObject Prefab => prefab;
        public float SelectionWeight => selectionWeight;
        public float Cost => cost;
        public bool AllowsSharedSpawnPoints => allowsSharedSpawnPoints;
    }
}
