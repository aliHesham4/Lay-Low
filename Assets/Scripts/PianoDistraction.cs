using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Same mechanism as the TV, the vacuum and the alarm clock: at one random moment (maybe never, if the round ends
// first) the piano starts playing by itself, the killer walks over to stop it, and it never plays again that game.
// It shares DistractionGate with them: only one at a time, with a break between them.
// While it plays, random keys go down and up (more keys at once when the music is louder).
public class PianoDistraction : MonoBehaviour
{
    [Header("Piano")]
    public AudioSource pianoAudio;
    public AudioClip pianoClip;
    [Tooltip("Parent of the separate key objects (each key's pivot is its hinge at the back).")]
    public Transform keysRoot;
    [Tooltip("Where the enemy should stand to stop it (in front of the keyboard).")]
    public Transform turnOffPosition;
    [Tooltip("Yellow outline shown while it plays (optional).")]
    public DistractionOutline outline;

    [Header("Sound")]
    [Range(0f, 1f)] public float volume = 1f;
    [Tooltip("Seconds of fade-out after the killer stops it.")]
    public float fadeOutTime = 0.8f;

    [Header("Keys")]
    [Tooltip("How far a key tips down when pressed (degrees).")]
    public float pressAngle = 4f;
    [Tooltip("Black keys are shorter, so they tip a bit more for the same depth.")]
    public float blackKeyAngleScale = 1.8f;
    [Tooltip("Key presses per second at full loudness.")]
    public float pressesPerSecond = 14f;
    public Vector2 holdTime = new Vector2(0.08f, 0.35f);
    [Tooltip("Speed of a key going down / coming back up (degrees per second).")]
    public float keySpeed = 160f;

    [Header("Settings")]
    public float attentionRadius = 40f;
    public float minWaitTime = 30f;
    public float maxWaitTime = 150f;
    [Tooltip("Safety: stops by itself if the enemy never gets there, so it cannot block the other distractions forever.")]
    public float maxOnTime = 90f;

    public bool IsOn { get; private set; }

    class Key { public Transform t; public Quaternion rest; public float angle, target, releaseAt; public bool black; }
    readonly List<Key> keys = new List<Key>();
    bool hasPlayedOnce, fading;
    float onSince, fadeTimer, pressBudget;
    NavMeshAgent respondingAgent;
    float[] loudness;          // loudness of the clip, 20 values per second
    const int EnvRate = 20;

    void Start()
    {
        if (pianoAudio == null) pianoAudio = gameObject.AddComponent<AudioSource>();
        pianoAudio.playOnAwake = false;
        pianoAudio.loop = true;
        pianoAudio.clip = pianoClip;

        if (keysRoot != null)
            foreach (Transform k in keysRoot)
                keys.Add(new Key { t = k, rest = k.localRotation, black = k.name.StartsWith("Black") });
        BuildLoudness();

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
        if (!DistractionGate.CanStart()) { Invoke(nameof(TurnOn), 1f); return; }

        hasPlayedOnce = true;
        IsOn = true;
        fading = false;
        onSince = Time.time;
        DistractionGate.Started();

        pianoAudio.volume = volume;
        pianoAudio.loop = true;
        pianoAudio.time = 0f;
        pianoAudio.Play();
        if (outline != null) outline.SetVisible(true);

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
            PlayKeys(CurrentLoudness());

            if (Time.time - onSince > maxOnTime) { TurnOff(); return; }

            if (respondingAgent != null)
            {
                EnemyVision ev = respondingAgent.GetComponent<EnemyVision>();
                if (ev == null || ev.IsGoingToAppliance)
                {
                    Vector3 targetPos = turnOffPosition != null ? turnOffPosition.position : transform.position;
                    Vector3 agentPos = respondingAgent.transform.position;
                    targetPos.y = agentPos.y;
                    if (Vector3.Distance(agentPos, targetPos) <= 1f) TurnOff();
                }
            }
        }
        else if (fading)
        {
            fadeTimer += Time.deltaTime;
            float k = Mathf.Clamp01(fadeTimer / Mathf.Max(0.01f, fadeOutTime));
            pianoAudio.volume = volume * (1f - k);
            if (k >= 1f) { fading = false; pianoAudio.Stop(); pianoAudio.volume = volume; }
        }

        MoveKeys();
    }

    // ---------- keys ----------

    void PlayKeys(float loud)
    {
        if (keys.Count == 0) return;
        pressBudget += Time.deltaTime * pressesPerSecond * Mathf.Lerp(0.15f, 1f, loud);
        while (pressBudget >= 1f)
        {
            pressBudget -= 1f;
            Key k = keys[Random.Range(0, keys.Count)];
            k.target = pressAngle * (k.black ? blackKeyAngleScale : 1f);
            k.releaseAt = Time.time + Random.Range(holdTime.x, holdTime.y);
        }
    }

    void MoveKeys()
    {
        for (int i = 0; i < keys.Count; i++)
        {
            Key k = keys[i];
            if (k.target > 0f && (Time.time >= k.releaseAt || !IsOn)) k.target = 0f;
            if (Mathf.Approximately(k.angle, k.target) && k.target == 0f) continue;
            k.angle = Mathf.MoveTowards(k.angle, k.target, keySpeed * Time.deltaTime);
            // keys hinge at the back; pressing tips the front down
            k.t.localRotation = k.rest * Quaternion.Euler(0f, 0f, -k.angle);
        }
    }

    // ---------- loudness of the music, so busy passages press more keys ----------

    void BuildLoudness()
    {
        if (pianoClip == null) return;
        if (pianoClip.loadState != AudioDataLoadState.Loaded) pianoClip.LoadAudioData();
        int ch = pianoClip.channels;
        float[] all = new float[pianoClip.samples * ch];
        if (!pianoClip.GetData(all, 0)) return;
        int per = Mathf.Max(1, pianoClip.frequency / EnvRate);
        int n = Mathf.CeilToInt((float)pianoClip.samples / per);
        loudness = new float[n];
        float max = 0.0001f;
        for (int w = 0; w < n; w++)
        {
            double sum = 0; int cnt = 0;
            for (int f = w * per; f < Mathf.Min(pianoClip.samples, (w + 1) * per); f++)
                for (int c = 0; c < ch; c++) { float s = all[f * ch + c]; sum += s * s; cnt++; }
            loudness[w] = cnt > 0 ? Mathf.Sqrt((float)(sum / cnt)) : 0f;
            max = Mathf.Max(max, loudness[w]);
        }
        for (int w = 0; w < n; w++) loudness[w] /= max;
    }

    float CurrentLoudness()
    {
        if (loudness == null || loudness.Length == 0) return 0.6f;
        int i = Mathf.Clamp((int)(pianoAudio.time * EnvRate), 0, loudness.Length - 1);
        return loudness[i];
    }

    public void TurnOff()
    {
        bool wasOn = IsOn;
        IsOn = false;
        respondingAgent = null;
        if (outline != null) outline.SetVisible(false);

        if (wasOn)
        {
            fading = true;
            fadeTimer = 0f;
            DistractionGate.Ended();
            foreach (EnemyVision enemy in FindObjectsByType<EnemyVision>(FindObjectsSortMode.None)) enemy.ApplianceEnded(this);
        }
    }
}
