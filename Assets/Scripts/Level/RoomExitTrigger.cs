using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// Forwards the player entering the exit volume to its room. Separate from
    /// <see cref="RoomInstance"/> so the trigger can sit on its own child collider without
    /// the room having to be a collider itself.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class RoomExitTrigger : MonoBehaviour
    {
        [SerializeField] RoomInstance room;
        [SerializeField] string playerTag = "Player";

        void Awake()
        {
            if (room == null)
                room = GetComponentInParent<RoomInstance>();
        }

        void OnTriggerEnter(Collider other)
        {
            if (room != null && other != null && other.CompareTag(playerTag))
                room.NotifyExitReached();
        }
    }
}
