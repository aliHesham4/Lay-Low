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
    public float fleeSpeed = 1.8f;      // same as Player 1's walk speed

    [Header("Wandering")]
    public float wanderRadius = 4f;
    public float wanderPause = 1f;

    [Header("Danger")]
    [Tooltip("If the enemy gets this close (or is chasing this player), run and hide.")]
    public float dangerDistance = 3f;

    [Header("Hiding")]
    public float enterDistance = 0.8f;
    public Vector2 hideTimeRange = new Vector2(6f, 12f);
    [Tooltip("Won't come out while the enemy is closer than this.")]
    public float safeDistance = 4f;
    [Tooltip("Random delay before choosing to hide even when not in danger.")]
    public Vector2 idleHideDelayRange = new Vector2(15f, 30f);

    [Header("Animator")]
    public string speedFloatName = "Speed";

    private enum State { Wandering, GoingToHide, Hidden }
    private State state = State.Wandering;

    private NavMeshAgent agent;
    private Hider hider;
    private Animator animator;
    private HidingSpot targetSpot;
    private float pauseTimer;
    private float hideTimer;
    private float idleHideTimer;
    private readonly Dictionary<HidingSpot, float> blockedUntil = new Dictionary<HidingSpot, float>();

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        hider = GetComponent<Hider>();
        animator = GetComponentInChildren<Animator>();

        // Disable agent rotation so our custom script controls body orientation smoothly
        agent.updateRotation = false;

        if (hidingSpots == null || hidingSpots.Length == 0)
            hidingSpots = FindObjectsByType<HidingSpot>(FindObjectsSortMode.None);

        agent.speed = wanderSpeed;
        idleHideTimer = Random.Range(idleHideDelayRange.x, idleHideDelayRange.y);
    }

    void Update()
    {
        if (hider.IsCaught) { SetAnim(0f); return; }

        if (state == State.Hidden) { UpdateHidden(); SetAnim(0f); return; }

        bool inDanger = IsInDanger();

        if (state == State.Wandering)
        {
            idleHideTimer -= Time.deltaTime;

            if (inDanger || idleHideTimer <= 0f)
            {
                if (!StartHiding(inDanger))
                {
                    idleHideTimer = 5f;
                    UpdateWander(inDanger);
                }
            }
            else
            {
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
            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 12f);
        }
    }

    // ---------- Danger ----------
    bool IsInDanger()
    {
        if (enemy == null) return false;
        if (enemy.CurrentTarget == hider) return true;
        return Vector3.Distance(transform.position, enemy.transform.position) < dangerDistance;
    }

    // ---------- Wandering ----------
    void UpdateWander(bool inDanger)
    {
        agent.isStopped = false;
        agent.speed = inDanger ? fleeSpeed : wanderSpeed;

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f)
        {
            pauseTimer -= Time.deltaTime;
            if (inDanger || pauseTimer <= 0f)
            {
                pauseTimer = wanderPause;
                PickWanderPoint(inDanger);
            }
        }
    }

    void PickWanderPoint(bool awayFromEnemy)
    {
        if (!agent.isOnNavMesh) return;

        Vector3 best = transform.position;
        float bestScore = -1f;
        bool found = false;
        int tries = awayFromEnemy ? 6 : 1;

        for (int i = 0; i < tries; i++)
        {
            Vector3 random = transform.position + Random.insideUnitSphere * wanderRadius;
            random.y = transform.position.y;

            if (NavMesh.SamplePosition(random, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            {
                float score = (awayFromEnemy && enemy != null)
                    ? Vector3.Distance(hit.position, enemy.transform.position) : 0f;

                if (!found || score > bestScore)
                {
                    best = hit.position;
                    bestScore = score;
                    found = true;
                }
            }
        }

        if (found) agent.SetDestination(best);
    }

    // ---------- Going to a hiding spot ----------
    bool StartHiding(bool urgent)
    {
        HidingSpot spot = FindBestSpot(urgent);
        if (spot == null) return false;

        targetSpot = spot;
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
        float bestDist = float.MaxValue;

        foreach (HidingSpot s in hidingSpots)
        {
            if (s == null || !s.IsFree) continue;
            if (blockedUntil.TryGetValue(s, out float until) && Time.time < until) continue;

            float d = Vector3.Distance(transform.position, s.transform.position);

            if (urgent && enemy != null)
            {
                float dEnemy = Vector3.Distance(enemy.transform.position, s.transform.position);
                if (dEnemy < d) continue;
            }

            if (d < bestDist) { bestDist = d; best = s; }
        }
        return best;
    }

    void UpdateGoingToHide(bool inDanger)
    {
        agent.speed = inDanger ? fleeSpeed : wanderSpeed;

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

        bool enemyNear = enemy != null &&
            Vector3.Distance(transform.position, enemy.transform.position) < safeDistance;

        if (hideTimer > 0f || enemyNear) return;

        targetSpot.Exit(hider);
        targetSpot = null;
        state = State.Wandering;
        idleHideTimer = Random.Range(idleHideDelayRange.x, idleHideDelayRange.y);

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 1f, NavMesh.AllAreas))
            agent.Warp(hit.position);

        agent.isStopped = false;
        agent.speed = wanderSpeed;
        PickWanderPoint(false);
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