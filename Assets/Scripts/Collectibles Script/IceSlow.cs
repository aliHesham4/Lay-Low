using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// Frozen by the ice gun: ice cubes on both legs, slow walking and slow animation for a few seconds,
// with the ice crackling the whole time. Works on the human and on AI players.
// The player can still hide while frozen (the timer keeps running); a pepper melts it at once.
// Runs after the other scripts each frame so it can scale whatever speed they set.
[DefaultExecutionOrder(1000)]
public class IceSlow : MonoBehaviour
{
    float timeLeft;
    float factor = 0.4f;
    Hider hider;
    PlayerMovement movement;
    NavMeshAgent agent;
    Animator animator;
    float animSpeedBefore = 1f;
    float lastAgentSpeed = -1f;
    AudioSource crack;
    readonly List<GameObject> cubes = new List<GameObject>();
    GameObject shatterVfx;
    RawImage frost;

    public bool Active { get { return timeLeft > 0f; } }
    public static bool IsFrozen(GameObject g) { IceSlow s = g != null ? g.GetComponent<IceSlow>() : null; return s != null && s.Active; }

    public static bool Apply(GameObject target, float duration, float slowFactor, AudioClip crackClip, Material iceMaterial, GameObject hitVfx)
    {
        if (target == null) return false;
        Hider h = target.GetComponent<Hider>();
        if (h != null && h.IsCaught) return false;
        IceSlow s = target.GetComponent<IceSlow>();
        if (s == null) s = target.AddComponent<IceSlow>();
        s.Begin(duration, slowFactor, crackClip, iceMaterial, hitVfx);
        return true;
    }

    // a pepper melts the ice straight away
    public static void Cancel(GameObject target)
    {
        IceSlow s = target != null ? target.GetComponent<IceSlow>() : null;
        if (s != null && s.Active) s.End();
    }

    void Begin(float duration, float slowFactor, AudioClip crackClip, Material iceMaterial, GameObject hitVfx)
    {
        factor = Mathf.Clamp(slowFactor, 0.05f, 1f);
        if (!Active)
        {
            hider = GetComponent<Hider>();
            movement = GetComponent<PlayerMovement>();
            agent = GetComponent<NavMeshAgent>();
            animator = FindBodyAnimator();
            if (animator != null) animSpeedBefore = animator.speed;
            lastAgentSpeed = -1f;
            BuildCubes(iceMaterial);
            shatterVfx = hitVfx;
            if (hitVfx != null) foreach (GameObject c in cubes) { GameObject v = Instantiate(hitVfx, c.transform.position, Quaternion.identity); Destroy(v, 3f); }

            if (crackClip != null)
            {
                crack = gameObject.AddComponent<AudioSource>();
                crack.clip = crackClip;
                crack.loop = true;
                crack.spatialBlend = 0.6f;   // heard clearly, but still from where the player is
                crack.minDistance = 3f;
                crack.maxDistance = 25f;
                crack.Play();
            }
            if (hider != null && hider.isHumanControlled) ShowFrost(true);
        }
        timeLeft = duration;
    }

    Animator FindBodyAnimator()
    {
        Animator own = GetComponent<Animator>();
        if (own != null && own.isHuman) return own;
        foreach (Animator a in GetComponentsInChildren<Animator>()) if (a.isHuman) return a;
        return own;
    }

    void Update()
    {
        if (!Active) return;
        if (hider != null && hider.IsCaught) { End(); return; }
        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f) { End(); return; }

        if (movement != null) movement.speedMultiplier = factor;
        if (agent != null && agent.enabled)
        {
            // the AI scripts set their speed every frame; scale it once each time they do
            if (Mathf.Abs(agent.speed - lastAgentSpeed) > 0.001f)
            {
                lastAgentSpeed = agent.speed * factor;
                agent.speed = lastAgentSpeed;
            }
        }
        // heavy, dragging steps (not while hiding: the hiding spot pauses the animation itself)
        if (animator != null && (hider == null || (!hider.IsHidden && !hider.IsCaught))) animator.speed = Mathf.Lerp(0.35f, 1f, factor);
    }

    void LateUpdate()
    {
        if (!Active) return;
        bool show = hider == null || !hider.IsHidden;
        foreach (GameObject c in cubes) if (c != null && c.activeSelf != show) c.SetActive(show);
        if (frost != null)
        {
            Color col = frost.color;
            col.a = 0.55f + 0.1f * Mathf.Sin(Time.time * 3f);
            if (timeLeft < 0.6f) col.a *= timeLeft / 0.6f; // thaws out at the end
            frost.color = col;
        }
    }

    void End()
    {
        timeLeft = 0f;
        if (movement != null) movement.speedMultiplier = 1f;
        if (agent != null && agent.enabled && lastAgentSpeed > 0f && Mathf.Abs(agent.speed - lastAgentSpeed) < 0.001f)
            agent.speed = lastAgentSpeed / factor;
        lastAgentSpeed = -1f;
        // back to normal speed, unless a hiding spot has it paused (it sets 1 again on the way out)
        if (animator != null && (hider == null || (!hider.IsHidden && !hider.IsCaught))) animator.speed = 1f;
        if (crack != null) { crack.Stop(); Destroy(crack); crack = null; }
        foreach (GameObject c in cubes)
            if (c != null)
                Destroy(c); // the ice just goes (no burst at the end)
        cubes.Clear();
        ShowFrost(false);
    }

    void OnDisable() { if (Active) End(); }

    // ---------------- ice cubes around both ankles ----------------
    void BuildCubes(Material mat)
    {
        foreach (GameObject c in cubes) if (c != null) Destroy(c);
        cubes.Clear();
        Animator a = animator;
        if (a != null && a.isHuman)
        {
            AddLeg(a.GetBoneTransform(HumanBodyBones.LeftLowerLeg), a.GetBoneTransform(HumanBodyBones.LeftFoot), mat);
            AddLeg(a.GetBoneTransform(HumanBodyBones.RightLowerLeg), a.GetBoneTransform(HumanBodyBones.RightFoot), mat);
        }
        if (cubes.Count == 0) // no humanoid rig: a block of ice at the feet
        {
            AddCube(transform, transform.position + Vector3.up * 0.12f - transform.right * 0.08f, 0.2f, mat);
            AddCube(transform, transform.position + Vector3.up * 0.12f + transform.right * 0.08f, 0.2f, mat);
        }
    }

    void AddLeg(Transform lowerLeg, Transform foot, Material mat)
    {
        if (lowerLeg == null || foot == null) return;
        float shin = Vector3.Distance(lowerLeg.position, foot.position);
        Vector3 ankle = Vector3.Lerp(lowerLeg.position, foot.position, 0.7f);
        float size = Mathf.Clamp(shin * 0.7f, 0.1f, 0.35f);
        // a few chunky cubes clumped around the ankle
        AddCube(lowerLeg, ankle, size, mat);
        AddCube(lowerLeg, ankle + (lowerLeg.position - foot.position).normalized * size * 0.6f + Random.insideUnitSphere * size * 0.15f, size * 0.8f, mat);
        AddCube(foot, Vector3.Lerp(ankle, foot.position, 0.6f) + Random.insideUnitSphere * size * 0.15f, size * 0.7f, mat);
    }

    void AddCube(Transform parent, Vector3 worldPos, float worldSize, Material mat)
    {
        GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cube);
        c.name = "IceCube";
        Destroy(c.GetComponent<Collider>());
        c.transform.SetParent(parent, true);
        c.transform.position = worldPos;
        c.transform.rotation = Random.rotation;
        Vector3 ps = parent.lossyScale;
        c.transform.localScale = new Vector3(worldSize / Mathf.Max(0.0001f, ps.x), worldSize / Mathf.Max(0.0001f, ps.y), worldSize / Mathf.Max(0.0001f, ps.z));
        MeshRenderer r = c.GetComponent<MeshRenderer>();
        if (mat != null) r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        cubes.Add(c);
    }

    // ---------------- frosty screen edges for the human player ----------------
    static Texture2D frostTex;

    void ShowFrost(bool on)
    {
        if (!on) { if (frost != null) Destroy(frost.gameObject); frost = null; return; }
        if (frost != null) return;
        PlayerHUD hud = GetComponent<PlayerHUD>();
        Canvas canvas = (hud != null && hud.bagIcon != null) ? hud.bagIcon.canvas.rootCanvas : FindFirstObjectByType<Canvas>();
        if (canvas == null) return;
        GameObject g = new GameObject("IceFrostOverlay", typeof(RectTransform), typeof(RawImage));
        g.transform.SetParent(canvas.transform, false);
        g.transform.SetAsFirstSibling(); // under the HUD cards
        RectTransform rt = g.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        frost = g.GetComponent<RawImage>();
        frost.raycastTarget = false;
        frost.texture = FrostTexture();
        frost.color = new Color(0.8f, 0.95f, 1f, 0f);
    }

    static Texture2D FrostTexture()
    {
        if (frostTex != null) return frostTex;
        const int n = 256;
        frostTex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        frostTex.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = x / (n - 1f) * 2f - 1f, v = y / (n - 1f) * 2f - 1f;
                float d = Mathf.Sqrt(u * u + v * v) / 1.414f;
                float crystals = Mathf.PerlinNoise(x * 0.09f, y * 0.09f) * 0.6f + Mathf.PerlinNoise(x * 0.35f, y * 0.35f) * 0.4f;
                float s = Mathf.InverseLerp(0.5f, 1f, d + (crystals - 0.5f) * 0.25f);
                float a = s * s * (3f - 2f * s); // clear in the middle, frosted towards the edges
                px[y * n + x] = new Color(0.85f + crystals * 0.15f, 0.95f, 1f, a);
            }
        frostTex.SetPixels(px);
        frostTex.Apply();
        return frostTex;
    }
}
