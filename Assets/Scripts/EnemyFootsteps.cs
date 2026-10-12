using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(AudioSource))]
public class EnemyFootsteps : MonoBehaviour
{
    private NavMeshAgent agent;
    private AudioSource audioSource;

    [Header("Footstep Audio")]
    [Tooltip("Drop your footstep sound clips here.")]
    public AudioClip[] footstepClips;

    [Header("Step Timing")]
    [Tooltip("Time between steps while walking (patrolling).")]
    public float walkStepInterval = 0.5f;
    [Tooltip("Time between steps while running (chasing). Faster = smaller number.")]
    public float runStepInterval = 0.25f;

    [Tooltip("Speed threshold to consider the enemy moving.")]
    public float minVelocityThreshold = 0.1f;
    [Tooltip("Speed above which the enemy is considered running.")]
    public float runSpeedThreshold = 3f;

    private float stepTimer;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        audioSource = GetComponent<AudioSource>();

        // Ensure AudioSource is set to 3D sound so it fades out with distance
        audioSource.spatialBlend = 1f;
    }

    void Update()
    {
        // If the enemy is stopped or not moving fast enough, don't play footsteps
        if (agent.velocity.magnitude < minVelocityThreshold || !agent.isOnNavMesh)
        {
            stepTimer = 0f; // Reset so the first step plays immediately when moving again
            return;
        }

        // Determine whether to use walking or running timing based on current speed
        bool isRunning = agent.speed > runSpeedThreshold || agent.velocity.magnitude > runSpeedThreshold;
        float currentInterval = isRunning ? runStepInterval : walkStepInterval;

        // Count down the step timer
        stepTimer -= Time.deltaTime;
        if (stepTimer <= 0f)
        {
            PlayRandomFootstep();
            stepTimer = currentInterval; // Reset timer for the next step
        }
    }

    void PlayRandomFootstep()
    {
        if (footstepClips == null || footstepClips.Length == 0) return;

        // Pick a random clip from the array so it sounds natural
        AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];

        // Play with a slight random pitch variation to avoid a robotic sound
        audioSource.pitch = Random.Range(0.9f, 1.1f);
        audioSource.PlayOneShot(clip, 0.7f);
    }
}