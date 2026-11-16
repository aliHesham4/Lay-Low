using UnityEngine;

// A door that swings open on its own when a player or the killer walks up to it, away from them
// (it is pushed open), and swings shut again after they have gone through.
// The door has NO collider: it never blocks or pushes anyone, and it does not block the NavMesh.
// Put it on the hinge (the pivot at the door's edge); the door panel is a child of it.
public class DoorSwing : MonoBehaviour
{
    [HideInInspector] public bool hasClosedRotation;
    [HideInInspector] public Quaternion closedLocalRotation = Quaternion.identity;

    [Header("Mode")]
    [Tooltip("The door starts (and rests) fully open around its hinge.")]
    public bool startOpen = true;
    [Tooltip("Which way it rests open: +1 or -1 (chosen by the builder so it lies against the wall, not in furniture).")]
    public float openSide = 1f;
    [Tooltip("Swing automatically when people come near. Off = it just stays in its start position.")]
    public bool autoSwing = false;

    [Tooltip("How close someone must get (from the middle of the doorway) to open it.")]
    public float openDistance = 1.1f;
    [Tooltip("It closes once everyone is further than this.")]
    public float closeDistance = 1.5f;
    public float openAngle = 90f;
    [Tooltip("Degrees per second.")]
    public float swingSpeed = 260f;
    [Tooltip("Seconds it stays open after the last person leaves.")]
    public float closeDelay = 0.6f;
    [Tooltip("Width of the doorway (metres), set by the builder; used to find the doorway's middle.")]
    public float width = 0.43f;
    [Tooltip("+1 or -1: which way positive swing goes (set by the builder for mirrored double doors).")]
    public float side = 1f;

    [Header("Sounds (optional)")]
    public AudioClip openSound;
    public AudioClip closeSound;
    [Range(0f, 1f)] public float soundVolume = 0.6f;

    Quaternion closedRot;
    float angle;        // current swing
    float target;       // wanted swing
    float lastNear = -99f;
    static Transform[] movers;
    static float moversFoundAt = -99f;

    Vector3 middle, closedForward;

    void Start()
    {
        closedRot = hasClosedRotation ? closedLocalRotation : transform.localRotation;
        // measured from the closed pose (the door's own axes turn as it swings)
        Quaternion closedWorld = (transform.parent != null ? transform.parent.rotation : Quaternion.identity) * closedRot;
        middle = transform.position + closedWorld * Vector3.right * (width * 0.5f) + Vector3.up * 0.5f;
        closedForward = closedWorld * Vector3.forward;

        if (startOpen)
        {
            angle = target = openSide * openAngle;
            transform.localRotation = closedRot * Quaternion.Euler(0f, angle * side, 0f);
        }
    }

    public Vector3 DoorwayMiddle { get { return middle; } }
    public float OpenAngleNow { get { return angle; } }

    Vector3 Middle() { return middle; }

    static Transform[] Movers()
    {
        if (movers == null || Time.time - moversFoundAt > 2f)
        {
            var list = new System.Collections.Generic.List<Transform>();
            foreach (Hider h in FindObjectsByType<Hider>(FindObjectsSortMode.None)) list.Add(h.transform);
            foreach (EnemyVision e in FindObjectsByType<EnemyVision>(FindObjectsSortMode.None)) list.Add(e.transform);
            movers = list.ToArray();
            moversFoundAt = Time.time;
        }
        return movers;
    }

    void Update()
    {
        if (!autoSwing) return; // rests where it started
        Vector3 mid = Middle();
        Transform nearest = null;
        float best = float.MaxValue;
        foreach (Transform m in Movers())
        {
            if (m == null) continue;
            Hider h = m.GetComponent<Hider>();
            if (h != null && (h.IsCaught || h.IsHidden)) continue;
            Vector3 d = m.position - mid; d.y = 0f;
            float dist = d.magnitude;
            if (dist < best) { best = dist; nearest = m; }
        }

        if (nearest != null && best <= closeDistance) lastNear = Time.time;

        if (target == 0f)
        {
            if (nearest != null && best <= openDistance)
            {
                // open away from the person (they push it)
                Vector3 toPerson = nearest.position - mid;
                float which = Vector3.Dot(toPerson, closedForward) >= 0f ? 1f : -1f; // +angle swings the panel toward -forward
                target = which * openAngle;
                Play(openSound);
            }
        }
        else if (Time.time - lastNear > closeDelay)
        {
            target = 0f;
        }

        float before = angle;
        angle = Mathf.MoveTowards(angle, target, swingSpeed * Time.deltaTime);
        if (before != 0f && angle == 0f) Play(closeSound);
        transform.localRotation = closedRot * Quaternion.Euler(0f, angle * side, 0f);
    }

    void Play(AudioClip c)
    {
        if (c != null) AudioSource.PlayClipAtPoint(c, Middle(), soundVolume);
    }
}
