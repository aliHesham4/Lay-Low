using UnityEngine;
using UnityEngine.AI;

public class ThrownStoneSound : MonoBehaviour
{
    public AudioClip impactSound;
    public float attentionRadius = 15f;
    bool alerted;


    void OnCollisionEnter(Collision collision)
    {
        // Play impact sound
        AudioSource.PlayClipAtPoint(impactSound, transform.position, 1f);

        // Alert the killer once (first impact). He decides himself whether to go:
        // ignored while chasing, remembered while killing / checking a spot, followed while wandering.
        if (!alerted)
        {
            alerted = true;
            foreach (EnemyVision enemy in FindObjectsByType<EnemyVision>(FindObjectsSortMode.None))
                enemy.HearNoise(transform.position, attentionRadius);
        }

        // Destroy stone object after impact
        Destroy(gameObject, 2f);
    }
}