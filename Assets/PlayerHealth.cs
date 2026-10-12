using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Animator Setup")]
    public string dieTriggerName = "Die"; // Name of your Death trigger parameter in Animator

    private Animator playerAnimator;
    private CharacterController characterController;
    private MonoBehaviour playerMovementScript;
    private bool isDead = false;

void Awake()
    {
        playerAnimator = GetComponentInChildren<Animator>();
        characterController = GetComponent<CharacterController>();

        // The script that moves this character: keyboard (Player1) or AI (Player2)
        playerMovementScript = GetComponent<PlayerMovement>();
        if (playerMovementScript == null) playerMovementScript = GetComponent<AIHider>();
    }

public void Die()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log(name + " killed by Enemy!");

        // 1. Disable character controller so gravity/inputs stop moving the player
        if (characterController != null)
        {
            characterController.enabled = false;
        }

        // 2. Disable the movement script (keyboard or AI)
        if (playerMovementScript != null)
        {
            playerMovementScript.enabled = false;
        }

        // 3. Stop the NavMeshAgent if this is the AI player
        UnityEngine.AI.NavMeshAgent agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        // 4. Play death animation
        if (playerAnimator != null && !string.IsNullOrEmpty(dieTriggerName))
        {
            playerAnimator.SetTrigger(dieTriggerName);
        }
    }
}