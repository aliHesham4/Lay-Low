using UnityEngine;

// Pepper pickup: whoever touches it gets a short speed boost.
public class PepperCollectible : MonoBehaviour
{
    public float boostSpeed = 3.2f;
    public float boostDuration = 5f;
    [Tooltip("How close a player must get to grab it (metres, flat distance).")]
    public float pickupRadius = 0.9f;

    void Update()
    {
        Hider[] players = FindObjectsByType<Hider>(FindObjectsSortMode.None);
        foreach (Hider h in players)
        {
            if (h == null || h.IsCaught) continue;
            Vector3 a = h.transform.position, b = transform.position;
            a.y = 0f; b.y = 0f;
            if (Vector3.Distance(a, b) <= pickupRadius)
            {
                SpeedBoost.Apply(h.gameObject, boostSpeed, boostDuration);
                Destroy(gameObject);
                return;
            }
        }
    }
}
