using UnityEngine;
using UnityEngine.AI;

// Player 2's use of the party whistle: she walks up to the human player, blows the horn so the killer
// comes to that spot, and then runs away / hides.
[RequireComponent(typeof(AIHider))]
public class AIWhistleBehaviour : MonoBehaviour
{
    [Header("References")]
    public EnemyVision enemy;
    [Tooltip("The human player(s) she may set up.")]
    public Hider[] otherPlayers;

    [Header("Timing")]
    [Tooltip("Seconds she waits after picking the whistle up before she acts.")]
    public Vector2 thinkTime = new Vector2(5f, 12f);
    [Tooltip("She blows when she is this close to the human player.")]
    public float blowDistance = 4f;
    [Tooltip("She will not start while the killer is closer than this to her.")]
    public float minKillerDistance = 7f;
    public float giveUpAfter = 30f;

    enum Mode { Idle, Approaching, Blowing }
    Mode mode = Mode.Idle;

    AIHider ai;
    Hider hider;
    NavMeshAgent agent;
    WhistleHolder holder;
    AIStoneBehaviour stones;
    float thinkTimer;
    float giveUpTimer;
    float repathTimer;
    bool hadWhistle;
    Hider victim;

    void Start()
    {
        ai = GetComponent<AIHider>();
        hider = GetComponent<Hider>();
        agent = GetComponent<NavMeshAgent>();
        stones = GetComponent<AIStoneBehaviour>();
        if (enemy == null) enemy = ai.enemy;
    }

    void Update()
    {
        if (holder == null) holder = GetComponent<WhistleHolder>();
        if (hider.IsCaught) { Stop(); return; }

        bool has = holder != null && (holder.HasWhistle || holder.IsBlowing);
        if (has && !hadWhistle) thinkTimer = Random.Range(thinkTime.x, thinkTime.y);
        hadWhistle = has;
        if (!has) { if (mode != Mode.Idle) Stop(); return; }

        if (mode == Mode.Idle) UpdateIdle();
        else if (mode == Mode.Approaching) UpdateApproaching();
        else UpdateBlowing();
    }

    float KillerDist()
    {
        if (enemy == null || !enemy.enabled) return 999f;
        return Vector3.Distance(transform.position, enemy.transform.position);
    }

    void UpdateIdle()
    {
        thinkTimer -= Time.deltaTime;
        if (thinkTimer > 0f) return;
        if (stones != null && stones.Busy) return;
        if (!ai.IsWandering || ai.InDanger) return;
        if (KillerDist() < minKillerDistance) return;

        victim = NearestHuman();
        if (victim == null) { thinkTimer = 3f; return; }

        mode = Mode.Approaching;
        giveUpTimer = giveUpAfter;
        repathTimer = 0f;
        ai.externalControl = true;
        agent.isStopped = false;
        agent.speed = ai.wanderSpeed;
    }

    void UpdateApproaching()
    {
        giveUpTimer -= Time.deltaTime;
        if (victim == null || victim.IsCaught || ai.InDanger || giveUpTimer <= 0f ||
            (enemy != null && enemy.CurrentTarget == hider))
        {
            Stop(); thinkTimer = 4f; return;
        }

        Vector3 a = transform.position, b = victim.transform.position;
        a.y = 0f; b.y = 0f;
        if (Vector3.Distance(a, b) <= blowDistance && KillerDist() >= minKillerDistance * 0.6f)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            holder.Blow();
            mode = Mode.Blowing;
            return;
        }

        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            repathTimer = 0.5f;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(victim.transform.position, out hit, 2f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
        }
    }

    void UpdateBlowing()
    {
        if (holder.IsBlowing) return;
        // Done: the killer is on his way to that spot, so she gets away from it
        mode = Mode.Idle;
        ai.externalControl = false;
        ai.RunAndHide();
    }

    Hider NearestHuman()
    {
        Hider best = null; float bestDist = float.MaxValue;
        if (otherPlayers == null) return null;
        foreach (Hider h in otherPlayers)
        {
            if (h == null || h == hider || h.IsCaught) continue;
            float d = Vector3.Distance(transform.position, h.transform.position);
            if (d < bestDist) { bestDist = d; best = h; }
        }
        return best;
    }

    void Stop()
    {
        if (mode != Mode.Idle)
        {
            mode = Mode.Idle;
            if (ai != null && !(holder != null && holder.IsBlowing)) ai.externalControl = false;
            if (agent != null && agent.enabled && agent.isOnNavMesh && !(holder != null && holder.IsBlowing)) agent.isStopped = false;
        }
        victim = null;
    }
}
