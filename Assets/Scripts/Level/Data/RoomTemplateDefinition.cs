using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// A hand-authored room prefab plus the facts the generator needs about it. DESIGN.md
    /// forbids procedural geometry — this only describes an authored room, never builds one.
    /// </summary>
    [CreateAssetMenu(menuName = "Monk/Room Template", fileName = "RoomTemplate")]
    public sealed class RoomTemplateDefinition : ScriptableObject, IRoomTemplate
    {
        [SerializeField, Tooltip("The authored room prefab. Must carry a RoomInstance.")]
        RoomInstance prefab;
        [SerializeField, Tooltip("Relative likelihood of being picked. 0 excludes it entirely.")]
        float selectionWeight = 1f;
        [SerializeField, Tooltip("Which roles this authored room can serve. The boss room is always last and always exactly one per level.")]
        RoomRole supportedRoles = RoomRole.Standard;

        public string Id => name;
        public RoomInstance Prefab => prefab;
        public float SelectionWeight => selectionWeight;
        public RoomRole SupportedRoles => supportedRoles;

        /// <summary>
        /// Read off the prefab rather than typed in, so the generator can never disagree with
        /// the geometry about how many enemies a room can hold.
        /// </summary>
        public int SpawnPointCount => prefab != null ? prefab.SpawnPointCount : 0;
    }
}
