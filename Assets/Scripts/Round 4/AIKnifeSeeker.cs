using UnityEngine;
using UnityEngine.AI;

// Round 4: an AI player walks to the golden knife once it has dropped (after a short reaction time).
// She still runs from the killer / the knife holder first, and does nothing while she is hiding.
[RequireComponent(typeof(AIHider))]
public class AIKnifeSeeker : MonoBehaviour
{
    public Vector2 reactionTime = new Vector2(2.5f, 6f);
    public float walkSpeed = 2.0f;

    AIHider ai;
    NavMeshAgent agent;
    Hider hider;
    float startAt;
    float repath;
    bool driving;

    void Start()
    {
        ai = GetComponent<AIHider>();
        agent = GetComponent<NavMeshAgent>();
        hider = GetComponent<Hider>();
        startAt = Time.time + Random.Range(reactionTime.x, reactionTime.y);
    }

    void Update()
    {
        if (hider == null || hider.IsCaught || KnifeHolder.Current != null || Time.time < startAt) { Release(); return; }
        GoldenKnifeCollectible knife = FindFirstObjectByType<GoldenKnifeCollectible>();
        if (knife == null || ai.IsHiddenNow || ai.InDanger || !agent.enabled || !agent.isOnNavMesh) { Release(); return; }

        driving = true;
        ai.externalControl = true;
        agent.isStopped = false;
        agent.speed = walkSpeed;
        repath -= Time.deltaTime;
        if (repath <= 0f)
        {
            repath = 0.4f;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(knife.transform.position, out hit, 2f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
        }
    }

    void Release()
    {
        if (!driving) return;
        driving = false;
        if (ai != null) ai.externalControl = false;
    }

    void OnDestroy() { Release(); }
}
