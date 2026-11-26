using UnityEngine;
using UnityEngine.AI;

// Safety net for the killer: if he is trying to walk somewhere but is not getting anywhere
// (wedged on furniture, a doorway, a corner...) it frees him step by step:
//   1. throw the path away and ask for it again,
//   2. nudge him half a metre along the path,
//   3. give up on that destination and pick a new one.
// It also puts him back on the navmesh if he ever ends up off it.
// Does nothing while he is attacking, opening a hiding spot, or has nowhere reachable to go.
[RequireComponent(typeof(NavMeshAgent))]
public class KillerUnstick : MonoBehaviour
{
    [Tooltip("Seconds of 'trying to move but not moving' before it steps in.")]
    public float stuckSeconds = 1.2f;
    [Tooltip("He counts as moving if he gets further than this (metres) within the time above.")]
    public float minProgress = 0.15f;
    [Tooltip("Ignore it when he is this close to where he is going.")]
    public float minRemaining = 0.9f;
    [Tooltip("Seconds standing still right next to the goal before it counts as arrived.")]
    public float nearStallSeconds = 1f;
    public bool logRecoveries = false;
    float nearStall;

    NavMeshAgent agent;
    EnemyWander wander;
    Vector3 mark;
    float markTime;
    float timer;
    int stage;
    float offMeshSince = -1f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        wander = GetComponent<EnemyWander>();
        mark = transform.position;
        markTime = Time.time;
    }

    void OnEnable() { mark = transform.position; markTime = Time.time; stage = 0; offMeshSince = -1f; }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer < 0.2f) return;
        timer = 0f;
        if (agent == null || !agent.enabled) { Reset(); return; }

        if (!agent.isOnNavMesh)
        {
            if (offMeshSince < 0f) offMeshSince = Time.time;
            if (Time.time - offMeshSince > 0.4f) PutBackOnMesh();
            return;
        }
        offMeshSince = -1f;

        // right next to the goal but held back (furniture without a navmesh hole, another agent...): count it as arrived
        if (!agent.isStopped && !agent.pathPending && agent.hasPath && agent.speed > 0.1f
            && agent.remainingDistance > agent.stoppingDistance + 0.05f && agent.remainingDistance <= minRemaining
            && agent.velocity.sqrMagnitude < 0.0025f)
        {
            nearStall += 0.2f;
            if (nearStall >= nearStallSeconds) { nearStall = 0f; agent.ResetPath(); Log("arrived (blocked next to goal)"); Reset(); }
            return;
        }
        nearStall = 0f;

        bool wantsToMove = !agent.isStopped && !agent.pathPending && agent.hasPath
            && agent.pathStatus == NavMeshPathStatus.PathComplete
            && agent.remainingDistance > minRemaining && agent.speed > 0.1f;
        if (!wantsToMove) { Reset(); return; }

        Vector3 p = transform.position;
        if ((p - mark).sqrMagnitude > minProgress * minProgress) { mark = p; markTime = Time.time; stage = 0; return; }
        if (Time.time - markTime < stuckSeconds) return;

        // stuck
        markTime = Time.time;
        if (stage == 0)
        {
            stage = 1;
            Vector3 dest = agent.destination;
            agent.ResetPath();
            agent.SetDestination(dest);
            Log("repath");
        }
        else if (stage == 1)
        {
            stage = 2;
            Vector3 dir = agent.steeringTarget - p; dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(p + dir.normalized * 0.5f, out hit, 0.8f, agent.areaMask)) agent.Warp(hit.position);
            Log("nudge");
        }
        else
        {
            stage = 0;
            agent.ResetPath();
            if (wander != null) wander.CancelNoise(); // forget this sound / goal, wander somewhere else
            Vector3 rnd = p + Random.insideUnitSphere * 6f;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(rnd, out hit, 6f, agent.areaMask)) agent.SetDestination(hit.position);
            Log("new destination");
        }
        mark = transform.position;
    }

    void PutBackOnMesh()
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 3f, agent.areaMask) || NavMesh.SamplePosition(transform.position, out hit, 8f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            Log("back on navmesh");
        }
        offMeshSince = Time.time;
        Reset();
    }

    void Reset() { mark = transform.position; markTime = Time.time; stage = 0; }

    void Log(string what)
    {
        if (logRecoveries) Debug.Log("[KillerUnstick] " + what + " at " + transform.position.ToString("F2"));
    }
}
