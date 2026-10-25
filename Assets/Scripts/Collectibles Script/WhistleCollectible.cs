using UnityEngine;

// The party whistle lying on the map (same glass bubble as the other collectibles).
// Whoever walks up to it and has room in the bag takes it; the whistle then shows in the inventory.
public class WhistleCollectible : MonoBehaviour
{
    public Sprite icon;
    public AudioClip hornSound;
    [Tooltip("How far the horn can be heard by the killer (metres).")]
    public float hearingRadius = 40f;
    [Range(0f, 1f)] public float hornVolume = 1f;
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

            WhistleHolder holder = h.GetComponent<WhistleHolder>();
            if (holder == null) holder = h.gameObject.AddComponent<WhistleHolder>();
            if (holder.Collect(icon, hornSound, hearingRadius, hornVolume)) { Destroy(gameObject); return; }
        }
    }
}
