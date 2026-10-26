using UnityEngine;
using UnityEngine.AI;

public class EnemyWander : MonoBehaviour
{
    public float wanderRadius = 10f;
    public float waitTime = 2f;

    private NavMeshAgent agent;
    private Animator animator; // 1. Added Animator reference
    private float timer;

    // Heard a noise: walk there first, then carry on wandering
    private bool hasNoise;
    private float noiseTimeout;
    private Vector3 noiseTarget;


    public bool HasNoise { get { return hasNoise; } }

    public bool GoTo(Vector3 p) { return GoTo(p, 20f); }

    public bool GoTo(Vector3 p, float giveUpAfter)
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        NavMeshHit hit;
        if (agent == null || !agent.isOnNavMesh) return false;
        if (NavMesh.SamplePosition(p, out hit, 3f, NavMesh.AllAreas))
        {
            hasNoise = true;
            noiseTimeout = Time.time + giveUpAfter;
            noiseTarget = hit.position;
            agent.SetDestination(hit.position);
            return true;
        }
        return false;
    }

    // Forget the sound he was walking to and go back to normal wandering.
    public void CancelNoise()
    {
        if (!hasNoise) return;
        hasNoise = false;
        timer = 0f;
        if (agent != null && agent.isOnNavMesh) SetNewDestination(); // stop walking to the old sound
    }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>(); // Finds Animator on this or child mesh
        timer = waitTime;
        SetNewDestination();
    }

    void Update()
    {
        // 2. Pass current agent speed to the Animator (normalized 0 to 1)
        if (animator != null)
        {
            float speed = agent.velocity.magnitude / agent.speed;
            animator.SetFloat("Speed", speed);
        }

        if (hasNoise)
        {
            Vector3 d = noiseTarget - transform.position; d.y = 0f;
            bool arrived = d.magnitude <= agent.stoppingDistance + 0.6f; // real distance, not the (possibly stale) path length
            if (arrived || Time.time > noiseTimeout) { hasNoise = false; timer = 0f; }
            else return; // keep walking to the noise
        }

        // Check if agent reached destination
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            timer += Time.deltaTime;
            if (timer >= waitTime)
            {
                SetNewDestination();
                timer = 0f;
            }
        }
    }

    void SetNewDestination()
    {
        Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
        randomDirection += transform.position;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, wanderRadius, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }
}