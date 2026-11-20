using UnityEngine;

// The ice gun lying on the map (same glass bubble as the other collectibles).
// Whoever walks up to it and has room in the bag takes it; it then shows in the inventory.
public class IceGunCollectible : MonoBehaviour
{
    public Sprite icon;
    [Tooltip("Played when the gun is fired at someone.")]
    public AudioClip gunSound;
    [Tooltip("Loops on the frozen player while the ice holds.")]
    public AudioClip crackSound;
    public Material iceMaterial;
    [Tooltip("Burst of ice when the cubes form and when they shatter.")]
    public GameObject iceVfx;
    public float freezeDuration = 5f;
    [Range(0.05f, 1f)] [Tooltip("Speed while frozen, as a fraction of normal speed.")]
    public float slowFactor = 0.35f;
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

            IceGunHolder holder = h.GetComponent<IceGunHolder>();
            if (holder == null) holder = h.gameObject.AddComponent<IceGunHolder>();
            if (holder.Collect(this)) { Destroy(gameObject); return; }
        }
    }
}
