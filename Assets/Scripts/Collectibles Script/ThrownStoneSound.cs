using UnityEngine;
using UnityEngine.AI;

public class ThrownStoneSound : MonoBehaviour
{
    public AudioClip impactSound;
    public float attentionRadius = 15f;

    void OnCollisionEnter(Collision collision)
    {
        // Play impact sound
        AudioSource.PlayClipAtPoint(impactSound, transform.position, 1f);

        // Alert nearby enemies
        EnemyVision[] enemies = FindObjectsByType<EnemyVision>(FindObjectsSortMode.None);
        foreach (EnemyVision enemy in enemies)
        {
            if (Vector3.Distance(transform.position, enemy.transform.position) <= attentionRadius)
            {
                // Force enemy to investigate the sound position
                NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    agent.SetDestination(transform.position);
                }
            }
        }

        // Destroy stone object after impact
        Destroy(gameObject, 2f);
    }
}