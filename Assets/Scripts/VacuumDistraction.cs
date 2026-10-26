using UnityEngine;
using UnityEngine.AI;

// Same mechanism as the TV: at one random moment (maybe never, if the round ends first) the vacuum
// switches on, the enemy walks over to turn it off, and it never plays again that game.
public class VacuumDistraction : MonoBehaviour
{
    [Header("Vacuum Components")]
    public AudioSource vacuumAudio;

    [Tooltip("Where the enemy should stand to turn it off.")]
    public Transform turnOffPosition;

    [Tooltip("Yellow border shown on the HUD while the vacuum is on (optional).")]
    public DistractionOutline outline;

    [Header("Settings")]
    public float attentionRadius = 40f;
    public float minWaitTime = 20f;
    public float maxWaitTime = 120f;
    [Tooltip("Safety: switches itself off if the enemy never gets there, so it cannot block the TV forever.")]
    public float maxOnTime = 90f;

    public bool IsOn { get; private set; }
    private bool hasPlayedOnce = false;
    private float onSince;
    private NavMeshAgent respondingAgent;

    void Start()
    {
        TurnOff();
        Invoke(nameof(TurnOn), Random.Range(minWaitTime, maxWaitTime));
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

        if (vacuumAudio != null) vacuumAudio.Play();
        if (outline != null) outline.SetVisible(true);

        // The killer comes to switch it off: right away if he is wandering or going to a sound,
        // after he finishes if he is chasing / killing / checking a hiding spot.
        Vector3 destination = turnOffPosition != null ? turnOffPosition.position : transform.position;
        foreach (EnemyVision enemy in FindObjectsByType<EnemyVision>(FindObjectsSortMode.None))
        {
            if (enemy.HearAppliance(this, destination, attentionRadius, TurnOff))
            {
                respondingAgent = enemy.GetComponent<NavMeshAgent>();
                break;
            }
        }
    }

    void Update()
    {
        if (!IsOn) return;

        if (Time.time - onSince > maxOnTime) { TurnOff(); return; }

        if (respondingAgent != null)
        {
            EnemyVision ev = respondingAgent.GetComponent<EnemyVision>();
            if (ev != null && !ev.IsGoingToAppliance) return; // only switches off when he actually came for it

            Vector3 targetPos = turnOffPosition != null ? turnOffPosition.position : transform.position;
            Vector3 agentPos = respondingAgent.transform.position;
            targetPos.y = agentPos.y;

            if (Vector3.Distance(agentPos, targetPos) <= 1f) TurnOff();
        }
    }

    public void TurnOff()
    {
        bool wasOn = IsOn;
        IsOn = false;
        respondingAgent = null;

        if (vacuumAudio != null) vacuumAudio.Stop();
        if (outline != null) outline.SetVisible(false);

        if (wasOn)
        {
            DistractionGate.Ended();
            foreach (EnemyVision enemy in FindObjectsByType<EnemyVision>(FindObjectsSortMode.None)) enemy.ApplianceEnded(this);
        }
    }
}
