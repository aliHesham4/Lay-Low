using UnityEngine;
using UnityEngine.AI;

// Same mechanism as the TV and the vacuum: at one random moment (maybe never, if the round ends first)
// the alarm clock starts ringing, the killer walks over to switch it off, and it never rings again that game.
// It shares DistractionGate with them: only one at a time, with a break between them.
// While ringing it loops the first seconds of the alarm sound and the clock rattles.
// When the killer switches it off, the sound jumps to the clip's own ring-out and fades away.
public class ClockAlarmDistraction : MonoBehaviour
{
    [Header("Clock")]
    public AudioSource alarmAudio;
    public AudioClip alarmClip;
    [Tooltip("The part of the clock that rattles while ringing (the model).")]
    public Transform shakeTarget;

    [Tooltip("Where the enemy should stand to switch it off.")]
    public Transform turnOffPosition;

    [Tooltip("Yellow outline shown while the alarm rings (optional).")]
    public DistractionOutline outline;

    [Header("Sound")]
    [Tooltip("The first seconds of the clip that loop while it rings.")]
    public float loopEnd = 3f;
    [Tooltip("Seconds of fade-out after the killer switches it off.")]
    public float fadeOutTime = 1.5f;
    [Range(0f, 1f)] public float volume = 1f;

    [Header("Rattle")]
    public float shakeAngle = 6f;
    public float shakeSpeed = 45f;

    [Header("Settings")]
    public float attentionRadius = 40f;
    public float minWaitTime = 30f;
    public float maxWaitTime = 150f;
    [Tooltip("Safety: switches itself off if the enemy never gets there, so it cannot block the TV / vacuum forever.")]
    public float maxOnTime = 90f;

    public bool IsOn { get; private set; }
    bool hasPlayedOnce;
    float onSince;
    NavMeshAgent respondingAgent;
    bool fading;
    float fadeTimer;
    Quaternion shakeRest;

    void Start()
    {
        if (alarmAudio == null) alarmAudio = gameObject.AddComponent<AudioSource>();
        alarmAudio.playOnAwake = false;
        alarmAudio.loop = false;
        BuildClips();
        if (shakeTarget != null) shakeRest = shakeTarget.localRotation;

        IsOn = false;
        if (outline != null) outline.SetVisible(false);
        Invoke(nameof(TurnOn), Random.Range(minWaitTime, maxWaitTime));
    }

    void TurnOn()
    {
        // its room is sealing / sealed (environmental state): it stays off for the rest of the round,
        // otherwise the killer would keep trying to reach a switch behind a locked door
        if (EnvironmentalHazard.IsDangerous(turnOffPosition != null ? turnOffPosition.position : transform.position)) return;
        if (hasPlayedOnce) return;

        // Only one distraction at a time and a break between them: try again shortly.
        if (!DistractionGate.CanStart())
        {
            Invoke(nameof(TurnOn), 1f);
            return;
        }

        hasPlayedOnce = true;
        IsOn = true;
        fading = false;
        onSince = Time.time;
        DistractionGate.Started();

        alarmAudio.clip = loopClip != null ? loopClip : alarmClip;
        alarmAudio.loop = true; // the loop clip is exactly the first loopEnd seconds
        alarmAudio.volume = volume;
        alarmAudio.time = 0f;
        alarmAudio.Play();
        if (outline != null) outline.SetVisible(true);

        // The killer comes to switch it off: right away if he is wandering or going to a sound,
        // after he finishes if he is chasing / killing / opening a hiding spot.
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
        if (IsOn)
        {
            // the loop clip loops by itself (AudioSource.loop)

            Rattle(1f);

            if (Time.time - onSince > maxOnTime) { TurnOff(); return; }

            if (respondingAgent != null)
            {
                EnemyVision ev = respondingAgent.GetComponent<EnemyVision>();
                if (ev != null && !ev.IsGoingToAppliance) return; // only switches off when he actually came for it

                Vector3 targetPos = turnOffPosition != null ? turnOffPosition.position : transform.position;
                Vector3 agentPos = respondingAgent.transform.position;
                Vector3 flat = targetPos; flat.y = agentPos.y;
                // close in a straight line is not enough (the corridor is right behind the wall):
                // he has to actually be at the clock, measured along the floor around the walls
                if (Vector3.Distance(agentPos, flat) <= 1f && EnemyVision.WalkingDistance(agentPos, targetPos) <= 1.2f) TurnOff();
            }
        }
        else if (fading)
        {
            fadeTimer += Time.deltaTime;
            float k = Mathf.Clamp01(fadeTimer / Mathf.Max(0.01f, fadeOutTime));
            alarmAudio.volume = volume * (1f - k);
            Rattle(1f - k);
            if (k >= 1f || !alarmAudio.isPlaying)
            {
                fading = false;
                alarmAudio.Stop();
                alarmAudio.volume = volume;
                Rattle(0f);
            }
        }
    }

    AudioClip loopClip, tailClip;

    // Cut the alarm into: the first loopEnd seconds (loops seamlessly) and the rest (the ring-out).
    void BuildClips()
    {
        if (alarmClip == null) return;
        if (alarmClip.loadState != AudioDataLoadState.Loaded) alarmClip.LoadAudioData();
        int ch = alarmClip.channels, hz = alarmClip.frequency;
        float[] all = new float[alarmClip.samples * ch];
        if (!alarmClip.GetData(all, 0)) { loopClip = alarmClip; return; }

        int loopFrames = Mathf.Clamp(Mathf.RoundToInt(loopEnd * hz), 1, alarmClip.samples);
        float[] a = new float[loopFrames * ch];
        System.Array.Copy(all, a, a.Length);
        // tiny fade at the seam so the loop never clicks
        int fade = Mathf.Min(loopFrames / 4, hz / 200);
        for (int i = 0; i < fade; i++)
        {
            float g = (float)i / fade;
            for (int c = 0; c < ch; c++) { a[i * ch + c] *= g; a[(loopFrames - 1 - i) * ch + c] *= g; }
        }
        loopClip = AudioClip.Create(alarmClip.name + "_loop", loopFrames, ch, hz, false);
        loopClip.SetData(a, 0);

        int tailFrames = alarmClip.samples - loopFrames;
        if (tailFrames > 0)
        {
            float[] b = new float[tailFrames * ch];
            System.Array.Copy(all, loopFrames * ch, b, 0, b.Length);
            tailClip = AudioClip.Create(alarmClip.name + "_tail", tailFrames, ch, hz, false);
            tailClip.SetData(b, 0);
        }
    }

    void Rattle(float amount)
    {
        if (shakeTarget == null) return;
        if (amount <= 0f) { shakeTarget.localRotation = shakeRest; return; }
        float a = Mathf.Sin(Time.time * shakeSpeed) * shakeAngle * amount;
        float b = Mathf.Sin(Time.time * shakeSpeed * 1.37f + 1.1f) * shakeAngle * 0.5f * amount;
        shakeTarget.localRotation = shakeRest * Quaternion.Euler(b, 0f, a);
    }

    public void TurnOff()
    {
        bool wasOn = IsOn;
        IsOn = false;
        respondingAgent = null;
        if (outline != null) outline.SetVisible(false);

        if (wasOn)
        {
            // let the alarm ring out: jump past the looped part to the clip's own ending and fade it away
            alarmAudio.loop = false;
            if (tailClip != null)
            {
                alarmAudio.clip = tailClip;
                alarmAudio.time = 0f;
                alarmAudio.Play();
            }
            fading = true;
            fadeTimer = 0f;

            DistractionGate.Ended();
            foreach (EnemyVision enemy in FindObjectsByType<EnemyVision>(FindObjectsSortMode.None)) enemy.ApplianceEnded(this);
        }
    }
}
