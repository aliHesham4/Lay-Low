using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Put this on every character that can hide and be caught (Player1 and Player2).
public class Hider : MonoBehaviour
{
    public string displayName = "Player 1";
    [Tooltip("Tick on the keyboard-controlled player. Leave unticked on the AI player.")]
    public bool isHumanControlled = false;

    public bool IsHidden { get; private set; }
    public bool IsCaught { get; private set; }
    // who stabbed this player (the golden knife round); null when the killer got him
    public Hider KilledBy { get; private set; }

    private readonly List<Renderer> hiddenRenderers = new List<Renderer>();
    private PlayerMovement movement;
    private CharacterController controller;
    private NavMeshAgent agent;
    private PlayerHealth health;
    private Animator animator;

    private int originalLayer;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        controller = GetComponent<CharacterController>();
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<PlayerHealth>();
        animator = GetComponentInChildren<Animator>();

        originalLayer = gameObject.layer;
    }

    public void SetHidden(bool hidden, bool hideRenderers = true)
    {
        if (IsHidden == hidden) return;
        IsHidden = hidden;

        if (hidden)
        {
            // 1. FIX RUNNING IN WATER: Freeze the animation state completely
            if (animator != null)
            {
                animator.SetFloat("Speed", 0f);
                animator.speed = 0f; // Pauses motion on the current pose
            }

            // 2. FIX GETTING KILLED: Switch layer so Enemy vision raycasts ignore the player
            gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");

            if (hideRenderers)
            {
                foreach (Renderer r in GetComponentsInChildren<Renderer>())
                {
                    if (r.enabled) { r.enabled = false; hiddenRenderers.Add(r); }
                }
            }
        }
        else
        {
            // 1. Unfreeze animation playback on exit
            if (animator != null)
            {
                animator.speed = 1f;
            }

            // 2. Restore normal player layer
            gameObject.layer = originalLayer;

            foreach (Renderer r in hiddenRenderers)
                if (r != null) r.enabled = true;
            hiddenRenderers.Clear();
        }

        if (isHumanControlled)
        {
            if (movement != null) movement.enabled = !hidden;
            if (controller != null) controller.enabled = !hidden;
        }
        else if (agent != null)
        {
            agent.enabled = !hidden;
        }
    }

    // Called by the player holding the golden knife
    public void FreezeBy(Hider killer)
    {
        if (IsCaught) return;
        KilledBy = killer;
        Freeze();
    }

    // Called by the enemy the moment it grabs this hider
    public void Freeze()
    {
        if (IsCaught) return;
        IsCaught = true;
        KnifeHolder holder = GetComponent<KnifeHolder>();
        if (holder != null) holder.OnDeath(); // the golden knife drops where he fell

        if (health != null)
        {
            health.Die(); // stops movement and plays the Die animation
        }

        // Always disable movement, CharacterController, and NavMeshAgent (even when health != null)
        // so living players cannot push the body and the NavMeshAgent stops holding it above the floor.
        if (movement != null) movement.enabled = false;
        if (controller != null) controller.enabled = false;

        if (agent != null)
        {
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
            agent.enabled = false;
        }

        AIHider aiHider = GetComponent<AIHider>();
        if (aiHider != null) aiHider.enabled = false;

        // Turn off all solid colliders on root and children so players pass right through without pushing
        foreach (Collider col in GetComponentsInChildren<Collider>())
        {
            if (!col.isTrigger) col.enabled = false;
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.speed = 1f;
            animator.SetFloat("Speed", 0f);
        }

        SnapBodyToFloor();

        if (RoundManager.Instance != null) RoundManager.Instance.ReportCaught(this);
    }

    void SnapBodyToFloor()
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore);

        float closestDist = float.MaxValue;
        float floorY = transform.position.y;
        bool foundFloor = false;

        foreach (RaycastHit hit in hits)
        {
            if (hit.transform.IsChildOf(transform)) continue;
            if (hit.transform.GetComponentInParent<Hider>() != null) continue;
            if (hit.transform.GetComponentInParent<EnemyVision>() != null) continue;

            if (hit.distance < closestDist)
            {
                closestDist = hit.distance;
                floorY = hit.point.y;
                foundFloor = true;
            }
        }

        if (foundFloor)
        {
            Vector3 pos = transform.position;
            pos.y = floorY;
            transform.position = pos;
        }
    }
}