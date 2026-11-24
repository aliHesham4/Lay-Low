using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Round 4: an AI player that holds the golden knife. She hunts the others:
//  - chases anyone she can see and stabs them when she gets close,
//  - otherwise walks from hiding spot to hiding spot, opens each one and stabs whoever is hiding inside.
// She still runs from the killer first (and does nothing while she is hiding herself).
[RequireComponent(typeof(AIHider))]
public class AIKnifeHunter : MonoBehaviour
{
    public float chaseSpeed = 2.4f;
    public float walkSpeed = 1.5f;
    public float sightDistance = 9f;
    public float stabDistance = 1.2f;
    public float openSeconds = 1.2f;
    [Tooltip("Seconds before an empty (or just searched) hiding spot is checked again.")]
    public float recheckAfter = 40f;

    AIHider ai;
    Hider hider;
    NavMeshAgent agent;
    KnifeHolder knife;
    EnemyVision killer;
    MonoBehaviour[] paused;

    HidingSpot spot;
    float spotSince, openUntil;
    bool opening;
    float repath;
    readonly Dictionary<HidingSpot, float> checkedUntil = new Dictionary<HidingSpot, float>();

    void Start()
    {
        ai = GetComponent<AIHider>();
        hider = GetComponent<Hider>();
        agent = GetComponent<NavMeshAgent>();
        knife = GetComponent<KnifeHolder>();
        killer = ai.enemy != null ? ai.enemy : FindFirstObjectByType<EnemyVision>();

        // her other jobs (stones, whistle, feather...) would take the agent back from under us
        paused = new MonoBehaviour[]
        {
            GetComponent<AIStoneBehaviour>(), GetComponent<AIWhistleBehaviour>(), GetComponent<AIFeatherBehaviour>(),
            GetComponent<AIDoorLockBehaviour>(), GetComponent<AIIceGunBehaviour>(), GetComponent<AICollectibleSeeker>()
        };
        foreach (MonoBehaviour m in paused) if (m != null) m.enabled = false;
    }

    void OnDestroy()
    {
        if (paused != null) foreach (MonoBehaviour m in paused) if (m != null) m.enabled = true;
        if (ai != null) ai.externalControl = false;
    }

    void Update()
    {
        if (hider.IsCaught || knife == null) return;

        // hiding from the killer, or he is close: let her normal fear behaviour (run / hide) take over
        if (ai.IsHiddenNow || ai.InDanger || !agent.enabled || !agent.isOnNavMesh)
        {
            ai.externalControl = false;
            opening = false;
            if (knife != null) knife.target = null;
            return;
        }
        ai.externalControl = true;
        agent.isStopped = false;

        Hider victim = FindVictim();
        knife.target = victim;
        if (victim != null)
        {
            opening = false;
            spot = null;
            agent.speed = chaseSpeed;
            Vector3 flat = victim.transform.position - transform.position; flat.y = 0f;
            if (flat.magnitude <= stabDistance)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                knife.StabAt(victim);
            }
            else Go(victim.transform.position, 0.15f);
            return;
        }

        SearchSpots();
    }

    // somebody free that she can see
    Hider FindVictim()
    {
        Hider best = null;
        float bestD = sightDistance;
        LayerMask walls = killer != null ? killer.obstacleMask : (LayerMask)(~0);
        foreach (Hider h in FindObjectsByType<Hider>(FindObjectsSortMode.None))
        {
            if (h == null || h == hider || h.IsCaught || h.IsHidden) continue;
            float d = Vector3.Distance(h.transform.position, transform.position);
            if (d >= bestD) continue;
            if (Physics.Linecast(transform.position + Vector3.up * 0.6f, h.transform.position + Vector3.up * 0.4f, walls, QueryTriggerInteraction.Ignore)) continue;
            bestD = d; best = h;
        }
        return best;
    }

    void SearchSpots()
    {
        if (opening)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            if (Time.time < openUntil) return;
            opening = false;
            if (spot != null)
            {
                Hider occ = spot.Occupant;
                if (occ != null && occ != hider && !occ.IsCaught)
                {
                    spot.Exit(occ);
                    knife.Swing();
                    occ.FreezeBy(hider);
                }
                checkedUntil[spot] = Time.time + recheckAfter;
                spot = null;
            }
            return;
        }

        if (spot == null) spot = PickSpot();
        if (spot == null) { WanderOn(); return; }

        agent.speed = walkSpeed;
        Vector3 p = spot.GetApproachPoint();
        Vector3 flat = p - transform.position; flat.y = 0f;
        bool arrived = flat.magnitude <= 1.0f || (!agent.pathPending && agent.hasPath && agent.remainingDistance <= 0.5f);
        if (arrived || Time.time - spotSince > 25f)
        {
            if (arrived)
            {
                opening = true;
                openUntil = Time.time + openSeconds;
                spot.NotifyEnemyOpening(openSeconds);
            }
            else { checkedUntil[spot] = Time.time + 10f; spot = null; }
            return;
        }
        Go(p, 0.4f);
    }

    HidingSpot PickSpot()
    {
        HidingSpot best = null;
        float bestScore = float.MaxValue;
        foreach (HidingSpot s in FindObjectsByType<HidingSpot>(FindObjectsSortMode.None))
        {
            if (s == null) continue;
            float until;
            if (checkedUntil.TryGetValue(s, out until) && Time.time < until) continue;
            if (EnvironmentalHazard.IsDangerous(s.transform.position, ai)) continue;
            float d = Vector3.Distance(s.transform.position, transform.position);
            float score = d + (s.Occupant != null ? 0f : 3f) * Random.value + Random.value * 4f; // a bit random, never the same route
            if (score < bestScore) { bestScore = score; best = s; }
        }
        if (best != null) spotSince = Time.time;
        return best;
    }

    void WanderOn()
    {
        agent.speed = walkSpeed;
        if (agent.hasPath && !agent.pathPending && agent.remainingDistance > 0.6f) return;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position + Random.insideUnitSphere * 8f, out hit, 8f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
    }

    void Go(Vector3 world, float every)
    {
        repath -= Time.deltaTime;
        if (repath > 0f) return;
        repath = every;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(world, out hit, 2f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
    }
}
