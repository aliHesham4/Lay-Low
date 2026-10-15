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

    // Called by the enemy the moment it grabs this hider
    public void Freeze()
    {
        if (IsCaught) return;
        IsCaught = true;

        // Turn off the physical collider so the enemy walks right over the body
        Collider myCollider = GetComponent<Collider>();
        if (myCollider != null) myCollider.enabled = false;

        if (health != null)
        {
            health.Die();   // stops movement and plays the Die animation
        }
        else
        {
            if (movement != null) movement.enabled = false;

            if (controller != null) controller.enabled = false;

            if (agent != null && agent.enabled)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                agent.enabled = false;
            }
            if (animator != null)
            {
                animator.speed = 1f;
                animator.SetFloat("Speed", 0f);
            }
        }

        if (RoundManager.Instance != null) RoundManager.Instance.ReportCaught(this);
    }
}