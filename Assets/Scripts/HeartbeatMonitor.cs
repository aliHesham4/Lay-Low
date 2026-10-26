using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// Player 1's heartbeat: a quiet steady beat while the killer is far away that gets faster, louder
// and higher pitched as he gets closer. Also drives the BPM readout + pulsing heart in the HUD.
// "Close" means how far he would have to WALK to reach me (around walls, through doors),
// plus a little extra when a wall is between us, so a killer in the next room does not cause panic.
[RequireComponent(typeof(Hider))]
public class HeartbeatMonitor : MonoBehaviour
{
    [Header("Killer distance (metres he would have to walk to reach me)")]
    [Tooltip("At or beyond this distance the heart is calm.")]
    public float calmDistance = 9f;
    [Tooltip("At or inside this distance the heart is at its maximum.")]
    public float panicDistance = 2f;
    [Tooltip("Use the walking route around walls (through doors) instead of the straight line through walls.")]
    public bool useWalkingDistance = true;
    [Tooltip("Extra metres added when a wall is between us (no line of sight), so the next room feels further.")]
    public float wallPenalty = 2f;
    [Tooltip("How often the walking route is recalculated (seconds).")]
    public float pathInterval = 0.25f;
    [Tooltip("Height of the wall check above the feet. Must be lower than the door frames (~1.3 m in this house).")]
    public float eyeHeight = 0.4f;
    [Tooltip("Layers that count as a wall between us.")]
    public LayerMask wallMask = 1 << 8; // Wall

    [Header("Rate")]
    [Tooltip("Higher = the heart stays calm longer and only panics when he is really close.")]
    public float panicCurve = 2.5f;
    public float restBpm = 60f;
    public float maxBpm = 160f;
    [Tooltip("How fast the heart speeds up when the killer gets closer (level units per second).")]
    public float riseSpeed = 0.6f;
    [Tooltip("How fast it calms down again once he leaves (level units per second).")]
    public float fallSpeed = 0.25f;

    [Header("Sound")]
    public AudioClip beatClip;
    [Range(0f, 1f)] public float restVolume = 0.05f;
    [Range(0f, 1f)] public float maxVolume = 0.6f;
    public float restPitch = 0.9f;
    public float maxPitch = 1.3f;
    [Tooltip("Seconds of leading silence in the clip to skip so every beat starts on the thump.")]
    public float clipStartOffset = 0.18f;

    [Header("HUD")]
    public Image heartIcon;
    public TMP_Text bpmText;
    public float pulseScale = 1.3f;
    public Color calmColor = new Color(0.85f, 0.25f, 0.25f, 1f);
    public Color panicColor = new Color(1f, 0.02f, 0.02f, 1f);
    public Color deadColor = new Color(0.45f, 0.45f, 0.45f, 1f);

    Hider hider;
    AudioSource source;
    EnemyVision enemy;
    float level;      // 0 = calm, 1 = killer on top of me
    float beatTimer;
    float pulse;
    float shownBpm;
    float findTimer;
    float pathTimer;
    float feltDistance = 999f;
    bool wallBetween;
    NavMeshPath path;

    public float Level { get { return level; } }
    public float CurrentBpm { get { return Mathf.Lerp(restBpm, maxBpm, level); } }
    public float FeltDistanceToKiller { get { return feltDistance; } }
    public bool WallBetween { get { return wallBetween; } }

    void Awake()
    {
        hider = GetComponent<Hider>();
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f; // 2D: it is inside the player's own chest
        shownBpm = restBpm;
        beatTimer = 0.5f;
        path = new NavMeshPath();
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (hider != null && hider.IsCaught)
        {
            if (source.isPlaying) source.Stop();
            pulse = 0f;
            Draw(0f, deadColor, true);
            return;
        }

        if (enemy == null)
        {
            findTimer -= dt;
            if (findTimer <= 0f) { enemy = FindFirstObjectByType<EnemyVision>(); findTimer = 1f; }
        }

        float target = 0f;
        if (enemy != null && enemy.enabled)
        {
            pathTimer -= dt;
            if (pathTimer <= 0f) { pathTimer = pathInterval; feltDistance = MeasureDistance(); }
            target = Mathf.Pow(Mathf.InverseLerp(calmDistance, panicDistance, feltDistance), panicCurve);
            // Being chased or stabbed: the heart is racing regardless of the distance.
            if (enemy.CurrentTarget == hider) target = 1f;
        }

        level = Mathf.MoveTowards(level, target, (target > level ? riseSpeed : fallSpeed) * dt);

        float bpm = CurrentBpm;
        beatTimer -= dt;
        if (beatTimer <= 0f)
        {
            Beat();
            beatTimer += 60f / Mathf.Max(30f, bpm);
            if (beatTimer < 0f) beatTimer = 0f;
        }

        pulse = Mathf.MoveTowards(pulse, 0f, dt * 6f);
        shownBpm = Mathf.Lerp(shownBpm, bpm, dt * 3f);
        Draw(shownBpm, Color.Lerp(calmColor, panicColor, level), false);
    }

    // How far the killer "feels": walking distance around walls + a penalty if a wall blocks the view.
    float MeasureDistance()
    {
        Vector3 me = transform.position;
        Vector3 him = enemy.transform.position;
        Vector3 flat = him - me; flat.y = 0f;
        float straight = flat.magnitude;
        float dist = straight;

        if (useWalkingDistance)
        {
            NavMeshHit a, b;
            if (NavMesh.SamplePosition(me, out a, 2f, NavMesh.AllAreas) &&
                NavMesh.SamplePosition(him, out b, 2f, NavMesh.AllAreas) &&
                NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) &&
                path.status == NavMeshPathStatus.PathComplete)
            {
                float len = 0f;
                Vector3[] c = path.corners;
                for (int i = 1; i < c.Length; i++) len += Vector3.Distance(c[i - 1], c[i]);
                dist = Mathf.Max(straight, len);
            }
        }

        // Is a wall between us? (eye height to eye height, using the killer's own obstacle layers)
        Vector3 from = me + Vector3.up * eyeHeight;
        Vector3 to = him + Vector3.up * eyeHeight;
        Vector3 dir = to - from;
        wallBetween = false;
        RaycastHit[] hits = Physics.RaycastAll(from, dir.normalized, dir.magnitude, wallMask, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit h in hits)
        {
            if (h.transform.IsChildOf(transform) || h.transform.IsChildOf(enemy.transform)) continue;
            wallBetween = true;
            break;
        }
        if (wallBetween) dist += wallPenalty;

        return dist;
    }

    void Beat()
    {
        pulse = 1f;
        if (beatClip == null) return;
        source.Stop();
        source.clip = beatClip;
        source.volume = Mathf.Lerp(restVolume, maxVolume, level);
        source.pitch = Mathf.Lerp(restPitch, maxPitch, level);
        source.time = Mathf.Clamp(clipStartOffset, 0f, beatClip.length - 0.05f);
        source.Play();
    }

    void Draw(float bpm, Color col, bool dead)
    {
        if (heartIcon != null)
        {
            heartIcon.color = col;
            float s = 1f + (pulseScale - 1f) * pulse;
            heartIcon.rectTransform.localScale = new Vector3(s, s, 1f);
        }
        if (bpmText != null)
            bpmText.text = (dead ? "--" : Mathf.RoundToInt(bpm).ToString()) + "\n<size=40%><color=#CCCCCC>BPM</color></size>";
    }
}
