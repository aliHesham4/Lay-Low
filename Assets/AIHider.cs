using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Player 2: an AI hider that wanders the NavMesh, runs from the enemy and uses hiding spots.
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Hider))]
public class AIHider : MonoBehaviour
{
    [Header("References")]
    public EnemyVision enemy;
    [Tooltip("Leave empty to find every HidingSpot in the scene automatically.")]
    public HidingSpot[] hidingSpots;

    [Header("Speeds")]
    public float wanderSpeed = 1.2f;
    public float fleeSpeed = 2.4f;

    [Header("Wandering")]
    public float wanderRadius = 4f;
    public float wanderPause = 1f;

    [Header("Danger")]
    [Tooltip("If the enemy gets this close (or is chasing this player), run and hide.")]
    public float dangerDistance = 4f;

    [Header("Hiding")]
    public float enterDistance = 0.8f;
    public Vector2 hideTimeRange = new Vector2(6f, 12f);
    [Tooltip("Won't come out while the enemy is closer than this.")]
    public float safeDistance = 6f;
    [Tooltip("Random delay before choosing to hide even when not in danger.")]
    public Vector2 idleHideDelayRange = new Vector2(25f, 45f);
    [Tooltip("Hide now and then even when the killer is nowhere near. Off = she only hides when she can't run.")]
    public bool idleHiding = false;

    [Tooltip("If the killer comes closer than this while she hides, she may panic and run.")]
    public float panicDistance = 3.5f;
    [Tooltip("Chance per second to panic while he is approaching inside that distance.")]
    public float panicChancePerSecond = 0.3f;

    [Tooltip("Max extra seconds she stays hidden past her timer while the killer is close.")]
    public float maxExtraHideTime = 6f;

    [Tooltip("Higher = she picks her hiding spot more randomly instead of always the best one.")]
    public float randomSpotVariety = 8f;

    [Header("Animator")]
    public string speedFloatName = "Speed";

    private enum State { Wandering, GoingToHide, Hidden }
    private State state = State.Wandering;

    // Used by AIStoneBehaviour: while true it drives the agent (walking to a stone / throwing)
    [HideInInspector] public bool externalControl;
    // Set by SpeedBoost (pepper): while > 0 it overrides her walking speed
    [HideInInspector] public float speedOverride;
    // Set by AIStoneBehaviour while a stone is on the map and she has none: collecting beats idle hiding
    [HideInInspector] public bool suppressIdleHide;
    public bool IsWandering { get { return state == State.Wandering && !externalControl; } }
    public bool IsHiddenNow { get { return state == State.Hidden; } }
    public bool InDanger { get { return IsInDanger(); } }
    public float ModelYaw { get { return modelYaw; } }

    private NavMeshAgent agent;
    private Hider hider;
    private Animator animator;
    private HidingSpot targetSpot;
    private float modelYaw; // the visible model's own Y rotation (180 = it faces the root's back)
    private float pauseTimer;
    private float hideTimer;
    private float idleHideTimer;
    private float startGraceUntil;
    private HidingSpot lastSpot;
    private float lastEnemyDist = 999f;
    private float noHideUntil; // after bailing out of a spot she just runs for a moment
    private bool wasInDanger;
    private bool escapeOk = true;     // last escape search found a route that keeps clear of him
    private bool killerClosing;
    private float lastKillerDist = 999f;
    private float closingTimer;
    private float hideRouteTimer;
    private Hider[] allHiders;

    float NearestOtherPlayer(Vector3 p)
    {
        if (allHiders == null) allHiders = FindObjectsByType<Hider>(FindObjectsSortMode.None);
        float best = 99f;
        foreach (Hider h in allHiders)
        {
            if (h == null || h == hider || h.IsCaught) continue;
            Vector3 a = h.transform.position - p; a.y = 0f;
            best = Mathf.Min(best, a.magnitude);
        }
        return best;
    }
    private float fleeCheckTimer;
    private readonly Dictionary<HidingSpot, float> blockedUntil = new Dictionary<HidingSpot, float>();

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        hider = GetComponent<Hider>();
        animator = GetComponentInChildren<Animator>();
        foreach (Animator a in GetComponentsInChildren<Animator>())
            if (a.transform != transform) { modelYaw = a.transform.localEulerAngles.y; break; }

        // Disable agent rotation so our custom script controls body orientation smoothly
        agent.updateRotation = false;

        if (hidingSpots == null || hidingSpots.Length == 0)
            hidingSpots = FindObjectsByType<HidingSpot>(FindObjectsSortMode.None);

        agent.speed = wanderSpeed;
        idleHideTimer = Random.Range(idleHideDelayRange.x, idleHideDelayRange.y);
        startGraceUntil = Time.time + 1f; // she reacts to a nearby killer almost right away (by keeping her distance, not panicking)
    }

    void Update()
    {
        if (hider.IsCaught) { SetAnim(0f); return; }

        if (state == State.Hidden) { UpdateHidden(); SetAnim(0f); return; }

        if (externalControl)
        {
            HandleRotation();
            SetAnim(agent.velocity.magnitude);
            return;
        }

        // is he coming her way? (checked a few times a second, not every frame)
        closingTimer -= Time.deltaTime;
        if (enemy != null && closingTimer <= 0f)
        {
            closingTimer = 0.25f;
            float kd0 = Vector3.Distance(transform.position, enemy.transform.position);
            killerClosing = kd0 < lastKillerDist - 0.15f;
            lastKillerDist = kd0;
        }

        bool inDanger = IsInDanger();
        if (!inDanger) escapeOk = true;

        if (state == State.Wandering)
        {
            idleHideTimer -= Time.deltaTime;

            if (inDanger)
            {
                // RUN first. She only hides when she can't run: no escape route that keeps clear of him,
                // or he is chasing her and right on top of her.
                float kd = enemy != null ? Vector3.Distance(transform.position, enemy.transform.position) : 99f;
                // hiding while he is watching her just shows him the spot (he searches it almost surely),
                // so then she keeps running even without a perfect route
                bool trapped = !escapeOk && !(BeingChased() && KillerSeesMe());
                if (trapped && Time.time >= noHideUntil && StartHiding(true)) { }
                else UpdateWander(true);
            }
            else if (idleHiding && idleHideTimer <= 0f && !suppressIdleHide)
            {
                if (!StartHiding(false))
                {
                    idleHideTimer = 5f;
                    UpdateWander(false);
                }
            }
            else
            {
                // safe: spread out, collect items and sabotage (the other AI scripts take over from here)
                UpdateWander(false);
            }
        }
        else if (state == State.GoingToHide)
        {
            UpdateGoingToHide(inDanger);
        }

        if (state == State.Hidden) { SetAnim(0f); return; }

        // Handle smooth body rotation toward movement direction (prevents backward walking)
        HandleRotation();

        // Calculate current moving speed for animations
        float currentSpeed = agent.velocity.magnitude;
        SetAnim(currentSpeed);
    }

    void LateUpdate()
    {
        if (speedOverride > 0f && agent != null && agent.enabled && agent.isOnNavMesh) agent.speed = speedOverride;
    }

    void HandleRotation()
    {
        Vector3 direction = agent.velocity;
        direction.y = 0f;

        // If velocity is too low, use the next steering target on the path to keep smooth rotation
        if (direction.sqrMagnitude < 0.01f && agent.hasPath && !agent.pathPending)
        {
            direction = agent.steeringTarget - transform.position;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude > 0.01f)
        {
            // Turn the root so the MODEL (not the root) faces the way we walk
            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized) * Quaternion.Euler(0f, -modelYaw, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 12f);
        }
    }

    // ---------- Danger ----------
    bool IsInDanger()
    {
        if (enemy == null || !enemy.enabled) return false; // killer still inside the elevator
        if (enemy.CurrentTarget == hider) return true;
        if (Time.time < startGraceUntil) return false;
        float d = Vector3.Distance(transform.position, enemy.transform.position);
        if (d >= dangerDistance) return false;
        // he only sees about 2 m and not through walls: behind a wall he is only a threat when right next to her
        // he only sees about 2 m and not through walls: he counts when he can see her, is right next to her,
        // or is coming her way
        return d < dangerDistance * 0.5f || KillerSeesMe() || killerClosing;
    }

    bool BeingChased() { return enemy != null && enemy.enabled && enemy.CurrentTarget == hider; }

    // true when nothing solid stands between the killer and her
    bool KillerSeesMe()
    {
        if (enemy == null) return false;
        Vector3 from = enemy.transform.position + Vector3.up * 0.6f, to = transform.position + Vector3.up * 0.4f;
        return !Physics.Linecast(from, to, enemy.obstacleMask, QueryTriggerInteraction.Ignore);
    }

    // ---------- Wandering ----------
    void UpdateWander(bool inDanger)
    {
        agent.isStopped = false;
        agent.speed = inDanger ? fleeSpeed : wanderSpeed;
        // the moment danger starts, drop the current stroll and pick a proper escape route
        if (inDanger && !wasInDanger) PickWanderPoint(true);
        wasInDanger = inDanger;

        // she sticks to her route and only changes it if it now runs past him (calm walks too:
        // never stroll past the killer or his elevator)
        if (enemy != null && agent.hasPath && !agent.pathPending)
        {
            fleeCheckTimer -= Time.deltaTime;
            if (fleeCheckTimer <= 0f)
            {
                fleeCheckTimer = 1f;
                if (inDanger) { if (RouteTowardKiller(agent.path)) PickWanderPoint(true); }
                else if (PathPasses(agent.path, enemy.transform.position, 3.5f)) PickWanderPoint(false);
            }
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f)
        {
            pauseTimer -= Time.deltaTime;
            if (inDanger || pauseTimer <= 0f)
            {
                pauseTimer = Random.Range(wanderPause, wanderPause * 3f); // stop and look around for a moment
                PickWanderPoint(inDanger);
            }
        }
    }

    void PickWanderPoint(bool awayFromEnemy)
    {
        if (!agent.isOnNavMesh) return;
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit start, 1f, NavMesh.AllAreas)) return;

        // real trips across the house: calm walks of 3-9 m, escapes of 4-10 m (by walking distance)
        bool crowded = !awayFromEnemy && NearestOtherPlayer(transform.position) < 5f;
        float minDist = awayFromEnemy ? 4f : 3f;
        float radius = wanderRadius * (awayFromEnemy || crowded ? 2f : 1.8f);
        int tries = awayFromEnemy ? 14 : 8;

        Vector3 best = transform.position;
        float bestScore = float.MinValue;
        bool found = false;
        float myKillerDist = enemy != null ? Vector3.Distance(transform.position, enemy.transform.position) : 99f;
        if (tryPath == null) tryPath = new NavMeshPath();

        for (int i = 0; i < tries; i++)
        {
            Vector2 r = Random.insideUnitCircle.normalized * Random.Range(minDist, radius);
            Vector3 random = transform.position + new Vector3(r.x, 0f, r.y);
            if (!NavMesh.SamplePosition(random, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) continue;
            if (!NavMesh.CalculatePath(start.position, hit.position, NavMesh.AllAreas, tryPath) || tryPath.status != NavMeshPathStatus.PathComplete) continue;
            float len = PathLength(tryPath);
            if (len < minDist * 0.8f) continue;

            float score;
            if (awayFromEnemy && enemy != null)
            {
                // as far from him as possible, and never along a route that runs past him
                // as far from him as possible, and never along a route that brings her closer to him
                score = Vector3.Distance(hit.position, enemy.transform.position);
                if (RouteTowardKiller(tryPath)) score -= 20f;
            }
            else
            {
                // away from the other players, a decent walk, and not into the killer's area
                score = Mathf.Min(8f, NearestOtherPlayer(hit.position)) + Mathf.Min(len, 8f) * 0.3f + Random.Range(0f, 2f);
                // stay away from the killer, including his elevator at the start of the round
                if (enemy != null && Vector3.Distance(hit.position, enemy.transform.position) < dangerDistance + 2f) score -= 6f;
                // ...and don't walk past him on the way there
                if (enemy != null && PathMinDist(tryPath, enemy.transform.position) < 3.5f) score -= 10f;
            }

            if (score > bestScore) { best = hit.position; bestScore = score; found = true; }
        }

        if (found) agent.SetDestination(best);
        // fleeing: she can only really escape if the route keeps clear of him and ends further from him than she is
        if (awayFromEnemy) escapeOk = found && bestScore > myKillerDist + 0.5f;
    }

    // a route is bad if it takes her closer to him than she is now (with some slack), or right past him
    bool RouteTowardKiller(NavMeshPath p)
    {
        if (enemy == null) return false;
        float now = Vector3.Distance(transform.position, enemy.transform.position);
        float closest = PathMinDist(p, enemy.transform.position);
        return closest < Mathf.Min(2.5f, now) - 0.3f || closest < now - 1.5f;
    }

    NavMeshPath tryPath;

    static float PathLength(NavMeshPath p)
    {
        float len = 0f;
        for (int i = 1; i < p.corners.Length; i++) len += Vector3.Distance(p.corners[i - 1], p.corners[i]);
        return len;
    }

    // does this route come within 'clearance' metres of the point (the killer)?
    bool PathPasses(NavMeshPath p, Vector3 point, float clearance)
    {
        return PathMinDist(p, point) < clearance;
    }

    // closest the route (from where she stands) gets to a point
    float PathMinDist(NavMeshPath p, Vector3 point)
    {
        float best = 999f;
        Vector3[] c = p.corners;
        if (c == null || c.Length == 0) return best;
        Vector3 k = point; k.y = 0f;
        Vector3 prev = transform.position; prev.y = 0f;
        for (int i = 0; i < c.Length; i++)
        {
            Vector3 q = c[i]; q.y = 0f;
            Vector3 ab = q - prev;
            float t = ab.sqrMagnitude < 1e-4f ? 0f : Mathf.Clamp01(Vector3.Dot(k - prev, ab) / ab.sqrMagnitude);
            best = Mathf.Min(best, Vector3.Distance(k, prev + ab * t));
            prev = q;
        }
        return best;
    }

    // ---------- Going to a hiding spot ----------
    bool StartHiding(bool urgent)
    {
        HidingSpot spot = FindBestSpot(urgent);
        if (spot == null) return false;

        targetSpot = spot;
        lastSpot = spot;
        state = State.GoingToHide;
        agent.isStopped = false;
        agent.speed = urgent ? fleeSpeed : wanderSpeed;

        Vector3 p = spot.GetApproachPoint();
        if (NavMesh.SamplePosition(p, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) p = hit.position;
        agent.SetDestination(p);
        return true;
    }

    HidingSpot FindBestSpot(bool urgent)
    {
        HidingSpot best = null;
        float bestScore = float.MaxValue;

        foreach (HidingSpot s in hidingSpots)
        {
            if (s == null || !s.IsFree) continue;
            if (blockedUntil.TryGetValue(s, out float until) && Time.time < until) continue;

            float d = Vector3.Distance(transform.position, s.transform.position);
            float score = d;

            if (enemy != null)
            {
                float dEnemy = Vector3.Distance(enemy.transform.position, s.transform.position);
                // never run toward the killer: skip spots he is nearer to (or right beside)
                if (dEnemy < 3f) continue;
                if (urgent && dEnemy < d) continue;
                // prefer spots that are close to me and far from him
                score = d - 0.5f * dEnemy;

                // and never a spot whose route runs past him
                if (tryPath == null) tryPath = new NavMeshPath();
                Vector3 ap = s.GetApproachPoint();
                if (NavMesh.SamplePosition(ap, out NavMeshHit aph, 1.5f, NavMesh.AllAreas) &&
                    NavMesh.CalculatePath(transform.position, aph.position, NavMesh.AllAreas, tryPath) &&
                    PathMinDist(tryPath, enemy.transform.position) < 2.5f) continue;
            }

            // don't crowd into a spot right next to another player
            if (NearestOtherPlayer(s.transform.position) < 4f) score += 5f;

            // random noise + a penalty for the spot she used last, so she doesn't always pick the same one
            score += Random.Range(0f, randomSpotVariety);
            if (s == lastSpot) score += 6f;

            if (score < bestScore) { bestScore = score; best = s; }
        }
        return best;
    }

    void UpdateGoingToHide(bool inDanger)
    {
        agent.speed = inDanger ? fleeSpeed : wanderSpeed;

        // he has moved onto her route to the spot: give it up and run instead
        hideRouteTimer -= Time.deltaTime;
        if (enemy != null && targetSpot != null && hideRouteTimer <= 0f && agent.hasPath && !agent.pathPending)
        {
            hideRouteTimer = 0.5f;
            if (agent.remainingDistance > enterDistance + 0.5f && PathPasses(agent.path, enemy.transform.position, 2f))
            {
                blockedUntil[targetSpot] = Time.time + 5f;
                targetSpot = null;
                state = State.Wandering;
                noHideUntil = Time.time + 1f;
                wasInDanger = true;
                PickWanderPoint(true);
                return;
            }
        }

        if (targetSpot == null || !targetSpot.IsFree)
        {
            state = State.Wandering;
            targetSpot = null;
            if (!StartHiding(inDanger)) idleHideTimer = 5f;
            return;
        }

        Vector3 a = transform.position;
        Vector3 b = targetSpot.GetApproachPoint();
        a.y = 0f; b.y = 0f;

        if (targetSpot.IsReachedBy(transform.position) || Vector3.Distance(a, b) <= enterDistance)
        {
            if (targetSpot.TryEnter(hider))
            {
                state = State.Hidden;
                hideTimer = Random.Range(hideTimeRange.x, hideTimeRange.y);
            }
            else
            {
                state = State.Wandering;
                targetSpot = null;
            }
            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f)
        {
            blockedUntil[targetSpot] = Time.time + 20f;
            state = State.Wandering;
            targetSpot = null;
            idleHideTimer = 5f;
        }
    }

    // ---------- Hidden ----------
    void UpdateHidden()
    {
        hideTimer -= Time.deltaTime;

        // She cannot know if he will open her spot (that is a dice roll on his side).
        // She only sees him getting closer, and she may lose her nerve and bolt.
        if (enemy != null && targetSpot != null)
        {
            float d = Vector3.Distance(transform.position, enemy.transform.position);
            bool approaching = d < lastEnemyDist - 0.01f;
            lastEnemyDist = d;
            if (d < panicDistance && approaching && Random.value < panicChancePerSecond * Time.deltaTime)
            {
                blockedUntil[targetSpot] = Time.time + 20f;
                ExitHidden();
                noHideUntil = Time.time + 3f;
                agent.speed = fleeSpeed;
                PickWanderPoint(true);
                return;
            }
        }

        bool enemyNear = enemy != null &&
            Vector3.Distance(transform.position, enemy.transform.position) < safeDistance;

        // once he is well away she doesn't sit around: back out within ~2 s to go for items
        if (!enemyNear && !IsInDanger()) hideTimer = Mathf.Min(hideTimer, 2f);

        // Never stay forever: after the timer plus 20 extra seconds she comes out even if he is near
        if (hideTimer > 0f || (enemyNear && hideTimer > -maxExtraHideTime)) return;

        ExitHidden();
        PickWanderPoint(false);
    }

    // After making noise: get away from this spot, preferably into a hiding spot
    public void RunAndHide()
    {
        if (state != State.Wandering) return;
        agent.isStopped = false;
        if (!StartHiding(true))
        {
            agent.speed = fleeSpeed;
            noHideUntil = Time.time + 3f;
            PickWanderPoint(true);
        }
    }

    // Used by AIStoneBehaviour (she steps out to throw)
    public void LeaveHidingSpot()
    {
        if (state != State.Hidden) return;
        ExitHidden();
    }

    void ExitHidden()
    {
        if (targetSpot != null) targetSpot.Exit(hider);
        targetSpot = null;
        state = State.Wandering;
        idleHideTimer = Random.Range(idleHideDelayRange.x, idleHideDelayRange.y);

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 1f, NavMesh.AllAreas))
            agent.Warp(hit.position);

        agent.isStopped = false;
        agent.speed = wanderSpeed;
    }

    void SetAnim(float speed)
    {
        if (animator != null && !string.IsNullOrEmpty(speedFloatName))
        {
            // Passes 1.0f when moving, 0f when stopped
            float animValue = speed > 0.1f ? 1f : 0f;
            animator.SetFloat(speedFloatName, animValue);
        }
    }
}