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

    float lastDist = 999f;

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
        if (!threat) return;

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

    static float Flat(Vector3 p, Vector3 q) { p.y = 0f; q.y = 0f; return Vector3.Distance(p, q); }

    static float DistToSegment(Vector3 p, Vector3 a, Vector3 b, out float t)
    {
        Vector3 ab = b - a;
        float len2 = ab.sqrMagnitude;
        t = len2 < 1e-6f ? 0f : Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2);
        return Vector3.Distance(p, a + ab * t);
    }
}
