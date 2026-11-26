using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// Handles picking up stones when within reach and using them in one of two ways:
//  1. EXPOSE: throw it near another player so the killer goes to that spot.
//  2. DECOY:  when the killer is close (and not chasing her), throw it away from herself
//             so he walks off to investigate the noise.
// (Map-wide seeking is handled centrally by AICollectibleSeeker.)
[RequireComponent(typeof(AIHider))]
public class AIStoneBehaviour : MonoBehaviour
{
    [Header("References")]
    public EnemyVision enemy;
    [Tooltip("The other player(s) she may try to expose.")]
    public Hider[] otherPlayers;
    public GameObject stonePrefab;
    [Tooltip("Optional. Where the stone leaves her hand. Defaults to a point in front of her chest.")]
    public Transform hand;
    [Tooltip("Her on-screen HUD (bag icon). Found automatically if left empty.")]
    public PlayerHUD playerHUD;

    [Header("Pickup")]
    public string stoneTag = "CollectibleStone";
    public float pickupDistance = 0.7f;
    [Tooltip("Seconds after a throw before she looks for another stone.")]
    public float cooldownAfterThrow = 12f;

    [Header("Deciding")]
    [Tooltip("Seconds she 'thinks' after picking a stone up before using it.")]
    public Vector2 thinkTime = new Vector2(6f, 16f);
    [Tooltip("Killer closer than this (and not chasing her) = throw a decoy.")]
    public float killerNearDistance = 8f;
    [Tooltip("Killer must be farther than this before she tries to expose someone.")]
    public float exposeMinKillerDistance = 8f;
    [Range(0f, 1f)] public float exposeChance = 1f;
    [Tooltip("How far the decoy lands beyond the killer.")]
    public float decoyDistance = 9f;

    [Header("Throwing")]
    public float arcHeight = 6f;
    public float maxThrowDistance = 15f;
    [Tooltip("Same floor mask as Player 1's thrower, so aim points land on the floor and not on walls.")]
    public LayerMask floorMask = Physics.DefaultRaycastLayers;
    [Tooltip("How fast the red marker drifts onto the target (metres per second). Lower = slower.")]
    public float markerSpeed = 1.6f;
    [Tooltip("Seconds she 'thinks' with the marker near her before aiming.")]
    public float thinkBeforeAim = 0.9f;
    [Tooltip("Seconds the marker settles on the target before she throws.")]
    public float settleTime = 0.9f;
    public float releaseDelay = 0.4f;
    public string throwTriggerName = "Throw";
    [Tooltip("Red target marker shown while she aims (like Player 1's blue one).")]
    public GameObject targetIndicator;

    [Header("House Bounds")]
    [Tooltip("Parent of the house boundary colliders. Found automatically by name 'SpaceLimit' if empty.")]
    public Transform spaceLimit;
    [Tooltip("Aim points stay this far inside the walls.")]
    public float wallMargin = 0.8f;

    enum Mode { Idle, Throwing }
    Mode mode = Mode.Idle;

    AIHider ai;
    Hider hider;
    NavMeshAgent agent;
    Animator animator;
    Collider ownCollider;
    bool hasStone;
    float thinkTimer;
    float stoneCooldownUntil;

    Bounds houseBounds;
    bool hasHouseBounds;

    public bool HasStone { get { return hasStone; } }
    public bool Busy { get { return mode != Mode.Idle; } }
    public bool OnSeekCooldown { get { return Time.time < stoneCooldownUntil; } }

    void Start()
    {
        ai = GetComponent<AIHider>();
        hider = GetComponent<Hider>();
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        ownCollider = GetComponent<Collider>();

        if (enemy == null) enemy = ai.enemy;
        if (playerHUD == null) playerHUD = GetComponent<PlayerHUD>();
        if (hand == null)
            foreach (Transform t in GetComponentsInChildren<Transform>())
                if (t.name.EndsWith("RightHand")) { hand = t; break; }
        if (targetIndicator != null) targetIndicator.SetActive(false);
    }

    void Update()
    {
        if (hider.IsCaught)
        {
            if (mode != Mode.Idle) Release();
            return;
        }

        if (!hasStone && TryPickUpNearbyStone()) return;

        if (hasStone) UpdateDeciding();
    }

    // ---------------- Pickup ----------------
    // Grabs any stone she steps close to (whether AICollectibleSeeker walked her there or she passed by it)
    bool TryPickUpNearbyStone()
    {
        if (ai.IsHiddenNow || mode == Mode.Throwing) return false;
        GameObject[] stones;
        try { stones = GameObject.FindGameObjectsWithTag(stoneTag); }
        catch (UnityException) { return false; }

        foreach (GameObject s in stones)
        {
            if (s == null || !s.activeInHierarchy) continue;
            Vector3 a = transform.position, b = s.transform.position;
            a.y = 0f; b.y = 0f;
            if (Vector3.Distance(a, b) <= pickupDistance)
            {
                PickUp(s);
                return true;
            }
        }
        return false;
    }

    void PickUp(GameObject stone)
    {
        if (hasStone) return; // one rock at a time
        if (stone != null) Destroy(stone);
        hasStone = true;
        thinkTimer = Random.Range(thinkTime.x, thinkTime.y);
        if (playerHUD != null) playerHUD.ShowStoneInBag();
    }

    // ---------------- Deciding what to do with the stone ----------------
    void UpdateDeciding()
    {
        if (mode == Mode.Throwing) return;

        thinkTimer -= Time.deltaTime;
        if (thinkTimer > 0f) return;
        // she may throw while out, or step out of her hiding spot to throw (not while running to one)
        bool fromHiding = ai.IsHiddenNow;
        if (!ai.IsWandering && !fromHiding) return;
        if (enemy == null) return;

        float killerDist = Vector3.Distance(transform.position, enemy.transform.position);
        bool killerChasingMe = enemy.CurrentTarget == hider;
        if (killerChasingMe) return; // running for her life, no time to throw

        // 1. DECOY: the killer is near, send him to a far place
        if (killerDist < killerNearDistance)
        {
            Vector3 away = enemy.transform.position - transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = transform.forward;
            Vector3 aim = transform.position + away.normalized * Mathf.Min(maxThrowDistance, killerDist + decoyDistance);
            if (fromHiding) { thinkTimer = 1.5f; return; } // he is near: stepping out now would be suicide
            BeginThrow(PickFarSpot(SnapToFloor(aim)));
            return;
        }

        // 2. EXPOSE: killer is away from her, so throw it at the human to throw him off
        if (killerDist > exposeMinKillerDistance && Random.value < exposeChance)
        {
            Hider victim = PickExposeTarget();
            if (victim != null)
            {
                Vector2 jitter = Random.insideUnitCircle * 0.8f;
                Vector3 aim = victim.transform.position + new Vector3(jitter.x, 0f, jitter.y);
                if (fromHiding) ai.LeaveHidingSpot();
                BeginThrow(SnapToFloor(aim));
                return;
            }
        }

        thinkTimer = 1.5f; // not the moment yet, keep it and look again soon
    }

    void BeginThrow(Vector3 target)
    {
        target = ClampToRange(target);
        StartCoroutine(Throw(target));
    }

    Hider PickExposeTarget()
    {
        // the player (human or AI) in throwing range that the killer is closest to, so the noise brings
        // him onto them; never someone right next to her, or he would come for her as well
        Hider best = null;
        float bestScore = float.MaxValue;
        if (otherPlayers == null) return null;
        foreach (Hider h in otherPlayers)
        {
            if (h == null || h == hider || h.IsCaught) continue;
            float d = Vector3.Distance(transform.position, h.transform.position);
            if (d > maxThrowDistance || d < 4f) continue;
            float score = enemy != null ? Vector3.Distance(enemy.transform.position, h.transform.position) : d;
            if (score < bestScore) { bestScore = score; best = h; }
        }
        return best;
    }

    // ---------------- Throwing ----------------
    IEnumerator Throw(Vector3 target)
    {
        mode = Mode.Throwing;
        ai.externalControl = true;
        agent.isStopped = true;
        agent.ResetPath();
        agent.velocity = Vector3.zero;

        Vector3 dir = target - transform.position;
        dir.y = 0f;
        Quaternion face = dir.sqrMagnitude > 0.01f
            ? Quaternion.LookRotation(dir.normalized) * Quaternion.Euler(0f, -ai.ModelYaw, 0f)
            : transform.rotation;

        Vector3 markerStart = SnapToFloor(transform.position);
        Vector3 side = Vector3.Cross(Vector3.up, (target - markerStart).normalized);
        if (targetIndicator != null) { targetIndicator.transform.position = markerStart + Vector3.up * 0.05f; targetIndicator.SetActive(true); }

        // 1. thinking: marker sits near her feet and sways a little
        float t = 0f;
        while (t < thinkBeforeAim)
        {
            t += Time.deltaTime;
            transform.rotation = Quaternion.Slerp(transform.rotation, face, Time.deltaTime * 4f);
            SetMarker(markerStart + side * Mathf.Sin(t * 5f) * 0.25f + (target - markerStart).normalized * 0.4f);
            if (ShouldAbortThrow()) { Release(); yield break; }
            yield return null;
        }

        // 2. drift to the target at a slow, steady speed
        float travel = Mathf.Clamp(Vector3.Distance(markerStart, target) / Mathf.Max(0.1f, markerSpeed), 1.2f, 4f);
        t = 0f;
        while (t < travel)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / travel));
            float wobble = Mathf.Sin(t * 4f) * 0.35f * (1f - k);
            transform.rotation = Quaternion.Slerp(transform.rotation, face, Time.deltaTime * 6f);
            SetMarker(Vector3.Lerp(markerStart, target, k) + side * wobble);
            if (ShouldAbortThrow()) { Release(); yield break; }
            yield return null;
        }

        // 3. last small adjustments, then lock on
        t = 0f;
        while (t < settleTime)
        {
            t += Time.deltaTime;
            float shrink = 1f - Mathf.Clamp01(t / settleTime);
            SetMarker(target + side * Mathf.Sin(t * 9f) * 0.2f * shrink);
            transform.rotation = Quaternion.Slerp(transform.rotation, face, Time.deltaTime * 8f);
            if (ShouldAbortThrow()) { Release(); yield break; }
            yield return null;
        }
        SetMarker(target);
        if (targetIndicator != null) targetIndicator.SetActive(false);

        if (animator != null && HasParameter(throwTriggerName)) animator.SetTrigger(throwTriggerName);
        yield return new WaitForSeconds(releaseDelay);

        if (hider.IsCaught) { Release(); yield break; }

        Vector3 origin = hand != null ? hand.position : transform.position + Vector3.up * 0.6f + transform.forward * 0.2f;
        hasStone = false;
        stoneCooldownUntil = Time.time + cooldownAfterThrow;
        if (playerHUD != null) playerHUD.EmptyBag();

        if (stonePrefab != null)
        {
            GameObject stone = Instantiate(stonePrefab, origin, Quaternion.identity);
            Collider sc = stone.GetComponent<Collider>();
            if (sc != null && ownCollider != null) Physics.IgnoreCollision(sc, ownCollider);
            Rigidbody rb = stone.GetComponent<Rigidbody>();
            if (rb != null) rb.linearVelocity = Trajectory(origin, target, arcHeight);
        }

        Release();
    }

    Vector3 Trajectory(Vector3 origin, Vector3 target, float height)
    {
        float gravity = Physics.gravity.y;
        float displacementY = target.y - origin.y;
        Vector3 displacementXZ = new Vector3(target.x - origin.x, 0, target.z - origin.z);

        float timeUp = Mathf.Sqrt(Mathf.Abs(-2 * height / gravity));
        float timeDown = Mathf.Sqrt(Mathf.Abs(2 * (displacementY - height) / gravity));

        Vector3 velocityY = Vector3.up * Mathf.Sqrt(Mathf.Abs(-2 * gravity * height));
        Vector3 velocityXZ = displacementXZ / (timeUp + timeDown);

        return velocityXZ + velocityY;
    }

    // ---------------- Helpers ----------------
    void SetMarker(Vector3 p)
    {
        if (targetIndicator != null) targetIndicator.transform.position = p + Vector3.up * 0.05f;
    }

    void BuildHouseBounds()
    {
        if (spaceLimit == null) { GameObject g = GameObject.Find("SpaceLimit"); if (g != null) spaceLimit = g.transform; }
        if (spaceLimit == null) return;
        Collider[] cols = spaceLimit.GetComponentsInChildren<Collider>();
        if (cols.Length == 0) return;
        houseBounds = cols[0].bounds;
        foreach (Collider cl in cols) houseBounds.Encapsulate(cl.bounds);
        hasHouseBounds = true;
    }

    bool InsideHouse(Vector3 p)
    {
        if (!hasHouseBounds) BuildHouseBounds();
        if (!hasHouseBounds) return true;
        return p.x > houseBounds.min.x + wallMargin && p.x < houseBounds.max.x - wallMargin &&
               p.z > houseBounds.min.z + wallMargin && p.z < houseBounds.max.z - wallMargin;
    }

    Vector3 ClampInsideHouse(Vector3 p)
    {
        if (!hasHouseBounds) BuildHouseBounds();
        if (!hasHouseBounds) return p;
        p.x = Mathf.Clamp(p.x, houseBounds.min.x + wallMargin, houseBounds.max.x - wallMargin);
        p.z = Mathf.Clamp(p.z, houseBounds.min.z + wallMargin, houseBounds.max.z - wallMargin);
        return p;
    }

    Vector3 SnapToFloor(Vector3 p)
    {
        p = ClampInsideHouse(p);
        NavMeshHit hit;
        if (NavMesh.SamplePosition(p, out hit, 3f, NavMesh.AllAreas)) p = ClampInsideHouse(hit.position);
        RaycastHit rh;
        if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out rh, 6f, floorMask)) return rh.point;
        return p;
    }

    Vector3 PickFarSpot(Vector3 fallback)
    {
        Vector3 best = fallback; float bestScore = -1f; bool found = false;
        for (int i = 0; i < 14; i++)
        {
            Vector2 r = Random.insideUnitCircle.normalized * Random.Range(8f, maxThrowDistance);
            Vector3 p = transform.position + new Vector3(r.x, 0f, r.y);
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(p, out hit, 1.2f, NavMesh.AllAreas)) continue;
            if (!InsideHouse(hit.position)) continue;
            float score = Vector3.Distance(hit.position, transform.position);
            if (enemy != null) score -= Vector3.Distance(hit.position, enemy.transform.position) * 0.3f;
            if (!found || score > bestScore) { found = true; bestScore = score; best = hit.position; }
        }
        return best;
    }

    Vector3 ClampToRange(Vector3 target)
    {
        Vector3 offset = target - transform.position;
        offset.y = 0f;
        if (offset.magnitude > maxThrowDistance)
            target = SnapToFloor(transform.position + offset.normalized * maxThrowDistance);
        return target;
    }

    bool HasParameter(string name)
    {
        if (animator == null) return false;
        foreach (AnimatorControllerParameter p in animator.parameters)
            if (p.name == name) return true;
        return false;
    }

    void Release()
    {
        mode = Mode.Idle;
        if (targetIndicator != null) targetIndicator.SetActive(false);
        if (ai != null)
        {
            if (ai.externalControl && agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = false;
            ai.externalControl = false;
        }
    }

    bool ShouldAbortThrow()
    {
        if (hider.IsCaught || ai.InDanger) return true;
        if (enemy != null && enemy.enabled)
        {
            if (enemy.CurrentTarget == hider) return true;
            if (Vector3.Distance(transform.position, enemy.transform.position) < 5.5f) return true;
        }
        return false;
    }
}