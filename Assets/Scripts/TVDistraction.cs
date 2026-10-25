using UnityEngine;
using UnityEngine.AI;

public class TVDistraction : MonoBehaviour
{
    [Header("TV Components")]
    public GameObject glitchScreenVisual;
    public Light tvLight;
    public AudioSource tvAudio;

    [Tooltip("Where the enemy should stand to turn it off.")]
    public Transform turnOffPosition;

    [Tooltip("Yellow border shown on the HUD while the TV is on (optional).")]
    public DistractionOutline outline;

    [Header("Settings")]
    public float attentionRadius = 25f; // Same as the stone's radius
    public float minWaitTime = 60f;
    public float maxWaitTime = 180f;
    [Tooltip("Safety: switches itself off if the enemy never gets there, so it cannot block the vacuum forever.")]
    public float maxOnTime = 90f;

    public bool IsOn { get; private set; }
    private bool hasPlayedOnce = false;
    private float onSince;
    private NavMeshAgent respondingAgent;

    void Start()
    {
        TurnOff();

        // Schedule the random one-time glitch
        float randomTime = Random.Range(minWaitTime, maxWaitTime);
        Invoke(nameof(TurnOn), randomTime);
    }

void TurnOn()
    {
        if (hasPlayedOnce) return;

        // Only one distraction at a time and a 10 s break between them: try again shortly.
        if (!DistractionGate.CanStart())
        {
            Invoke(nameof(TurnOn), 1f);
            return;
        }

        hasPlayedOnce = true;
        IsOn = true;
        onSince = Time.time;
        DistractionGate.Started();

        if (glitchScreenVisual != null) glitchScreenVisual.SetActive(true);
        if (tvLight != null) tvLight.enabled = true;
        if (tvAudio != null) tvAudio.Play();
        if (outline != null) outline.SetVisible(true);

        EnemyVision[] enemies = FindObjectsByType<EnemyVision>(FindObjectsSortMode.None);
        Debug.Log($"TV Turned on! Found {enemies.Length} enemies in the scene."); // <--- ADD THIS

        foreach (EnemyVision enemy in enemies)
        {
            float dist = Vector3.Distance(transform.position, enemy.transform.position);
            Debug.Log($"Enemy is {dist} meters away."); // <--- ADD THIS

            if (dist <= attentionRadius)
            {
                NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    Vector3 destination = turnOffPosition != null ? turnOffPosition.position : transform.position;
                    agent.SetDestination(destination);
                    respondingAgent = agent;
                    break;
                }
            }
        }
    }

    void Update()
    {
        if (IsOn && Time.time - onSince > maxOnTime) { TurnOff(); return; }

        if (IsOn && respondingAgent != null)
        {
            Vector3 targetPos = turnOffPosition != null ? turnOffPosition.position : transform.position;
            Vector3 agentPos = respondingAgent.transform.position;

            // Ignore height differences by forcing them to the same Y level
            targetPos.y = agentPos.y;

            // Calculate flat 2D distance
            float distance = Vector3.Distance(agentPos, targetPos);

            if (distance <= 1f)
            {
                // 1. Reset the path FIRST while we still have the reference
                if (respondingAgent.isActiveAndEnabled && respondingAgent.isOnNavMesh)
                {
                    respondingAgent.ResetPath();
                }

                // 2. THEN turn off the TV (which clears the respondingAgent variable)
                TurnOff();
            }
        }
    }

    public void TurnOff()
    {
        bool wasOn = IsOn;
        IsOn = false;
        respondingAgent = null;

        if (glitchScreenVisual != null) glitchScreenVisual.SetActive(false);
        if (tvLight != null) tvLight.enabled = false;
        if (tvAudio != null) tvAudio.Stop();
        if (outline != null) outline.SetVisible(false);

        if (wasOn) DistractionGate.Ended();
    }
}