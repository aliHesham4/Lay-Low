using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyVision : MonoBehaviour
{
    [Header("Targets (Player1 and Player2)")]
    public Hider[] hiders;

    [Header("Vision Settings")]
    public float viewDistance = 10f;
    [Range(0, 360)] public float viewAngle = 90f;
    public LayerMask obstacleMask;
    [Tooltip("Height above a hider's feet that the vision ray aims at, so it doesn't skim the floor.")]
    public float targetHeightOffset = 0.3f;

    [Header("Eye Position")]
    public Transform eyePoint;

    [Header("Movement Settings")]
    public float patrolSpeed = 2f;
    public float chaseSpeed = 5f;

    [Header("Chase Settings")]
    [Tooltip("Seconds without seeing anyone before giving up and going back to patrol.")]
    public float loseSightTime = 3f;
    public float repathInterval = 0.15f;
    [Tooltip("How close the enemy must get to grab a hider.")]
    public float attackRange = 0.7f;
    [Tooltip("How long the stab lasts before the enemy goes back to searching.")]
    public float attackDuration = 2f;

    [Header("Spot Searching Mechanic")]
    [Tooltip("Chance (0-100) to randomly search a hiding spot when walking past it.")]
    public float randomSearchChance = 25f;
    [Tooltip("Chance (0-100) to search a spot if the player hides right in front of them.")]
    public float sureSearchChance = 99f;
    public string openSpotTriggerName = "OpenSpot";
    public float openAnimationDuration = 1.5f;

    [Header("Animator Parameter Names")]
    public string speedFloatName = "Speed";
    public string runBoolName = "IsRunning";
    public string stabTriggerName = "Stab";

    private NavMeshAgent agent;
    private Animator animator;
    private EnemyWander wander;

    private enum EnemyState { Searching, Chasing, Attacking, MovingToSpot, OpeningSpot }
    private EnemyState currentState = EnemyState.Searching;

    private Hider target;
    private Hider attackTarget;
    private float lastSeenTimer;
    private float repathTimer;
    private float attackTimer;
    private Vector3 lastKnownPosition;

    // Spot searching variables
    private HidingSpot[] allHidingSpots;
    private HidingSpot targetSpot;
    private float openingTimer;
    private float randomSpotCheckTimer;
    private Dictionary<HidingSpot, float> checkedSpots = new Dictionary<HidingSpot, float>();

    public Hider CurrentTarget
    {
        get
        {
            if (currentState == EnemyState.Attacking) return attackTarget;
            if (currentState == EnemyState.Chasing) return target;
            return null;
        }
    }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        wander = GetComponent<EnemyWander>();

        allHidingSpots = FindObjectsByType<HidingSpot>(FindObjectsSortMode.None);
        agent.speed = patrolSpeed;
    }

    void Update()
    {
        switch (currentState)
        {
            case EnemyState.Searching: UpdateSearching(); break;
            case EnemyState.Chasing: UpdateChasing(); break;
            case EnemyState.Attacking: UpdateAttacking(); break;
            case EnemyState.MovingToSpot: UpdateMovingToSpot(); break;
            case EnemyState.OpeningSpot: UpdateOpeningSpot(); break;
        }
    }

    // ---------- Searching ----------
    void UpdateSearching()
    {
        UpdateSearchAnimation();

        Hider seen = FindVisibleHider(true);
        if (seen != null)
        {
            StartChase(seen);
            return;
        }

        // Randomly decide to check a nearby spot while patrolling
        randomSpotCheckTimer -= Time.deltaTime;
        if (randomSpotCheckTimer <= 0f)
        {
            randomSpotCheckTimer = 2f; // Check for a nearby spot every 2 seconds
            HidingSpot nearSpot = GetNearestHidingSpot(transform.position);

            if (nearSpot != null && Vector3.Distance(transform.position, nearSpot.transform.position) < 5f)
            {
                bool recentlyChecked = checkedSpots.TryGetValue(nearSpot, out float timeOut) && Time.time < timeOut;
                if (!recentlyChecked && Random.Range(0f, 100f) <= randomSearchChance)
                {
                    StartInvestigatingSpot(nearSpot, false);
                }
            }
        }
    }

    public void BeginChase(Hider h) { if (h != null) StartChase(h); }

    void StartChase(Hider h)
    {
        currentState = EnemyState.Chasing;
        target = h;
        lastKnownPosition = h.transform.position;
        lastSeenTimer = 0f;
        repathTimer = repathInterval;

        if (wander != null) wander.enabled = false;
        agent.isStopped = false;
        agent.speed = chaseSpeed;
    }

    // ---------- Chasing ----------
    void UpdateChasing()
    {
        UpdateChaseAnimation();

        // If the target suddenly became hidden during the chase
        if (target.IsHidden)
        {
            HidingSpot nearestToTarget = GetNearestHidingSpot(target.transform.position);

            // If they hid close to their last known position, we know they went in there
            if (nearestToTarget != null && Vector3.Distance(target.transform.position, nearestToTarget.transform.position) < 3f)
            {
                if (Random.Range(0f, 100f) <= sureSearchChance)
                {
                    StartInvestigatingSpot(nearestToTarget, true);
                    return;
                }
            }
            // If they got away and we failed the percentage check, give up
            ResumeSearching();
            return;
        }

        if (CanSee(target, false))
        {
            lastSeenTimer = 0f;
            lastKnownPosition = target.transform.position;
        }
        else
        {
            Hider other = FindVisibleHider(false);
            if (other != null)
            {
                target = other;
                lastSeenTimer = 0f;
                lastKnownPosition = other.transform.position;
            }
            else
            {
                lastSeenTimer += Time.deltaTime;
            }
        }

        // Grab anyone who is in reach and not hidden
        foreach (Hider h in hiders)
        {
            if (h == null || h.IsHidden || h.IsCaught) continue;
            if (FlatDistance(transform.position, h.transform.position) <= attackRange)
            {
                StartAttack(h);
                return;
            }
        }

        repathTimer += Time.deltaTime;
        if (repathTimer >= repathInterval)
        {
            repathTimer = 0f;
            MoveTo(lastKnownPosition);
        }

        if (lastSeenTimer > loseSightTime) ResumeSearching();
    }

    void MoveTo(Vector3 destination)
    {
        if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
    }

    void ResumeSearching()
    {
        currentState = EnemyState.Searching;
        target = null;
        attackTarget = null;
        targetSpot = null;
        agent.speed = patrolSpeed;
        agent.isStopped = false;
        if (wander != null) wander.enabled = true;
    }

    // ---------- Spot Investigating ----------
    void StartInvestigatingSpot(HidingSpot spot, bool running)
    {
        currentState = EnemyState.MovingToSpot;
        targetSpot = spot;

        if (wander != null) wander.enabled = false;

        agent.speed = running ? chaseSpeed : patrolSpeed;
        agent.isStopped = false;
        agent.SetDestination(spot.GetApproachPoint());
    }

    void UpdateMovingToSpot()
    {
        UpdateSearchAnimation(); // Uses walk/run based on current speed

        // If he sees someone else running around while walking to the spot, chase them instead!
        Hider seen = FindVisibleHider(true);
        if (seen != null)
        {
            StartChase(seen);
            return;
        }

        // Have we reached the hiding spot?
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f)
        {
            currentState = EnemyState.OpeningSpot;
            openingTimer = 0f;
            agent.isStopped = true;
            agent.velocity = Vector3.zero;

            if (animator != null)
            {
                animator.SetFloat(speedFloatName, 0f);
                if (!string.IsNullOrEmpty(runBoolName)) animator.SetBool(runBoolName, false);
                if (!string.IsNullOrEmpty(openSpotTriggerName)) animator.SetTrigger(openSpotTriggerName);
            }
        }
    }

    void UpdateOpeningSpot()
    {
        // Smoothly rotate to face the hiding spot
        Vector3 lookDir = targetSpot.transform.position - transform.position;
        lookDir.y = 0;
        if (lookDir != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 10f);
        }

        openingTimer += Time.deltaTime;

        // When the animation finishes
        if (openingTimer >= openAnimationDuration)
        {
            if (targetSpot.Occupant != null)
            {
                // Gotcha! Rip them out of the spot and stab them.
                Hider victim = targetSpot.Occupant;
                targetSpot.Exit(victim); // Forces the player model to unhide
                StartAttack(victim);
            }
            else
            {
                // It was empty. Don't check this spot again for 45 seconds.
                checkedSpots[targetSpot] = Time.time + 45f;
                ResumeSearching();
            }
        }
    }

    HidingSpot GetNearestHidingSpot(Vector3 pos)
    {
        HidingSpot nearest = null;
        float minDist = float.MaxValue;
        foreach (HidingSpot spot in allHidingSpots)
        {
            if (spot == null) continue;
            float dist = Vector3.Distance(pos, spot.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = spot;
            }
        }
        return nearest;
    }

    // ---------- Attacking ----------
    void StartAttack(Hider h)
    {
        currentState = EnemyState.Attacking;
        attackTarget = h;
        attackTimer = 0f;
        targetSpot = null;

        agent.isStopped = true;
        agent.velocity = Vector3.zero;

        if (animator != null)
        {
            if (!string.IsNullOrEmpty(runBoolName)) animator.SetBool(runBoolName, false);
            if (!string.IsNullOrEmpty(speedFloatName)) animator.SetFloat(speedFloatName, 0f);
            if (!string.IsNullOrEmpty(stabTriggerName)) animator.SetTrigger(stabTriggerName);
        }

        h.Freeze();
    }

    void UpdateAttacking()
    {
        if (attackTarget != null)
        {
            Vector3 lookDirection = attackTarget.transform.position - transform.position;
            lookDirection.y = 0;
            if (lookDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDirection), Time.deltaTime * 10f);
            }
        }

        attackTimer += Time.deltaTime;
        if (attackTimer >= attackDuration) ResumeSearching();
    }

    // ---------- Vision & Helpers ----------
    Hider FindVisibleHider(bool checkAngle)
    {
        Hider best = null;
        float bestDist = float.MaxValue;

        foreach (Hider h in hiders)
        {
            if (!CanSee(h, checkAngle)) continue;
            float d = Vector3.Distance(transform.position, h.transform.position);
            if (d < bestDist) { bestDist = d; best = h; }
        }
        return best;
    }

    bool CanSee(Hider h, bool checkAngle)
    {
        if (h == null || h.IsHidden || h.IsCaught) return false;

        Vector3 origin = eyePoint != null ? eyePoint.position : transform.position;
        Vector3 targetPos = h.transform.position + Vector3.up * targetHeightOffset;
        Vector3 dir = targetPos - origin;
        float distance = dir.magnitude;

        if (distance > viewDistance) return false;
        if (checkAngle && Vector3.Angle(transform.forward, dir) > viewAngle / 2f) return false;

        if (Physics.Raycast(origin, dir.normalized, out RaycastHit hit, distance, obstacleMask))
        {
            if (!hit.transform.IsChildOf(h.transform)) return false;
        }

        return true;
    }

    static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // ---------- Animation ----------
    void UpdateSearchAnimation()
    {
        if (animator == null) return;
        float currentSpeed = agent.velocity.magnitude;
        if (!string.IsNullOrEmpty(speedFloatName)) animator.SetFloat(speedFloatName, currentSpeed);
        if (!string.IsNullOrEmpty(runBoolName)) animator.SetBool(runBoolName, false);
    }

    void UpdateChaseAnimation()
    {
        if (animator == null) return;
        if (!string.IsNullOrEmpty(runBoolName)) animator.SetBool(runBoolName, true);
        if (!string.IsNullOrEmpty(speedFloatName))
        {
            float effectiveSpeed = Mathf.Max(agent.velocity.magnitude, chaseSpeed * 0.5f);
            animator.SetFloat(speedFloatName, effectiveSpeed);
        }
    }
}