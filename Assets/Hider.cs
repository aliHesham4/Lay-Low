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

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        controller = GetComponent<CharacterController>();
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<PlayerHealth>();
        animator = GetComponentInChildren<Animator>();
    }

    public void SetHidden(bool hidden)
    {
        if (IsHidden == hidden) return;
        IsHidden = hidden;

        if (hidden)
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (r.enabled) { r.enabled = false; hiddenRenderers.Add(r); }
            }
        }
        else
        {
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

        // 1. Turn off the physical collider so the enemy walks right over the body
        Collider myCollider = GetComponent<Collider>();
        if (myCollider != null) myCollider.enabled = false;

        if (health != null)
        {
            health.Die();   // stops movement and plays the Die animation
        }
        else
        {
            if (movement != null) movement.enabled = false;

            // 2. Turn off the Character Controller
            if (controller != null) controller.enabled = false;

            if (agent != null && agent.enabled)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                // 3. Turn off the Agent entirely so it stops acting as a solid obstacle
                agent.enabled = false;
            }
            if (animator != null) animator.SetFloat("Speed", 0f);
        }

        if (RoundManager.Instance != null) RoundManager.Instance.ReportCaught(this);
    }
}
