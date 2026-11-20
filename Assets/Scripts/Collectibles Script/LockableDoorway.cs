using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// One per doorway. When a player uses the door lock on it, a metal shutter slams down,
// blocking players and the killer (and his view) for a few seconds, then rolls back up.
// The object sits at the bottom-centre of the opening, forward = through the wall, right = along the opening.
public class LockableDoorway : MonoBehaviour
{
    [Header("Opening (set by the builder)")]
    public float width = 0.7f;
    public float height = 0.98f;
    public float thickness = 0.09f;

    [Header("Parts")]
    public Transform shutter;        // pivot at the top of the opening, scaled down/up in Y
    public Renderer stripe;          // coloured strip at the bottom of the shutter
    public BoxCollider blocker;      // stops players
    public NavMeshObstacle obstacle; // stops the killer (carves the NavMesh)
    public Renderer highlight;       // see-through panel shown while a player is choosing

    [Header("Feel")]
    public AudioClip slamSound;
    [Range(0f, 1f)] public float slamVolume = 1f;
    public float slamTime = 0.12f;
    public float raiseTime = 0.4f;

    static readonly List<LockableDoorway> all = new List<LockableDoorway>();
    public static List<LockableDoorway> All { get { return all; } }

    public bool IsLocked { get; private set; }
    public Hider LockedBy { get; private set; }
    public Vector3 Center { get { return transform.position + transform.up * (height * 0.5f); } }

    float unlockAt;
    Coroutine anim;
    MaterialPropertyBlock mpb;

    void OnEnable() { if (!all.Contains(this)) all.Add(this); }
    void OnDisable() { all.Remove(this); }

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
        // keep the steel-vented slats the same size on narrow and wide doorways
        if (shutter != null)
            foreach (Renderer r in shutter.GetComponentsInChildren<Renderer>(true))
                if (r != stripe)
                {
                    var block = new MaterialPropertyBlock();
                    block.SetVector("_BaseMap_ST", new Vector4(Mathf.Max(0.1f, width / 0.65f), Mathf.Max(0.1f, height), 0f, 0f));
                    r.SetPropertyBlock(block);
                }
        SetShutter(0f);
        if (blocker != null) blocker.enabled = false;
        if (obstacle != null) obstacle.enabled = false;
        if (highlight != null) highlight.enabled = false;
    }

    // ---------- locking ----------

    public bool Lock(Hider by, Color color, float seconds)
    {
        if (IsLocked) return false;
        IsLocked = true;
        LockedBy = by;
        unlockAt = Time.time + seconds;
        SetHighlight(0, color);

        if (stripe != null)
        {
            stripe.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", color);
            mpb.SetColor("_EmissionColor", color * 1.5f);
            stripe.SetPropertyBlock(mpb);
        }
        if (blocker != null) blocker.enabled = true;
        if (obstacle != null) obstacle.enabled = true;
        if (slamSound != null) AudioSource.PlayClipAtPoint(slamSound, Center, slamVolume);

        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(Move(1f, slamTime, true));
        return true;
    }

    // Environmental states: shut for the rest of the round (also takes over a door already locked by a player)
    public void LockForever(Color color)
    {
        if (!IsLocked) Lock(null, color, float.PositiveInfinity);
        unlockAt = float.PositiveInfinity;
        LockedBy = null;
        if (stripe != null)
        {
            stripe.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", color);
            mpb.SetColor("_EmissionColor", color * 1.5f);
            stripe.SetPropertyBlock(mpb);
        }
    }

    void Update()
    {
        if (IsLocked && Time.time >= unlockAt) Unlock();
    }

    void Unlock()
    {
        IsLocked = false;
        LockedBy = null;
        if (blocker != null) blocker.enabled = false;
        if (obstacle != null) obstacle.enabled = false;
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(Move(0f, raiseTime, false));
    }

    IEnumerator Move(float to, float time, bool slam)
    {
        float from = shutter != null ? shutter.localScale.y / Mathf.Max(0.0001f, height) : 0f;
        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / time);
            k = slam ? k * k : 1f - (1f - k) * (1f - k); // slam accelerates, the rise eases out
            SetShutter(Mathf.Lerp(from, to, k));
            yield return null;
        }
        SetShutter(to);
        anim = null;
    }

    void SetShutter(float amount)
    {
        if (shutter == null) return;
        shutter.gameObject.SetActive(amount > 0.001f);
        Vector3 s = shutter.localScale;
        s.y = Mathf.Max(0.0001f, amount * height);
        shutter.localScale = s;
    }

    // ---------- choosing ----------

    // level: 0 = off, 1 = available, 2 = hovered
    public void SetHighlight(int level, Color color)
    {
        if (highlight == null) return;
        highlight.enabled = level > 0 && !IsLocked;
        if (!highlight.enabled) return;
        Color c = color;
        c.a = level == 2 ? 0.75f : 0.28f;
        highlight.GetPropertyBlock(mpb);
        mpb.SetColor("_Color", c);
        mpb.SetFloat("_Hover", level == 2 ? 1f : 0f);
        highlight.SetPropertyBlock(mpb);
    }

    // Is the mouse inside this opening on screen? depth = distance from the camera.
    public bool ScreenContains(Camera cam, Vector2 mouse, out float depth)
    {
        depth = float.MaxValue;
        Vector3 r = transform.right * (width * 0.5f), u = transform.up * height, b = transform.position;
        Vector3[] w = { b - r, b + r, b + r + u, b - r + u };
        Vector2[] p = new Vector2[4];
        for (int i = 0; i < 4; i++)
        {
            Vector3 s = cam.WorldToScreenPoint(w[i]);
            if (s.z <= 0.05f) return false; // behind the camera
            p[i] = s;
        }
        // inside a convex quad: same side of every edge
        float sign = 0f;
        for (int i = 0; i < 4; i++)
        {
            Vector2 a = p[i], c = p[(i + 1) % 4];
            float cross = (c.x - a.x) * (mouse.y - a.y) - (c.y - a.y) * (mouse.x - a.x);
            if (Mathf.Abs(cross) < 1e-4f) continue;
            if (sign == 0f) sign = Mathf.Sign(cross);
            else if (Mathf.Sign(cross) != sign) return false;
        }
        depth = Vector3.Distance(cam.transform.position, Center);
        return true;
    }
}
