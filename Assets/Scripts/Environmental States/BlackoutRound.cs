using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Round 3: BLACKOUT. The power dies a moment after the round starts: every house light goes out and only a faint
// moonlight remains. A lamp only stutters back to life while the killer walks past it (that flicker is the only
// clue where he is), and now and then a lamp flickers on its own (a fake-out). This round the killer's footsteps
// are quieter, the player markers are hidden (until you are out and spectating), the AI players only know where
// he is from what they could notice themselves, fewer items lie around (no feather) and they glow faintly.
public class BlackoutRound : MonoBehaviour
{
    [Tooltip("Round this happens in (1..4).")]
    public int activeRound = 3;
    [Tooltip("Seconds after the round starts before the power dies.")]
    public float powerCutDelay = 3f;
    public float fadeOutTime = 1f;

    [Header("Alert")]
    public string alertTitle = "POWER OUTAGE";
    [TextArea] public string alertMessage = "The power is out. Watch the lights: they flicker when he walks past.";
    public AudioClip powerDownSound;
    [Range(0f, 1f)] public float powerDownVolume = 0.7f;

    [Header("Darkness")]
    [Tooltip("Faint moonlight left in the house.")]
    public Color moonlight = new Color(0.05f, 0.06f, 0.1f);
    [Range(0f, 1f)] public float reflections = 0.15f;
    [Tooltip("Light names (or their parents' names) that stay as they are, e.g. the fireplace and the TV.")]
    public string[] keepLightsNamed = { "Fire", "TV", "Spill", "InnerLight" };

    [Header("Flicker when the killer is near")]
    public float killerFlickerRange = 3f;
    [Tooltip("Brightest a flicker gets (1 = the lamp's normal brightness).")]
    [Range(0f, 2f)] public float flickerMaxBrightness = 1.3f;
    [Tooltip("Flickers per second for a lamp near him. Capped below 3 a second so it stays safe for people sensitive to flashing.")]
    [Range(0.5f, 2.4f)] public float flickersPerSecond = 1.8f;
    [Tooltip("How long one flicker stays lit, in seconds (min, max).")]
    public Vector2 flickerOnTime = new Vector2(0.15f, 0.3f);
    [Tooltip("Brightness of the first-person hands' own light during the blackout.")]
    public float handLightInDark = 0.06f;
    [Tooltip("Optional electric buzz played from a flickering lamp.")]
    public AudioClip buzzSound;
    [Range(0f, 1f)] public float buzzVolume = 0.35f;

    [Header("Fake-outs")]
    [Tooltip("Seconds between flickers with nobody near (min, max). 0 = none.")]
    public Vector2 fakeOutInterval = new Vector2(30f, 55f);

    [Header("Terror: glowing eyes")]
    public bool glowingEyes = true;
    public Color eyeColor = new Color(1f, 0.05f, 0.02f);
    [Tooltip("Which pixels of his texture glow (white = his pupils). Made from his own eye sockets.")]
    public Texture2D eyeGlowMask;
    [Tooltip("How bright his eyes glow.")]
    public float eyeGlowIntensity = 4f;

    [Header("Terror: panic")]
    [Tooltip("Dark red edges that pulse with your heartbeat as he gets close.")]
    public Color vignetteColor = new Color(0.35f, 0f, 0f);
    [Range(0f, 1f)] public float vignetteStrength = 0.75f;
    [Tooltip("Other sounds get muffled down to this (Hz) when he is right next to you. 22000 = off.")]
    public float muffleCutoff = 5000f;

    [Header("Terror: evil sounds")]
    [Tooltip("One of these plays at random (never the same one twice in a row), from where he really is.")]
    public AudioClip[] evilSounds;
    [Range(0f, 1f)] public float roarVolume = 0.5f;
    [Tooltip("Seconds between evil sounds (min, max).")]
    public Vector2 roarInterval = new Vector2(20f, 40f);
    public float roarHearDistance = 30f;

    [Header("Terror: the final hunt")]
    [Tooltip("Seconds left in the round when he starts hunting.")]
    public float huntSeconds = 30f;
    public string huntTitle = "HE'S HUNTING";
    [TextArea] public string huntMessage = "He knows you are still here. Don't stop moving.";
    [Tooltip("Added to his patrol speed while hunting.")]
    public float huntSpeedBonus = 0.6f;
    [Tooltip("Flickers per second are multiplied by this while hunting (still capped below 3 a second).")]
    public float huntFlickerBoost = 1.5f;
    [Tooltip("Optional low drone that fades in for the hunt.")]
    public AudioClip huntDrone;
    [Range(0f, 1f)] public float huntDroneVolume = 0.5f;

    [Header("This round")]
    [Range(0f, 1f)] public float killerFootstepVolume = 0.35f;
    public int itemsAtStart = 1;
    public int maxItemsOnMap = 2;
    [Tooltip("Items with one of these components never spawn this round.")]
    public string[] excludedItems = { "FeatherCollectible" };
    public Color itemGlowColor = new Color(1f, 0.85f, 0.55f);
    public float itemGlowIntensity = 0.8f;
    public float itemGlowRange = 1.5f;

    // ---------- what other scripts ask ----------
    static BlackoutRound instance;
    static bool powerOff;
    static Hider human;
    public static Vector3 LastFlickerPos { get; private set; }
    public static float LastFlickerTime { get; private set; }

    public static BlackoutRound Instance { get { return instance; } }
    // this scene's round is the blackout round (true from the start, before the power is cut)
    public static bool ThisRound { get { return instance != null && instance.isActiveAndEnabled && RoundManager.CurrentRound == instance.activeRound; } }
    // the lights are out right now
    public static bool Active { get { return powerOff && instance != null; } }
    public static float FootstepScale { get { return Active ? instance.killerFootstepVolume : 1f; } }
    // markers show normally, or during the blackout only once you are out and spectating
    public static bool MarkersAllowed
    {
        get
        {
            if (!Active) return true;
            if (human == null || !human.isHumanControlled)
            {
                human = null;
                foreach (Hider h in FindObjectsByType<Hider>(FindObjectsSortMode.None)) if (h.isHumanControlled) { human = h; break; }
            }
            return human != null && human.IsCaught;
        }
    }

    public static bool Excluded(GameObject prefab)
    {
        if (instance == null || prefab == null) return false;
        foreach (MonoBehaviour mb in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            if (mb != null) foreach (string n in instance.excludedItems) if (mb.GetType().Name == n) return true;
        return false;
    }

    // items carry their own little light: turn it down to a faint glow (or add one if there is none)
    public static void AddGlow(GameObject item)
    {
        if (instance == null || item == null) return;
        Light[] own = item.GetComponentsInChildren<Light>(true);
        if (own.Length > 0)
        {
            foreach (Light o in own)
            {
                o.intensity = Mathf.Min(o.intensity, instance.itemGlowIntensity);
                o.range = Mathf.Min(o.range, instance.itemGlowRange);
                o.shadows = LightShadows.None;
            }
            return;
        }
        GameObject g = new GameObject("ItemGlow");
        g.transform.SetParent(item.transform, false);
        g.transform.localPosition = Vector3.up * 0.3f;
        Light l = g.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = instance.itemGlowColor;
        l.intensity = instance.itemGlowIntensity;
        l.range = instance.itemGlowRange;
        l.shadows = LightShadows.None;
    }

    // ---------- the round ----------
    class Lamp { public Light light; public float intensity; public AudioSource buzz; public bool lit; public float fakeUntil; public float nextFlash, flashUntil, flashLevel; }
    readonly List<Lamp> lamps = new List<Lamp>();
    readonly List<Light> killerLights = new List<Light>();
    readonly List<Light> handLights = new List<Light>();
    readonly List<float> handStart = new List<float>();
    EnemyVision killer;
    float nextFake;

    AmbientMode oldMode; Color oldAmbient; float oldReflections; bool ambientChanged;

    void Awake() { instance = this; powerOff = false; human = null; LastFlickerTime = -999f; }

    void OnDestroy()
    {
        if (vignetteRoot != null) Destroy(vignetteRoot);
        if (muffle != null) Destroy(muffle);
        if (instance == this) { instance = null; powerOff = false; human = null; }
        if (ambientChanged) { RenderSettings.ambientMode = oldMode; RenderSettings.ambientLight = oldAmbient; RenderSettings.reflectionIntensity = oldReflections; }
    }

    void Start()
    {
        if (!ThisRound) { enabled = false; return; }
        StartCoroutine(CutPower());
    }

    IEnumerator CutPower()
    {
        yield return new WaitForSeconds(powerCutDelay);
        Begin();
    }

    void Begin()
    {
        if (powerOff || ambientChanged) return;
        killer = FindFirstObjectByType<EnemyVision>(FindObjectsInactive.Include);
        Bounds house = HouseBounds();
        foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional || !l.enabled || l.intensity <= 0f) continue;
            if (killer != null && l.transform.IsChildOf(killer.transform)) { killerLights.Add(l); continue; }
            if (l.name == "FPV Arms Light") { handLights.Add(l); handStart.Add(l.intensity); continue; } // the first-person hands' own light
            if (l.GetComponentInParent<Hider>() != null || l.name == "ItemGlow" || IsItem(l.transform)) continue;
            if (!house.Contains(l.transform.position) || Kept(l.transform)) continue;
            // a lamp that flickers on its own would give the game away: switch that script off
            FluorescentFlicker ff = l.GetComponentInParent<FluorescentFlicker>();
            if (ff != null) ff.enabled = false;
            Lamp lamp = new Lamp { light = l, intensity = l.intensity };
            if (buzzSound != null)
            {
                lamp.buzz = l.gameObject.AddComponent<AudioSource>();
                lamp.buzz.clip = buzzSound; lamp.buzz.loop = true; lamp.buzz.playOnAwake = false;
                lamp.buzz.spatialBlend = 1f; lamp.buzz.minDistance = 1.5f; lamp.buzz.maxDistance = 10f; lamp.buzz.volume = buzzVolume;
            }
            lamps.Add(lamp);
        }

        oldMode = RenderSettings.ambientMode; oldAmbient = RenderSettings.ambientLight; oldReflections = RenderSettings.reflectionIntensity;
        ambientChanged = true;

        HazardAlertUI.ShowSealed(alertTitle, alertMessage);
        if (powerDownSound != null) AudioSource.PlayClipAtPoint(powerDownSound, Camera.main != null ? Camera.main.transform.position : transform.position, powerDownVolume);
        StartCoroutine(FadeOut());
    }

    IEnumerator FadeOut()
    {
        Color startAmbient = RenderSettings.ambientMode == AmbientMode.Flat ? RenderSettings.ambientLight : RenderSettings.ambientSkyColor * RenderSettings.ambientIntensity;
        RenderSettings.ambientMode = AmbientMode.Flat;
        float t = 0f, time = Mathf.Max(0.05f, fadeOutTime);
        while (t < time)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / time);
            // a few dying stutters on the way down
            float stutter = (Random.value < 0.25f) ? 0.2f : 1f;
            foreach (Lamp l in lamps) if (l.light != null) l.light.intensity = l.intensity * (1f - k) * stutter;
            foreach (Light l in killerLights) if (l != null) l.intensity *= 0.85f;
            for (int i = 0; i < handLights.Count; i++) if (handLights[i] != null) handLights[i].intensity = Mathf.Lerp(handStart[i], handLightInDark, k);
            RenderSettings.ambientLight = Color.Lerp(startAmbient, moonlight, k);
            RenderSettings.reflectionIntensity = Mathf.Lerp(oldReflections, reflections, k);
            yield return null;
        }
        foreach (Lamp l in lamps) if (l.light != null) l.light.intensity = 0f;
        foreach (Light l in killerLights) if (l != null) l.enabled = false;
        RenderSettings.ambientLight = moonlight;
        RenderSettings.reflectionIntensity = reflections;
        powerOff = true;
        nextFake = Time.time + Random.Range(fakeOutInterval.x, fakeOutInterval.y);
        StartCoroutine(FlickerLoop());
        StartCoroutine(Terror());
    }

    // Every frame: a lamp near the killer stays almost dark and pops on in short flickers, a limited number per second
    // (never more than ~3 a second, the usual safe limit for flashing light); the rest stay dark.
    IEnumerator FlickerLoop()
    {
        while (powerOff)
        {
            bool killerOut = killer != null && killer.isActiveAndEnabled;
            Vector3 k = killerOut ? killer.transform.position : Vector3.zero;
            float now = Time.time;
            foreach (Lamp l in lamps)
            {
                if (l.light == null) continue;
                bool near = killerOut && Flat(l.light.transform.position, k) < killerFlickerRange;
                bool active = near || now < l.fakeUntil;
                float level = 0f;
                if (active)
                {
                    if (now >= l.nextFlash)
                    {
                        l.flashUntil = now + Random.Range(flickerOnTime.x, flickerOnTime.y);
                        l.flashLevel = flickerMaxBrightness * Random.Range(0.6f, 1f);
                        l.nextFlash = now + Random.Range(0.8f, 1.2f) / Mathf.Clamp(flickersPerSecond * (hunting ? huntFlickerBoost : 1f), 0.5f, 2.4f); // at least ~0.33 s apart
                    }
                    level = now < l.flashUntil ? l.flashLevel : 0.04f; // a faint glow between flickers
                    LastFlickerPos = l.light.transform.position; // fake-outs fool the AI players too
                    LastFlickerTime = now;
                }
                else l.nextFlash = now + Random.Range(0.05f, 0.3f); // first flicker comes quickly once he arrives
                l.light.intensity = l.intensity * level;
                SetBuzz(l, active);
            }
            if (fakeOutInterval.y > 0f && now >= nextFake && lamps.Count > 0)
            {
                nextFake = now + Random.Range(fakeOutInterval.x, fakeOutInterval.y);
                Lamp f = lamps[Random.Range(0, lamps.Count)];
                // never right next to him, that would not be a fake
                if (f.light != null && !(killerOut && Flat(f.light.transform.position, k) < killerFlickerRange * 2f))
                    f.fakeUntil = now + Random.Range(1.5f, 2.5f);
            }
            yield return null;
        }
    }

    // ---------- terror ----------
    bool hunting;
    GameObject vignetteRoot;
    UnityEngine.UI.RawImage vignette;
    AudioLowPassFilter muffle;

    IEnumerator Terror()
    {
        if (glowingEyes) MakeEyes();
        Hider me = null;
        HeartbeatMonitor heart = null;
        foreach (Hider h in FindObjectsByType<Hider>(FindObjectsSortMode.None))
            if (h.isHumanControlled) { me = h; heart = h.GetComponent<HeartbeatMonitor>(); break; }
        BuildVignette();
        AudioListener listener = FindFirstObjectByType<AudioListener>();
        if (listener != null && muffleCutoff < 21000f)
        {
            muffle = listener.GetComponent<AudioLowPassFilter>();
            if (muffle == null) muffle = listener.gameObject.AddComponent<AudioLowPassFilter>();
            muffle.cutoffFrequency = 22000f;
        }
        float nextRoar = Time.time + Random.Range(roarInterval.x, roarInterval.y);

        while (powerOff)
        {
            bool dead = me == null || me.IsCaught;
            float level = (!dead && heart != null && heart.isActiveAndEnabled) ? heart.Level : 0f;
            float beat = heart != null ? heart.Pulse : 0f;

            // panic: red edges that throb with the heartbeat, and the world muffles around you
            float a = Mathf.Clamp01(level * 1.3f) * vignetteStrength * (0.6f + 0.4f * beat);
            Color c = vignette.color;
            c.a = Mathf.MoveTowards(c.a, a, Time.deltaTime * 2.5f);
            vignette.color = c;
            if (muffle != null) muffle.cutoffFrequency = Mathf.Lerp(22000f, muffleCutoff, level);

            // the last seconds: he hunts
            RoundManager rm = RoundManager.Instance;
            if (!hunting && rm != null && !rm.RoundOver && rm.TimeLeft <= huntSeconds) { StartHunt(); nextRoar = Time.time + Random.Range(roarInterval.x, roarInterval.y) * 0.5f; }

            if (Time.time >= nextRoar)
            {
                Roar();
                nextRoar = Time.time + Random.Range(roarInterval.x, roarInterval.y) * (hunting ? 0.5f : 1f);
            }
            yield return null;
        }
    }

    void StartHunt()
    {
        hunting = true;
        HazardAlertUI.ShowSealed(huntTitle, huntMessage);
        if (killer != null) killer.patrolSpeed += huntSpeedBonus;
        Roar();
        if (huntDrone != null)
        {
            AudioSource d = gameObject.AddComponent<AudioSource>();
            d.clip = huntDrone; d.loop = true; d.spatialBlend = 0f; d.volume = 0f;
            d.Play();
            StartCoroutine(FadeTo(d, huntDroneVolume, 4f));
        }
    }

    IEnumerator FadeTo(AudioSource s, float to, float time)
    {
        float from = s.volume, t = 0f;
        while (t < time && s != null) { t += Time.deltaTime; s.volume = Mathf.Lerp(from, to, t / time); yield return null; }
    }

    // a roar from where he really is: you can tell roughly where he is, and so can the AI players
    int lastEvil = -1;
    void Roar()
    {
        if (evilSounds == null || evilSounds.Length == 0 || killer == null || !killer.gameObject.activeInHierarchy) return;
        int pick = Random.Range(0, evilSounds.Length);
        if (evilSounds.Length > 1 && pick == lastEvil) pick = (pick + 1 + Random.Range(0, evilSounds.Length - 1)) % evilSounds.Length;
        AudioClip clip = evilSounds[pick];
        if (clip == null) return;
        lastEvil = pick;
        GameObject g = new GameObject("KillerRoar");
        g.transform.SetParent(killer.transform, false);
        g.transform.localPosition = Vector3.up * 1.5f;
        AudioSource s = g.AddComponent<AudioSource>();
        s.clip = clip; s.volume = roarVolume; s.spatialBlend = 1f;
        s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = 3f; s.maxDistance = roarHearDistance;
        s.Play();
        Destroy(g, clip.length + 0.2f);
        LastFlickerPos = killer.transform.position;
        LastFlickerTime = Time.time;
    }

    // his own eyes glow red: his material gets a red emission only where his pupils are (eyeGlowMask)
    void MakeEyes()
    {
        if (killer == null || eyeGlowMask == null) return;
        foreach (Renderer r in killer.GetComponentsInChildren<Renderer>(true))
        {
            if (!(r is SkinnedMeshRenderer)) continue;
            Material m = r.material; // this round only: a copy, the asset is untouched
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetTexture("_EmissionMap", eyeGlowMask);
            m.SetColor("_EmissionColor", eyeColor * eyeGlowIntensity);
        }
    }

    void BuildVignette()
    {
        vignetteRoot = new GameObject("PanicVignette");
        Canvas c = vignetteRoot.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = -10; // under the HUD
        vignette = new GameObject("Edges").AddComponent<UnityEngine.UI.RawImage>();
        vignette.transform.SetParent(vignetteRoot.transform, false);
        vignette.raycastTarget = false;
        RectTransform rt = vignette.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        const int n = 128;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color32[] px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / 0.5f; // 0 centre, 1 edge middle, 1.41 corner
                float alpha = Mathf.Pow(Mathf.Clamp01((r - 0.55f) / 0.7f), 1.4f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        vignette.texture = tex;
        vignette.color = new Color(vignetteColor.r, vignetteColor.g, vignetteColor.b, 0f);
    }

    void SetBuzz(Lamp l, bool on)
    {
        if (l.buzz == null || l.lit == on) return;
        l.lit = on;
        if (on) l.buzz.Play(); else l.buzz.Stop();
    }

    // a collectible / instant lying around (its light belongs to the item, not the house)
    static bool IsItem(Transform t)
    {
        for (; t != null; t = t.parent)
        {
            if (t.CompareTag("CollectibleStone")) return true;
            foreach (MonoBehaviour mb in t.GetComponents<MonoBehaviour>())
                if (mb != null && mb.GetType().Name.EndsWith("Collectible")) return true;
        }
        return false;
    }

    bool Kept(Transform t)
    {
        for (; t != null; t = t.parent)
            foreach (string n in keepLightsNamed) if (!string.IsNullOrEmpty(n) && t.name.Contains(n)) return true;
        return false;
    }

    // the house: everything on the Wall / Floor layers, a little padded
    static Bounds HouseBounds()
    {
        Bounds b = new Bounds();
        bool init = false;
        int wall = LayerMask.NameToLayer("Wall"), floor = LayerMask.NameToLayer("Floor");
        foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            int l = r.gameObject.layer;
            if (l != wall && l != floor) continue;
            if (!init) { b = r.bounds; init = true; } else b.Encapsulate(r.bounds);
        }
        b.Expand(new Vector3(1f, 3f, 1f));
        return b;
    }

    static float Flat(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }
}
