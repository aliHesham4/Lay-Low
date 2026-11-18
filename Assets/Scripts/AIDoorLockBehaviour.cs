using UnityEngine;
using UnityEngine.AI;

// AI player with a door lock: when the killer is chasing her, she slams the doorway
// that lies on his way to her (between him and her) shut.
[RequireComponent(typeof(Hider))]
public class AIDoorLockBehaviour : MonoBehaviour
{
    public EnemyVision enemy;
    public float checkInterval = 0.25f;
    [Tooltip("How close the doorway must be to the killer's route to her (metres).")]
    public float routeTolerance = 0.6f;
    [Tooltip("Do not lock a doorway someone is standing in.")]
    public float keepClear = 0.6f;
    [Tooltip("Also use it when the killer is this close and coming closer, even without a chase.")]
    public float dangerDistance = 6f;

    [Header("Sabotage")]
    [Tooltip("Lock another player in with the killer when he is in the same room as them.")]
    public bool sabotage = true;
    [Tooltip("The killer must be this close to the other player (and see them, no wall between).")]
    public float trapKillerRadius = 5f;
    [Tooltip("Only doorways this close to the trapped player count as their way out.")]
    public float trapDoorRadius = 5f;
    [Tooltip("She only sabotages when the killer is at least this far from her.")]
    public float sabotageSafeDistance = 6f;
    [Range(0f, 1f)] [Tooltip("Chance she takes a sabotage chance when she sees one.")]
    public float sabotageChance = 0.75f;
    [Tooltip("Seconds she waits after deciding against it before considering it again.")]
    public float sabotageRethink = 3f;

    float lastDist = 999f;
    float rethinkUntil;
    Hider[] allHiders;

    Hider hider;
    DoorLockHolder holder;
    float timer;
    NavMeshPath path;

    void Start()
    {
        hider = GetComponent<Hider>();
        if (enemy == null) enemy = FindFirstObjectByType<EnemyVision>();
        path = new NavMeshPath();
    }

    void Update()
    {
        if (hider == null || hider.isHumanControlled || hider.IsCaught) return;
        if (holder == null) { holder = GetComponent<DoorLockHolder>(); if (holder == null) return; }
        if (!holder.HasLock || enemy == null || !enemy.enabled) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = checkInterval;

        // use it when he is chasing her, or when he is close and coming her way (also while she hides)
        float killerDist = Flat(enemy.transform.position, transform.position);
        bool closing = killerDist < lastDist - 0.05f;
        lastDist = killerDist;
        bool threat = enemy.CurrentTarget == hider || (killerDist < dangerDistance && closing);
        if (!threat) { if (sabotage && killerDist >= sabotageSafeDistance) TrySabotage(); return; }

        // the killer's walking route to her
        NavMeshHit a, b;
        if (!NavMesh.SamplePosition(enemy.transform.position, out a, 1.5f, NavMesh.AllAreas)) return;
        if (!NavMesh.SamplePosition(transform.position, out b, 1.5f, NavMesh.AllAreas)) return;
        if (!NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) || path.corners.Length < 2) return;

        LockableDoorway pick = null;
        float bestAlong = float.MaxValue;
        foreach (LockableDoorway d in LockableDoorway.All)
        {
            if (d == null || d.IsLocked) continue;
            Vector3 c = d.Center; c.y = 0f;
            if (Flat(enemy.transform.position, c) < keepClear || Flat(transform.position, c) < keepClear) continue;

            float along = 0f, bestOn = float.MaxValue, alongAt = 0f;
            for (int i = 1; i < path.corners.Length; i++)
            {
                Vector3 p0 = path.corners[i - 1], p1 = path.corners[i]; p0.y = 0f; p1.y = 0f;
                float dist = DistToSegment(c, p0, p1, out float t);
                if (dist < bestOn) { bestOn = dist; alongAt = along + t * Vector3.Distance(p0, p1); }
                along += Vector3.Distance(p0, p1);
            }
            // the doorway closest to her along his route, so it cuts him off
            if (bestOn <= routeTolerance && (along - alongAt) < bestAlong) { bestAlong = along - alongAt; pick = d; }
        }
        if (pick != null) holder.LockDoorway(pick);
    }

    // ---------- sabotage: trap someone in a room with the killer ----------
    void TrySabotage()
    {
        if (Time.time < rethinkUntil) return;
        if (allHiders == null) allHiders = FindObjectsByType<Hider>(FindObjectsSortMode.None);
        Vector3 k = enemy.transform.position;

        // a victim: out in the open, in the same room as the killer (close, nothing solid between them)
        Hider victim = null; float best = trapKillerRadius;
        foreach (Hider h in allHiders)
        {
            if (h == null || h == hider || h.IsCaught || h.IsHidden) continue;
            float d = Flat(k, h.transform.position);
            if (d >= best) continue;
            if (Physics.Linecast(k + Vector3.up * 0.6f, h.transform.position + Vector3.up * 0.4f, enemy.obstacleMask, QueryTriggerInteraction.Ignore)) continue;
            best = d; victim = h;
        }
        if (victim == null) return;

        // their way out: the nearest open doorway on the killer's side (same room), that she herself is not stuck behind
        Vector3 v = victim.transform.position;
        LockableDoorway pick = null; float pickDist = trapDoorRadius;
        foreach (LockableDoorway d in LockableDoorway.All)
        {
            if (d == null || d.IsLocked) continue;
            Vector3 c = d.Center;
            float dv = Flat(v, c);
            if (dv >= pickDist || dv < keepClear || Flat(k, c) < keepClear || Flat(transform.position, c) < keepClear) continue;
            float sv = Side(d, v), sk = Side(d, k), sm = Side(d, transform.position);
            if (Mathf.Abs(sv) < 0.15f || Mathf.Sign(sv) != Mathf.Sign(sk)) continue; // killer and victim must be in the same room
            if (Mathf.Sign(sm) == Mathf.Sign(sv) && Flat(transform.position, c) < trapDoorRadius) continue; // don't lock herself in too
            // it must really be their door: nothing solid between them and the doorway
            if (Physics.Linecast(v + Vector3.up * 0.4f, c, enemy.obstacleMask, QueryTriggerInteraction.Ignore)) continue;
            pickDist = dv; pick = d;
        }
        if (pick == null) return;

        if (Random.value > sabotageChance) { rethinkUntil = Time.time + sabotageRethink; return; }
        holder.LockDoorway(pick);
    }

    // which side of the doorway a point is on (signed distance along the doorway's facing)
    static float Side(LockableDoorway d, Vector3 p)
    {
        Vector3 f = d.transform.forward; f.y = 0f; Vector3 o = p - d.Center; o.y = 0f;
        return Vector3.Dot(f.normalized, o);
    }

    static float Flat(Vector3 p, Vector3 q) { p.y = 0f; q.y = 0f; return Vector3.Distance(p, q); }

    static float DistToSegment(Vector3 p, Vector3 a, Vector3 b, out float t)
    {
        Vector3 ab = b - a;
        float len2 = ab.sqrMagnitude;
        t = len2 < 1e-6f ? 0f : Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2);
        return Vector3.Distance(p, a + ab * t);
    }
}
