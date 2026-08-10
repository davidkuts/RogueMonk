using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// Forwards the player reaching the exit volume to its room.
    ///
    /// Deliberately does not rely on <c>OnTriggerEnter</c> alone. The collider is disabled
    /// until the room is cleared, and Unity raises no enter event for an overlap that already
    /// existed when the collider was switched on — so killing the last enemy while standing in
    /// the doorway left the player stuck in a cleared room until they wandered out and back in.
    /// An explicit containment check each frame closes that hole.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class RoomExitTrigger : MonoBehaviour
    {
        [SerializeField] RoomInstance room;
        [SerializeField] string playerTag = "Player";

        Collider volume;
        Transform player;
        bool notified;

        /// <summary>
        /// Which of the room's exit offers this door is. Assigned when the exit bank is built;
        /// the authored template trigger keeps 0, which is only ever read on the legacy
        /// single-door path where there is nothing to choose anyway.
        /// </summary>
        public int ExitIndex { get; set; }

        void Awake()
        {
            volume = GetComponent<Collider>();
            if (room == null)
                room = GetComponentInParent<RoomInstance>();
        }

        void OnEnable() => notified = false;

        void OnTriggerEnter(Collider other)
        {
            if (other != null && other.CompareTag(playerTag))
                Notify();
        }

        void Update()
        {
            if (notified || volume == null || !volume.enabled)
                return;

            if (player == null)
            {
                GameObject found = GameObject.FindWithTag(playerTag);
                if (found == null)
                    return;

                player = found.transform;
            }

            // ClosestPoint returns the query point itself when it lies inside the collider.
            // Guarded on volume.enabled because a disabled collider returns the point too.
            Vector3 position = player.position;
            if ((volume.ClosestPoint(position) - position).sqrMagnitude < 1e-6f)
                Notify();
        }

        void Notify()
        {
            if (notified || room == null)
                return;

            notified = true;
            room.NotifyExitReached(ExitIndex);
        }
    }
}
