using UnityEngine;

// The feather lying on the map (same glass bubble as the other collectibles).
// Whoever walks up to it and has room in the bag takes it; the feather then shows in the inventory.
public class FeatherCollectible : MonoBehaviour
{
    public Sprite icon;
    public AudioClip laughSound;
    [Tooltip("How far the laugh can be heard by the killer (metres).")]
    public float hearingRadius = 40f;
    [Range(0f, 1f)] public float laughVolume = 1f;
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

            FeatherHolder holder = h.GetComponent<FeatherHolder>();
            if (holder == null) holder = h.gameObject.AddComponent<FeatherHolder>();
            if (holder.Collect(icon, laughSound, hearingRadius, laughVolume)) { Destroy(gameObject); return; }
        }
    }
}
