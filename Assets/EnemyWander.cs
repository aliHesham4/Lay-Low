using UnityEngine;
using UnityEngine.AI;

public class EnemyWander : MonoBehaviour
{
    public float wanderRadius = 10f;
    public float waitTime = 2f;

    private NavMeshAgent agent;
    private Animator animator; // 1. Added Animator reference
    private float timer;
    private EnemyVision vision;
    private float stuckTime;
    private float nearStall;
    private NavMeshObstacle[] obstacles;


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
        vision = GetComponent<EnemyVision>();
        obstacles = FindObjectsByType<NavMeshObstacle>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        timer = waitTime;
        SetNewDestination();
    }

    void Update()
    {
        // 2. Pass current agent speed to the Animator (normalized 0 to 1)
        // EnemyVision drives the killer's animator (with real speeds); writing a different Speed here made the walk blend flicker
        if (animator != null && vision == null)
        {
            float speed = agent.velocity.magnitude / agent.speed;
            animator.SetFloat("Speed", speed);
        }

        // walking but going nowhere (held back by furniture / an obstacle): give up on this goal and pick another
        if (!agent.pathPending && agent.hasPath && !agent.isStopped && agent.remainingDistance > agent.stoppingDistance + 0.15f && agent.velocity.sqrMagnitude < 0.01f)
        {
            stuckTime += Time.deltaTime;
            if (stuckTime > 1.5f)
            {
                stuckTime = 0f;
                hasNoise = false;
                timer = 0f;
                SetNewDestination();
                return;
            }
        }
        else stuckTime = 0f;

        if (hasNoise)
        {
            Vector3 d = noiseTarget - transform.position; d.y = 0f;
            bool arrived = d.magnitude <= agent.stoppingDistance + 0.6f; // real distance, not the (possibly stale) path length
            if (arrived || Time.time > noiseTimeout) { hasNoise = false; timer = 0f; }
            else return; // keep walking to the noise
        }

        // Check if agent reached destination
        // arrived: right on the spot, or as close as furniture lets him get (avoidance holds him ~0.2-0.3 m off a bathtub, a table...)
        bool closeEnough = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.35f;
        if (!closeEnough && !agent.pathPending && agent.hasPath && agent.remainingDistance <= 0.8f && agent.velocity.sqrMagnitude < 0.0025f)
        {
            nearStall += Time.deltaTime;
            closeEnough = nearStall > 0.5f; // standing still right next to the goal: that's as close as it gets
        }
        else nearStall = 0f;
        if (closeEnough)
        {
            timer += Time.deltaTime;
            if (timer >= waitTime)
            {
                SetNewDestination();
                timer = 0f;
                nearStall = 0f;
            }
        }
    }

    void SetNewDestination()
    {
        // a few tries: skip spots inside furniture that has a nav obstacle but no hole in the navmesh (he could never reach them)
        NavMeshHit hit = new NavMeshHit();
        bool found = false;
        for (int i = 0; i < 8; i++)
        {
            Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
            randomDirection += transform.position;
            if (!NavMesh.SamplePosition(randomDirection, out hit, wanderRadius, NavMesh.AllAreas)) continue;
            found = true;
            if (!InsideObstacle(hit.position)) break;
        }
        if (found) agent.SetDestination(hit.position);
    }

    bool InsideObstacle(Vector3 p)
    {
        if (obstacles == null) return false;
        foreach (NavMeshObstacle o in obstacles)
        {
            if (o == null || o.carving || !o.enabled || !o.gameObject.activeInHierarchy) continue;
            Vector3 local = o.transform.InverseTransformPoint(p) - o.center;
            if (o.shape == NavMeshObstacleShape.Box)
            {
                Vector3 h = o.size * 0.5f; float m = 0.4f / Mathf.Max(0.01f, o.transform.lossyScale.x); // 0.4 m margin (his body width)
                if (Mathf.Abs(local.x) < h.x + m && Mathf.Abs(local.z) < h.z + m) return true;
            }
            else
            {
                float r = o.radius + 0.4f / Mathf.Max(0.01f, o.transform.lossyScale.x);
                if (local.x * local.x + local.z * local.z < r * r) return true;
            }
        }
        return false;
    }
}