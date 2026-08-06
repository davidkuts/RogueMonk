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

        public string Id => name;
        public GameObject Prefab => prefab;
        public float SelectionWeight => selectionWeight;
        public float Cost => cost;
    }
}
