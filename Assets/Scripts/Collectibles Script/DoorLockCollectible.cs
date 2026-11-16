using UnityEngine;

// The door lock lying on the map (same glass bubble as the other collectibles).
// Whoever walks up to it and has room in the bag takes it.
public class DoorLockCollectible : MonoBehaviour
{
    public Sprite icon;
    public AudioClip slamSound;
    [Tooltip("How long the shutter stays down (seconds).")]
    public float lockSeconds = 5f;
    [Tooltip("How close a player must get to grab it (metres, flat distance).")]
    public float pickupRadius = 0.9f;

    void Update()
    {
        foreach (Hider h in FindObjectsByType<Hider>(FindObjectsSortMode.None))
        {
            if (h == null || h.IsCaught || h.IsHidden) continue;
            Vector3 a = h.transform.position, b = transform.position;
            a.y = 0f; b.y = 0f;
            if (Vector3.Distance(a, b) > pickupRadius) continue;

            DoorLockHolder holder = h.GetComponent<DoorLockHolder>();
            if (holder == null) holder = h.gameObject.AddComponent<DoorLockHolder>();
            if (holder.Collect(icon, slamSound, lockSeconds)) { Destroy(gameObject); return; }
        }
    }
}
