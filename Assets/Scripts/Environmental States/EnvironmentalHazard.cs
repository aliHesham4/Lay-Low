using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// One room that can 'malfunction' (environmental state). When it starts:
//   1. an alert pops up for everyone (bottom-left) and counts down warningSeconds,
//   2. then the effect fills the room, every doorway into it slams shut for the rest of the round,
//      and anyone still inside (players or AI) dies. If the killer is in the room when the countdown ends, the alarm
//      keeps going and the room only seals once he has walked out (no teleporting him). Nobody can enter after.
// EnvironmentalStateManager decides when (round 2) and which rooms.
public class EnvironmentalHazard : MonoBehaviour
{
    public enum Phase { Idle, Warning, Sealed }
    public enum LightWarning { RedAlarm, Flicker, None }

    [Header("Alert")]
    public string roomName = "Bathroom";
    public string alertTitle = "SEWAGE LEAK";
    [TextArea] public string alertMessage = "Toxic fumes are leaking from the bathroom sewage.\nGet out before it is sealed!";
    [TextArea] public string sealedMessage = "The bathroom is sealed. The air inside is poisonous.";
    public float warningSeconds = 5f;

    [Header("Room")]
    [Tooltip("Trigger box covering the whole room. Anyone inside it when the room seals dies.")]
    public BoxCollider area;
    [Tooltip("More boxes that belong to the same hazard (e.g. a second terrace). Effects play in each.")]
    public BoxCollider[] extraAreas;

    [Header("Open areas (no doors)")]
    [Tooltip("Anyone who walks in while it is active dies (not only those inside when it starts).")]
    public bool deadlyWhileActive = false;
    [Tooltip("NavMesh area painted over these boxes. While active the killer can't walk there; AI players avoid it except by mistake.")]
    public string navArea = "";
    [Range(0f, 1f)] [Tooltip("Chance per AI player that it doesn't notice and may still wander in.")]
    public float aiMistakeChance = 0.2f;
    readonly HashSet<Component> careless = new HashSet<Component>();

    [Header("Lightning (thunderstorm)")]
    [Tooltip("Anyone killed by this hazard is struck by a lightning bolt, and random bolts hit the area while it's active.")]
    public bool lightningStrikes = false;
    public Color boltColor = new Color(0.75f, 0.85f, 1f, 1f);
    [Tooltip("Spawned where a bolt lands (e.g. CFXR3 Hit Electric C).")]
    public GameObject strikeEffectPrefab;
    [Tooltip("Extra flash where a bolt lands (e.g. CFXR Flash).")]
    public GameObject strikeFlashPrefab;
    public AudioClip strikeSound;
    [Range(0f, 1f)] public float strikeSoundVolume = 0.8f;
    [Tooltip("Seconds between random bolts on the area (min, max). 0 = no random bolts.")]
    public Vector2 ambientStrikeInterval = new Vector2(2.5f, 6f);
    public float boltHeight = 9f;
    static Material boltMaterial;

    [Header("Background effect (visual only)")]
    [Tooltip("Boxes (collider can stay disabled) where the main effect also plays just for looks: nobody dies there, no lightning.")]
    public BoxCollider[] backgroundAreas;
    [Tooltip("Particles per second per square metre in the background boxes.")]
    public float backgroundDensity = 6f;
    [Tooltip("Height above the box bottom where the background effect starts.")]
    public float backgroundHeight = 6f;


    [Tooltip("Every doorway into the room: shut for the rest of the round.")]
    public LockableDoorway[] doorways;
    [Tooltip("Objects hidden until the room seals, then dropped from above (e.g. the cones around the fireplace).")]
    public Transform[] dropObjects;
    [Tooltip("Invisible walls switched on when the room seals: nobody (players, AI, killer) can pass them.")]
    public GameObject[] barriers;
    public float dropHeight = 2.2f;
    public Color shutterColor = new Color(0.45f, 0.9f, 0.2f);

    [Header("During the alert")]
    [Tooltip("Optional effect played during the 5 s alert (e.g. Sparks Rain from the lamp).")]
    public GameObject warningEffectPrefab;
    [Tooltip("Where the alert effect plays (e.g. the lamp). Defaults to the first room light.")]
    public Transform warningEffectPoint;
    [Range(0.1f, 2f)] [Tooltip("Size of the alert effect (1 = as made). It points along the point's blue (Z) axis.")]
    public float warningEffectScale = 1f;
    [Range(0f, 1f)] [Tooltip("Opacity of the alert effect (e.g. the fireplace smoke). 1 = as made.")]
    public float warningEffectOpacity = 1f;

    [Header("Effect")]
    [Tooltip("Particle effect that fills the room (e.g. CFXR2 Poison Cloud). Several are spread over the room and keep looping.")]
    public GameObject effectPrefab;
    public int effectCount = 3;
    [Tooltip("Columns x rows to spread the effect over the area (0 = in a line along the longer side, using Effect Count).")]
    public Vector2Int effectGrid = Vector2Int.zero;
    [Tooltip("Keep the effect going until the round ends (e.g. the fireplace fire).")]
    public bool effectLastsAllRound = false;
    [Tooltip("The effect starts this many times bigger, then settles to its normal size (1 = no burst).")]
    public float effectStartBoost = 1f;
    public float effectSettleTime = 3f;
    [Tooltip("Overall size the effect settles to after the burst (1 = as made). Scales the whole thing, flame height included.")]
    public float effectSettledSize = 1f;
    [Tooltip("Seconds the effect burns at full strength before it calms down.")]
    public float effectHoldTime = 0f;
    [Range(0.1f, 1f)] [Tooltip("Height of the fire after it calms down (1 = unchanged).")]
    public float effectSettledHeight = 1f;
    [Range(0.05f, 1f)] [Tooltip("How much fire it keeps spawning after it calms down (1 = unchanged).")]
    public float effectSettledRate = 0.8f;
    [Tooltip("Turn each effect a random way (off for wall-shaped effects like a firewall).")]
    public bool effectRandomRotation = true;
    [Tooltip("One effect per box, stretched to cover it (e.g. rain): its emitter shape is resized to the box. Effect Height = how high above the floor it starts.")]
    public bool effectFitToArea = false;
    [Tooltip("With Fit To Area: particles per second per square metre.")]
    public float effectDensity = 6f;

    [Header("Extra flames (variety)")]
    [Tooltip("Optional smaller effects scattered over the area on top of the main one (e.g. CFXR Fire), each a different size and out of step.")]
    public GameObject extraEffectPrefab;
    public int extraEffectCount = 0;
    [Tooltip("Optional: place flames by hand. Each Transform here is one flame: its position is where it burns and its Scale X sizes it (1 = normal). When this list has entries, the random flames are not used. They light up in order of distance from Spill Origin, nearest first.")]
    public Transform[] fireSpots;
    [Tooltip("Random size range of the extra flames.")]
    public Vector2 extraEffectSize = new Vector2(0.5f, 1f);
    [Tooltip("Each extra flame starts after a random delay in this range (seconds after the seal).")]
    public Vector2 extraEffectDelay = new Vector2(0f, 2.5f);
    [Range(0f, 1f)] [Tooltip("Share of the extra flames that give off smoke.")]
    public float extraSmokeChance = 0.25f;
    [Tooltip("Switch off flying embers / sparks of the effect (keeps it tidy and inside the area).")]
    public bool effectNoEmbers = false;
    [Range(0.1f, 1f)] [Tooltip("Height of the flames compared to their width (1 = as made, lower = flatter, shorter fire).")]
    public float effectHeightScale = 1f;
    [Range(0f, 1f)] [Tooltip("Share of the effects that also give off smoke (1 = all of them).")]
    public float smokeChance = 1f;
    [Range(0f, 1f)] [Tooltip("Random offset of each effect inside its grid cell (0 = neat grid, 1 = anywhere in the cell).")]
    public float effectPositionJitter = 0f;
    [Range(0f, 1f)] [Tooltip("Random size difference between the effects (0 = all the same).")]
    public float effectSizeVariation = 0f;
    [Tooltip("Colour for the flames (alpha 0 = keep the effect's own colour).")]
    public Color effectTint = new Color(0f, 0f, 0f, 0f);
    [Tooltip("Colour for the smoke parts (alpha 0 = keep).")]
    public Color smokeTint = new Color(0f, 0f, 0f, 0f);
    [Tooltip("On = the effect keeps going (poison cloud). Off = it plays once (explosion).")]
    public bool loopEffect = true;
    [Tooltip("Height of the effect above the floor of the box.")]
    public float effectHeight = 0.3f;
    [Range(0.05f, 1f)] [Tooltip("Shrinks the cloud (speed and size) so it stays inside the room.")]
    public float effectScale = 0.3f;
    [Tooltip("Seconds the poison keeps pouring out after the room seals, then it fades away.")]
    public float effectDuration = 8f;
    [Tooltip("Optional effect shown on each victim (e.g. CFXR4 _POISONED_).")]
    public GameObject victimEffectPrefab;
    public AudioClip sealSound;
    [Tooltip("Loops from the room during the alert (e.g. a security alarm), then stops when it seals.")]
    public AudioClip warningSound;
    [Range(0f, 1f)] public float warningSoundVolume = 0.35f;
    [Tooltip("Played once the moment the room seals / ignites (e.g. a whoosh), on top of the Seal Sound. Started early so its loudest moment lands exactly on the seal.")]
    public AudioClip igniteSound;
    [Range(0f, 1f)] public float igniteSoundVolume = 1f;
    [Tooltip("Keep the seal sound looping until the round ends (e.g. fire crackle). Off = it plays once.")]
    public bool loopSealSound = false;
    [Range(0f, 1f)] public float sealSoundVolume = 1f;

    [Header("Room lights")]
    [Tooltip("Lights of this room (found automatically inside the box if left empty): they flash red during the alert, then fade to black.")]
    public Light[] roomLights;
    [Tooltip("RedAlarm: lights flash red. Flicker: same colour, twitching hard (overheating lamp).")]
    public LightWarning lightWarning = LightWarning.RedAlarm;
    public Color alarmColor = new Color(1f, 0.08f, 0.05f);
    public float flashInterval = 0.25f;
    public float fadeToBlackTime = 3f;

    [Header("Darkness after sealing")]
    [Range(0f, 1f)] [Tooltip("How dark the room looks once sealed (0 = off, 1 = pitch black), whatever other lights reach it.")]
    public float darkenAfterSeal = 0f;
    [Tooltip("Black see-through material for the darkness (RoomDarkness).")]
    public Material darknessMaterial;
    [Tooltip("How high the darkness reaches from the floor (about the wall height).")]
    public float darknessHeight = 1.6f;
    [Tooltip("Only darken the floor (a dark layer under the effect), e.g. a burning area where the flames must stay bright.")]
    public bool darkenFloorOnly = false;

    Color[] lightColors;
    float[] lightIntensities;
    readonly List<GameObject> clouds = new List<GameObject>();

    public Phase State { get; private set; }

    static readonly List<EnvironmentalHazard> all = new List<EnvironmentalHazard>();
    public static List<EnvironmentalHazard> All { get { return all; } }

    void OnEnable() { if (!all.Contains(this)) all.Add(this); }
    void OnDisable() { all.Remove(this); }

    void Awake()
    {
        if (area == null) area = GetComponent<BoxCollider>();
        if (area != null) area.isTrigger = true;
        // cones etc. stay hidden until they drop; the barriers only exist once sealed
        if (dropObjects != null) foreach (Transform d in dropObjects) if (d != null) d.gameObject.SetActive(false);
        if (barriers != null) foreach (GameObject g in barriers) if (g != null) g.SetActive(false);

        if ((roomLights == null || roomLights.Length == 0) && area != null)
        {
            var found = new List<Light>();
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type != LightType.Directional && Contains(l.transform.position)) found.Add(l);
            roomLights = found.ToArray();
        }
        lightColors = new Color[roomLights.Length];
        lightIntensities = new float[roomLights.Length];
        for (int i = 0; i < roomLights.Length; i++)
            if (roomLights[i] != null) { lightColors[i] = roomLights[i].color; lightIntensities[i] = roomLights[i].intensity; }
    }

    // ---------- queries used by the AI / spawner ----------

    public bool Contains(Vector3 p)
    {
        if (InBox(area, p)) return true;
        if (extraAreas != null) foreach (BoxCollider b in extraAreas) if (InBox(b, p)) return true;
        return false;
    }

    static bool InBox(BoxCollider box, Vector3 p)
    {
        if (box == null) return false;
        Vector3 local = box.transform.InverseTransformPoint(p) - box.center;
        Vector3 half = box.size * 0.5f;
        return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.z) <= half.z && Mathf.Abs(local.y) <= half.y + 1f;
    }

    BoxCollider BoxAt(Vector3 p)
    {
        if (InBox(area, p)) return area;
        if (extraAreas != null) foreach (BoxCollider b in extraAreas) if (InBox(b, p)) return b;
        return area;
    }

    IEnumerable<BoxCollider> Boxes()
    {
        if (area != null) yield return area;
        if (extraAreas != null) foreach (BoxCollider b in extraAreas) if (b != null) yield return b;
    }

    // a room that is about to seal or already sealed
    public static bool IsDangerous(Vector3 p)
    {
        foreach (EnvironmentalHazard h in all) if (h != null && h.State != Phase.Idle && h.Contains(p)) return true;
        return false;
    }

    // the same, for one particular AI player: one who 'didn't notice' this hazard doesn't treat it as dangerous
    public static bool IsDangerous(Vector3 p, Component who)
    {
        foreach (EnvironmentalHazard h in all)
            if (h != null && h.State != Phase.Idle && h.Contains(p) && !(who != null && h.careless.Contains(who))) return true;
        return false;
    }

    public static EnvironmentalHazard DangerAt(Vector3 p)
    {
        foreach (EnvironmentalHazard h in all) if (h != null && h.State != Phase.Idle && h.Contains(p)) return h;
        return null;
    }

    // nearest walkable point just outside this room
    public bool ExitPoint(Vector3 from, out Vector3 exit)
    {
        exit = from;
        float best = float.MaxValue;
        bool found = false;
        NavMeshPath path = new NavMeshPath();
        if (doorways != null)
            foreach (LockableDoorway d in doorways)
            {
                if (d == null) continue;
                // the two sides of the doorway: take the one outside the room
                foreach (float side in new[] { 1f, -1f })
                {
                    Vector3 p = d.transform.position + d.transform.forward * side * 1.2f;
                    if (Contains(p)) continue;
                    NavMeshHit hit;
                    if (!NavMesh.SamplePosition(p, out hit, 1f, NavMesh.AllAreas)) continue;
                    if (!NavMesh.CalculatePath(from, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                    float len = 0f; for (int i = 1; i < path.corners.Length; i++) len += Vector3.Distance(path.corners[i - 1], path.corners[i]);
                    if (len < best) { best = len; exit = hit.position; found = true; }
                }
            }
        if (!found && (doorways == null || doorways.Length == 0) && area != null)
        {
            // no doors (an open area): just outside the middle of each side of the box
            Bounds bb = BoxAt(from).bounds;
            Vector3[] sides = { new Vector3(bb.min.x - 1f, bb.center.y, bb.center.z), new Vector3(bb.max.x + 1f, bb.center.y, bb.center.z),
                                new Vector3(bb.center.x, bb.center.y, bb.min.z - 1f), new Vector3(bb.center.x, bb.center.y, bb.max.z + 1f) };
            foreach (Vector3 s in sides)
            {
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(s, out hit, 1.5f, NavMesh.AllAreas) || Contains(hit.position)) continue;
                if (!NavMesh.CalculatePath(from, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                float len = 0f; for (int i = 1; i < path.corners.Length; i++) len += Vector3.Distance(path.corners[i - 1], path.corners[i]);
                if (len < best) { best = len; exit = hit.position; found = true; }
            }
        }
        return found;
    }

    // the burst: full blaze for a few seconds, then it calms down to a lower, thinner fire that keeps burning
    IEnumerator Settle(Transform fx)
    {
        if (fx == null) yield break;
        ParticleSystem[] systems = fx.GetComponentsInChildren<ParticleSystem>(true);
        float[] rates = new float[systems.Length];
        for (int i = 0; i < systems.Length; i++) rates[i] = systems[i].emission.rateOverTimeMultiplier;

        Vector3 start = Vector3.one * effectStartBoost * effectSettledSize;
        Vector3 end = new Vector3(effectSettledSize, effectSettledSize * effectSettledHeight, effectSettledSize);
        fx.localScale = start;
        yield return new WaitForSeconds(effectHoldTime);

        float t = 0f, time = Mathf.Max(0.1f, effectSettleTime);
        while (t < time && fx != null)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / time);
            fx.localScale = Vector3.Lerp(start, end, k);
            if (effectLastsAllRound)
                for (int i = 0; i < systems.Length; i++)
                {
                    if (systems[i] == null) continue;
                    var em = systems[i].emission;
                    float target = systems[i].name.Contains("Smoke") ? 0.08f : effectSettledRate;
                    em.rateOverTimeMultiplier = rates[i] * Mathf.Lerp(1f, target, k);
                }
            yield return null;
        }
        if (fx != null) fx.localScale = end;
    }

    AudioSource PlayWarningSound()
    {
        if (warningSound == null || area == null) return null;
        GameObject s = new GameObject("HazardAlarm");
        s.transform.SetParent(transform, false);
        s.transform.position = area.bounds.center;
        AudioSource src = s.AddComponent<AudioSource>();
        src.clip = warningSound;
        src.loop = true;
        src.spatialBlend = 1f;               // it comes from the room
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 3f;
        src.maxDistance = 22f;               // heard across the house, clearly louder near the room
        src.volume = warningSoundVolume;
        src.Play();
        return src;
    }

    void PlayIgnite()
    {
        if (igniteSound != null && area != null) AudioSource.PlayClipAtPoint(igniteSound, area.bounds.center, igniteSoundVolume);
    }

    IEnumerator FadeOutAndStop(AudioSource src, float time)
    {
        float from = src.volume, t = 0f;
        while (t < time && src != null) { t += Time.deltaTime; src.volume = Mathf.Lerp(from, 0f, t / time); yield return null; }
        if (src != null) Destroy(src.gameObject);
    }

    // where the clip is loudest (seconds from its start), so it can be lined up with the seal
    static readonly Dictionary<AudioClip, float> peaks = new Dictionary<AudioClip, float>();
    static float PeakTime(AudioClip c)
    {
        float p;
        if (peaks.TryGetValue(c, out p)) return p;
        p = 0f;
        try
        {
            if (c.loadState != AudioDataLoadState.Loaded) c.LoadAudioData();
            float[] d = new float[c.samples * c.channels];
            if (c.GetData(d, 0))
            {
                float max = 0f;
                for (int i = 0; i < c.samples; i += 64)
                {
                    float v = Mathf.Abs(d[i * c.channels]);
                    if (v > max) { max = v; p = i / (float)c.frequency; }
                }
            }
        }
        catch { p = 0f; }
        peaks[c] = p;
        return p;
    }

    IEnumerator FadeIn(AudioSource src, float to, float time)
    {
        float t = 0f;
        while (t < time && src != null) { t += Time.deltaTime; src.volume = Mathf.Lerp(0f, to, t / time); yield return null; }
        if (src != null) src.volume = to;
    }

    // scattered flames on top of the main effect: random spots, sizes and start times so nothing pulses together
    [Header("Fire spilling out of an opening")]
    [Tooltip("Optional: the opening the fire comes out of (its blue Z arrow points into the room). The main fire sits here and the extra flames fan out onto the floor in front of it.")]
    public Transform spillOrigin;
    [Tooltip("How far into the room the flames reach (metres).")]
    public float spillLength = 2.2f;
    [Tooltip("How wide the fan of flames opens (degrees).")]
    public float spillAngle = 80f;
    [Tooltip("Layers that count as floor: flames only land there, never on furniture.")]
    public LayerMask spillFloorMask = 1 << 9;

    // furniture standing in the room (couch, tables...), even pieces without a collider: flames keep off their footprint
    List<Bounds> furniture;
    bool OnFurniture(Vector3 p)
    {
        if (furniture == null)
        {
            furniture = new List<Bounds>();
            Bounds room = area.bounds;
            room.Expand(new Vector3(0.5f, 2f, 0.5f));
            int wall = LayerMask.NameToLayer("Wall"), floor = LayerMask.NameToLayer("Floor");
            foreach (MeshRenderer r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || r.gameObject.layer == wall || r.gameObject.layer == floor) continue;
                if (r.transform.IsChildOf(transform) || r.GetComponentInParent<Hider>() != null) continue;
                Bounds b = r.bounds;
                if (!room.Intersects(b) || b.size.y < 0.25f || b.min.y > room.min.y + 2.6f) continue; // flat rugs are fine
                if (b.size.x > 4f || b.size.z > 4f) continue; // whole-room pieces
                furniture.Add(b);
            }
        }
        foreach (Bounds b in furniture)
            if (p.x > b.min.x - 0.1f && p.x < b.max.x + 0.1f && p.z > b.min.z - 0.1f && p.z < b.max.z + 0.1f) return true;
        return false;
    }

    // a spot on the floor in front of the opening for flame i; size shrinks with distance
    bool SpillPoint(int i, out Vector3 p, out float size)
    {
        p = Vector3.zero; size = 1f;
        Vector3 fwd = spillOrigin.forward; fwd.y = 0f; fwd.Normalize();
        for (int attempt = 0; attempt < 8; attempt++)
        {
            // spread the flames evenly over the distance, nearest ones first
            float k = Mathf.Clamp01((i + Random.Range(0.1f, 0.9f)) / Mathf.Max(1, extraEffectCount));
            float dist = Mathf.Lerp(0.25f, spillLength, k);
            float half = spillAngle * 0.5f * Mathf.Lerp(0.5f, 1f, k); // narrow at the mouth, wider further out
            Vector3 dir = Quaternion.Euler(0f, Random.Range(-half, half), 0f) * fwd;
            Vector3 top = spillOrigin.position + dir * dist + Vector3.up * 2.5f;
            RaycastHit hit;
            if (!Physics.Raycast(top, Vector3.down, out hit, 4f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (((1 << hit.collider.gameObject.layer) & spillFloorMask.value) == 0) continue; // furniture: try another spot
            if (!Contains(hit.point) || OnFurniture(hit.point)) continue;
            p = hit.point + Vector3.up * 0.02f;
            size = Mathf.Lerp(1f, 0.45f, k);
            return true;
        }
        return false;
    }

    IEnumerator SpawnExtraEffects()
    {
        if (area == null) yield break;
        Bounds b = area.bounds;
        var order = new List<float>();
        Transform[] manual = null;
        if (fireSpots != null)
        {
            var list = new List<Transform>();
            foreach (Transform s in fireSpots) if (s != null) list.Add(s);
            if (list.Count > 0)
            {
                Vector3 from = spillOrigin != null ? spillOrigin.position : transform.position;
                list.Sort((x, y) => (x.position - from).sqrMagnitude.CompareTo((y.position - from).sqrMagnitude));
                manual = list.ToArray();
            }
        }
        int total = manual != null ? manual.Length : extraEffectCount;
        for (int i = 0; i < total; i++) order.Add(Random.Range(extraEffectDelay.x, extraEffectDelay.y));
        order.Sort();
        float waited = 0f;
        for (int i = 0; i < total; i++)
        {
            if (order[i] > waited) { yield return new WaitForSeconds(order[i] - waited); waited = order[i]; }
            Vector3 p;
            float spillSize = 1f;
            if (manual != null)
            {
                p = manual[i].position;
                spillSize = manual[i].localScale.x;
            }
            else if (spillOrigin != null)
            {
                // fanning out of the opening onto the floor, smaller the further out
                if (!SpillPoint(i, out p, out spillSize)) continue;
            }
            else
            {
                // spread out: each flame gets its own slice of the area, then a random spot inside it
                float u = Mathf.Lerp(0.15f, 0.85f, (i + Random.Range(0.1f, 0.9f)) / extraEffectCount);
                float v = Random.Range(0.18f, 0.82f);
                bool alongX = b.size.x >= b.size.z;
                p = new Vector3(Mathf.Lerp(b.min.x, b.max.x, alongX ? u : v), b.min.y + effectHeight, Mathf.Lerp(b.min.z, b.max.z, alongX ? v : u));
            }
            GameObject fx = Instantiate(extraEffectPrefab, p, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
            clouds.Add(fx);
            fx.transform.localScale = Vector3.one * Random.Range(extraEffectSize.x, extraEffectSize.y) * spillSize;
            foreach (MonoBehaviour mb in fx.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb.GetType().Name == "CFXR_Effect") Destroy(mb);
            bool smoke = Random.value < extraSmokeChance;
            float speed = Random.Range(0.85f, 1.15f);
            foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps.name.Contains("Smoke") && !smoke) { ps.gameObject.SetActive(false); continue; }
                var main = ps.main;
                main.loop = true;
                main.stopAction = ParticleSystemStopAction.None;
                main.simulationSpeed = speed; // slightly different rhythm per flame
                if (effectTint.a > 0f && !ps.name.Contains("Smoke")) { Color c = effectTint; c.a = main.startColor.color.a; main.startColor = c; }
                if (ps.name.Contains("Smoke") && smokeTint.a > 0f) { Color c = smokeTint; c.a = main.startColor.color.a * 0.6f; main.startColor = c; var col = ps.colorOverLifetime; col.enabled = false; }
                ps.Play();
            }
        }
    }

    // the cones fall into place from above with a little bounce
    IEnumerator DropObjects()
    {
        var targets = new List<Vector3>();
        foreach (Transform d in dropObjects)
        {
            targets.Add(d != null ? d.position : Vector3.zero);
            if (d == null) continue;
            d.position += Vector3.up * dropHeight;
            d.gameObject.SetActive(true);
        }
        float t = 0f, fall = Mathf.Sqrt(2f * dropHeight / 9.81f);
        while (t < fall + 0.25f)
        {
            t += Time.deltaTime;
            for (int i = 0; i < dropObjects.Length; i++)
            {
                Transform d = dropObjects[i];
                if (d == null) continue;
                float k = t - i * 0.04f; // slightly one after another
                float y;
                if (k < fall) y = dropHeight - 0.5f * 9.81f * Mathf.Max(0f, k) * Mathf.Max(0f, k);
                else { float bt = (k - fall) / 0.25f; y = bt < 1f ? Mathf.Sin(bt * Mathf.PI) * 0.06f : 0f; }
                d.position = targets[i] + Vector3.up * Mathf.Max(0f, y);
            }
            yield return null;
        }
        for (int i = 0; i < dropObjects.Length; i++) if (dropObjects[i] != null) dropObjects[i].position = targets[i];
    }

    // ---------- running ----------

    public void Begin()
    {
        if (State != Phase.Idle) return;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        State = Phase.Warning;
        HazardAlertUI.ShowWarning(alertTitle, alertMessage, warningSeconds);
        SwitchOffAppliancesInside();

        // the room's lights: red alarm flashes, or a hard flicker (overheating lamp)
        GameObject warnFx = null;
        if (warningEffectPrefab != null)
        {
            Transform at = warningEffectPoint != null ? warningEffectPoint : (roomLights != null && roomLights.Length > 0 && roomLights[0] != null ? roomLights[0].transform : transform);
            warnFx = Instantiate(warningEffectPrefab, at.position, warningEffectPoint != null ? warningEffectPoint.rotation : Quaternion.identity, transform);
            KeepGoing(warnFx);
            if (warningEffectOpacity < 1f)
                foreach (ParticleSystem ps in warnFx.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    Color c = main.startColor.color; c.a *= warningEffectOpacity; main.startColor = c;
                }
            if (!Mathf.Approximately(warningEffectScale, 1f))
                foreach (ParticleSystem ps in warnFx.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    main.startSpeedMultiplier *= warningEffectScale;
                    main.startSizeMultiplier *= warningEffectScale;
                }
        }
        AudioSource alarm = PlayWarningSound();
        float lead = igniteSound != null ? PeakTime(igniteSound) : 0f;
        bool igniteStarted = false;
        float end = Time.time + warningSeconds;
        bool on = true;
        while (Time.time < end)
        {
            // the killer wandered in: hold the countdown just short of the end (alarm keeps going) until he walks out
            if (!igniteStarted && KillerInside() && end - Time.time < lead + 0.3f) end = Time.time + lead + 0.3f;
            if (!igniteStarted && igniteSound != null && Time.time >= end - lead) { igniteStarted = true; PlayIgnite(); }
            if (lightWarning == LightWarning.None)
            {
                yield return null;
            }
            else if (lightWarning == LightWarning.RedAlarm)
            {
                SetLights(on ? 1.4f : 0.05f, alarmColor);
                on = !on;
                yield return new WaitForSeconds(flashInterval);
            }
            else
            {
                // the closer to the end, the wilder: dark drops and over-bright surges
                float k = 1f - (end - Time.time) / Mathf.Max(0.1f, warningSeconds);
                float b = Random.value < 0.35f + 0.3f * k ? Random.Range(0f, 0.25f) : Random.Range(0.8f, 1.6f + 1.4f * k);
                SetLights(b, Color.clear);
                yield return new WaitForSeconds(Random.Range(0.03f, Mathf.Lerp(0.15f, 0.06f, k)));
            }
        }
        // he stepped back in during the very last moment: wait (alarm still on) until he is out again
        while (KillerInside()) yield return null;
        if (!igniteStarted && igniteSound != null) PlayIgnite();
        if (alarm != null) StartCoroutine(FadeOutAndStop(alarm, 0.25f));
        if (warnFx != null)
        {
            foreach (ParticleSystem ps in warnFx.GetComponentsInChildren<ParticleSystem>(true)) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(warnFx, 3f);
        }
        Seal();

        // then the light dies out: the room is out of bounds now
        if (lightWarning == LightWarning.None) goto LightsDone;
        float start = lightWarning == LightWarning.RedAlarm ? 1.4f : 1.6f;
        Color tint = lightWarning == LightWarning.RedAlarm ? alarmColor : Color.clear;
        float t = 0f;
        while (t < fadeToBlackTime)
        {
            t += Time.deltaTime;
            SetLights(Mathf.Lerp(start, 0f, t / fadeToBlackTime), tint);
            yield return null;
        }
        SetLights(0f, tint);

        LightsDone:
        if (effectLastsAllRound) yield break; // e.g. the fireplace keeps burning

        // the poison stops pouring out and the last of it fades away
        yield return new WaitForSeconds(Mathf.Max(0f, effectDuration - fadeToBlackTime));
        foreach (GameObject c in clouds)
            if (c != null)
            {
                foreach (ParticleSystem ps in c.GetComponentsInChildren<ParticleSystem>(true)) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Destroy(c, 5f);
            }
        clouds.Clear();
    }

    void Seal()
    {
        State = Phase.Sealed;

        if (doorways != null) foreach (LockableDoorway d in doorways) if (d != null) d.LockForever(shutterColor);
        if (barriers != null) foreach (GameObject g in barriers) if (g != null) g.SetActive(true);
        if (dropObjects != null && dropObjects.Length > 0) StartCoroutine(DropObjects());
        if (sealSound != null && area != null)
        {
            if (loopSealSound)
            {
                // a looping sound in the area, heard nearby and fading with distance; fades in over a second
                GameObject s = new GameObject("HazardLoopSound");
                s.transform.SetParent(transform, false);
                s.transform.position = area.bounds.center;
                AudioSource src = s.AddComponent<AudioSource>();
                src.clip = sealSound;
                src.loop = true;
                src.spatialBlend = 1f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 2f;
                src.maxDistance = 12f;
                src.volume = 0f;
                src.Play();
                StartCoroutine(FadeIn(src, sealSoundVolume, 1f));
            }
            else AudioSource.PlayClipAtPoint(sealSound, area.bounds.center, sealSoundVolume);
        }

        SpawnEffects();
        if (extraEffectPrefab != null && (extraEffectCount > 0 || (fireSpots != null && fireSpots.Length > 0))) StartCoroutine(SpawnExtraEffects());
        if (darkenAfterSeal > 0f) StartCoroutine(Darken());

        // everyone still inside dies
        foreach (Hider h in FindObjectsByType<Hider>(FindObjectsSortMode.None))
        {
            if (h == null || h.IsCaught || !Contains(h.transform.position)) continue;
            foreach (HidingSpot s in FindObjectsByType<HidingSpot>(FindObjectsSortMode.None))
                if (s.Occupant == h) { s.Exit(h); break; }
            if (victimEffectPrefab != null)
            {
                GameObject v = Instantiate(victimEffectPrefab, h.transform.position + Vector3.up * 1.2f, Quaternion.identity);
                Destroy(v, 4f);
            }
            KillHider(h);
        }

        BlockNavArea();
        if (lightningStrikes) StartCoroutine(AmbientStrikes());

        // (the killer is never inside here: Run() waits for him to leave before sealing)

        HazardAlertUI.ShowSealed(alertTitle, sealedMessage);
    }

    void SpawnEffects()
    {
        if (effectPrefab == null || area == null) return;
        foreach (BoxCollider box in Boxes()) SpawnEffectsIn(box);
        if (backgroundAreas != null) foreach (BoxCollider bg in backgroundAreas) SpawnBackground(bg);
    }

    void SpawnBackground(BoxCollider box)
    {
        if (box == null) return;
        Vector3 c = box.transform.TransformPoint(box.center);
        Vector3 s = Vector3.Scale(box.size, box.transform.lossyScale);
        GameObject one = Instantiate(effectPrefab, new Vector3(c.x, c.y - s.y * 0.5f + backgroundHeight, c.z), effectPrefab.transform.rotation, transform);
        one.name = "BackgroundEffect";
        clouds.Add(one);
        foreach (MonoBehaviour mb in one.GetComponentsInChildren<MonoBehaviour>(true))
            if (mb.GetType().Name == "CFXR_Effect") Destroy(mb);
        foreach (ParticleSystem ps in one.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.loop = true;
            main.stopAction = ParticleSystemStopAction.None;
            LightRain(ps, Mathf.Abs(s.x), Mathf.Abs(s.z), backgroundDensity, backgroundHeight);
            if (effectTint.a > 0f) main.startColor = effectTint;
            ps.Play();
        }
    }

    void SpawnEffectsIn(BoxCollider box)
    {
        Bounds b = box.bounds;
        if (effectFitToArea)
        {
            Vector3 top = new Vector3(b.center.x, b.min.y + effectHeight, b.center.z);
            GameObject one = Instantiate(effectPrefab, top, effectPrefab.transform.rotation, transform);
            clouds.Add(one);
            foreach (MonoBehaviour mb in one.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb.GetType().Name == "CFXR_Effect") Destroy(mb);
            foreach (ParticleSystem ps in one.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.loop = true;
                main.stopAction = ParticleSystemStopAction.None;
                LightRain(ps, b.size.x, b.size.z, effectDensity, effectHeight);
                if (effectTint.a > 0f) { Color c = effectTint; c.a = main.startColor.color.a; main.startColor = c; }
                ps.Play();
            }
            return;
        }
        int n = Mathf.Max(1, effectCount);
        for (int i = 0; i < n; i++)
        {
            Vector3 p;
            if (effectGrid.x > 0 && effectGrid.y > 0)
            {
                // grid over the inner part of the area
                int cols = effectGrid.x, rows = effectGrid.y;
                int cx = i % cols, cz = (i / cols) % rows;
                float u = cols == 1 ? 0.5f : Mathf.Lerp(0.15f, 0.85f, cx / (cols - 1f));
                float v = rows == 1 ? 0.5f : Mathf.Lerp(0.15f, 0.85f, cz / (rows - 1f));
                if (effectPositionJitter > 0f)
                {
                    float cw = (cols == 1 ? 0.7f : 0.7f / (cols - 1f)) * 0.5f, ch = (rows == 1 ? 0.7f : 0.7f / (rows - 1f)) * 0.5f;
                    u = Mathf.Clamp(u + Random.Range(-cw, cw) * effectPositionJitter, 0.1f, 0.9f);
                    v = Mathf.Clamp(v + Random.Range(-ch, ch) * effectPositionJitter, 0.1f, 0.9f);
                }
                p = new Vector3(Mathf.Lerp(b.min.x, b.max.x, u), b.min.y + effectHeight, Mathf.Lerp(b.min.z, b.max.z, v));
            }
            else
            {
                // spread along the longer side of the room
                float t = n == 1 ? 0.5f : Mathf.Lerp(0.25f, 0.75f, i / (n - 1f)); // keep away from the walls
                p = b.size.x >= b.size.z
                    ? new Vector3(Mathf.Lerp(b.min.x, b.max.x, t), b.min.y + effectHeight, b.center.z)
                    : new Vector3(b.center.x, b.min.y + effectHeight, Mathf.Lerp(b.min.z, b.max.z, t));
            }
            if (spillOrigin != null) p = spillOrigin.position + Vector3.up * effectHeight; // the main fire sits in the opening
            Quaternion rot = spillOrigin != null ? spillOrigin.rotation : (effectRandomRotation ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) : transform.rotation);
            GameObject fx = Instantiate(effectPrefab, p, rot, transform);
            clouds.Add(fx);
            float own = 1f + Random.Range(-effectSizeVariation, effectSizeVariation);
            bool keepSmoke = Random.value < smokeChance;
            if (effectNoEmbers)
                foreach (ParticleSystem sp in fx.GetComponentsInChildren<ParticleSystem>(true))
                    if (sp.name.Contains("Ember") || sp.name.Contains("Spark")) sp.gameObject.SetActive(false);
            foreach (ParticleSystem sp in fx.GetComponentsInChildren<ParticleSystem>(true))
                if (!keepSmoke && sp.name.Contains("Smoke")) sp.gameObject.SetActive(false);
            if (effectHeightScale < 1f)
                foreach (ParticleSystem sp in fx.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var mn = sp.main;
                    if (sp.name.Contains("Smoke")) continue;
                    // squash the flame sprites: same width, less height
                    float s = mn.startSize.constantMax;
                    if (!mn.startSize3D)
                    {
                        var sz = mn.startSize;
                        mn.startSize3D = true;
                        mn.startSizeX = sz;
                        mn.startSizeY = new ParticleSystem.MinMaxCurve(sz.constantMin * effectHeightScale, sz.constantMax * effectHeightScale);
                        mn.startSizeZ = sz;
                    }
                    else mn.startSizeYMultiplier *= effectHeightScale;
                    mn.startLifetimeMultiplier *= Mathf.Lerp(effectHeightScale, 1f, 0.5f);
                }
            if (effectLastsAllRound || effectStartBoost > 1f) StartCoroutine(Settle(fx.transform));
            // keep it going for the rest of the round
            foreach (MonoBehaviour mb in fx.GetComponentsInChildren<MonoBehaviour>(true))
                if (loopEffect && mb.GetType().Name == "CFXR_Effect") Destroy(mb); // its auto-destroy would remove the cloud (a one-shot keeps it: flash + shake)
            foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.loop = loopEffect;
                main.stopAction = ParticleSystemStopAction.None;
                // smaller and slower so it stays inside the room
                main.startSpeedMultiplier *= effectScale * own;
                main.startSizeMultiplier *= Mathf.Lerp(effectScale, 1f, 0.15f) * own;
                bool smoke = ps.name.Contains("Smoke");
                Color tint = smoke ? smokeTint : effectTint;
                if (tint.a > 0f) { Color c = tint; c.a = main.startColor.color.a; main.startColor = c; }
                if (smoke && smokeTint.a > 0f) { var col = ps.colorOverLifetime; col.enabled = false; }
                var shape = ps.shape;
                shape.radius *= effectScale;
                ps.Play();
            }
        }
    }

    // keep the killer (and most AI players) out: take the hazard's navmesh area off their walkable areas
    void BlockNavArea()
    {
        if (string.IsNullOrEmpty(navArea)) return;
        int idx = NavMesh.GetAreaFromName(navArea);
        if (idx < 0) return;
        int bit = 1 << idx;
        foreach (EnemyVision k in FindObjectsByType<EnemyVision>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            NavMeshAgent a = k.GetComponent<NavMeshAgent>();
            if (a != null) a.areaMask &= ~bit;
        }
        foreach (AIHider ai in FindObjectsByType<AIHider>(FindObjectsSortMode.None))
        {
            if (Random.value < aiMistakeChance) { careless.Add(ai); continue; } // this one may still wander in
            NavMeshAgent a = ai.GetComponent<NavMeshAgent>();
            if (a != null) a.areaMask &= ~bit;
        }
    }

    // deadly while active: anyone (player or AI) who steps in dies
    // kills a hider; in a thunderstorm a bolt hits them first
    void KillHider(Hider h)
    {
        if (lightningStrikes) Strike(h.transform.position);
        h.Freeze();
    }

    IEnumerator AmbientStrikes()
    {
        if (ambientStrikeInterval.y <= 0f) yield break;
        while (State == Phase.Sealed)
        {
            yield return new WaitForSeconds(Random.Range(ambientStrikeInterval.x, ambientStrikeInterval.y));
            if (State != Phase.Sealed) yield break;
            List<BoxCollider> boxes = new List<BoxCollider>(Boxes());
            if (boxes.Count == 0) yield break;
            Bounds b = boxes[Random.Range(0, boxes.Count)].bounds;
            Vector3 p = new Vector3(Random.Range(b.min.x + 0.3f, b.max.x - 0.3f), b.max.y, Random.Range(b.min.z + 0.3f, b.max.z - 0.3f));
            RaycastHit hit;
            p.y = Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out hit, 10f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : b.min.y;
            Strike(p);
        }
    }

    // one lightning bolt landing on 'ground'
    void Strike(Vector3 ground)
    {
        StartCoroutine(Bolt(ground));
        if (strikeEffectPrefab != null) { GameObject e = Instantiate(strikeEffectPrefab, ground + Vector3.up * 0.3f, Quaternion.identity); Destroy(e, 4f); }
        if (strikeFlashPrefab != null) { GameObject e = Instantiate(strikeFlashPrefab, ground + Vector3.up * 1f, Quaternion.identity); Destroy(e, 3f); }
        if (strikeSound != null)
        {
            GameObject s = new GameObject("ThunderStrike");
            s.transform.position = ground;
            AudioSource src = s.AddComponent<AudioSource>();
            src.clip = strikeSound; src.volume = strikeSoundVolume; src.spatialBlend = 0.6f; src.minDistance = 6f; src.maxDistance = 40f;
            src.Play();
            Destroy(s, strikeSound.length + 0.2f);
        }
    }

    IEnumerator Bolt(Vector3 ground)
    {
        if (boltMaterial == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            boltMaterial = new Material(sh);
        }
        GameObject go = new GameObject("LightningBolt");
        go.transform.SetParent(transform, true);
        LineRenderer main = MakeLine(go, 0.14f);
        LineRenderer branch = MakeLine(new GameObject("Branch"), 0.06f);
        branch.transform.SetParent(go.transform, true);

        Light glow = new GameObject("BoltLight").AddComponent<Light>();
        glow.transform.SetParent(go.transform, true);
        glow.transform.position = ground + Vector3.up * 1.5f;
        glow.type = LightType.Point; glow.color = boltColor; glow.range = 10f; glow.intensity = 12f;

        Vector3 top = ground + new Vector3(Random.Range(-1.5f, 1.5f), boltHeight, Random.Range(-1.5f, 1.5f));
        float t = 0f, life = 0.45f, nextJag = 0f;
        while (t < life)
        {
            if (t >= nextJag) // re-jag a few times so it crackles
            {
                Jag(main, top, ground, 12, 0.45f);
                Vector3 from = Vector3.Lerp(top, ground, Random.Range(0.3f, 0.6f));
                Jag(branch, from, from + new Vector3(Random.Range(-2f, 2f), -Random.Range(1.5f, 3f), Random.Range(-2f, 2f)), 6, 0.3f);
                nextJag = t + 0.07f;
            }
            float k = 1f - t / life;
            float flick = (Random.value > 0.3f) ? 1f : 0.25f;
            Color c = boltColor; c.a = k * flick;
            main.startColor = main.endColor = c;
            branch.startColor = branch.endColor = new Color(c.r, c.g, c.b, c.a * 0.7f);
            glow.intensity = 12f * k * flick;
            t += Time.deltaTime;
            yield return null;
        }
        Destroy(go);
    }

    LineRenderer MakeLine(GameObject go, float width)
    {
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = boltMaterial;
        lr.useWorldSpace = true;
        lr.startWidth = width; lr.endWidth = width * 0.4f;
        lr.numCapVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
    }

    static void Jag(LineRenderer lr, Vector3 from, Vector3 to, int points, float spread)
    {
        lr.positionCount = points;
        for (int i = 0; i < points; i++)
        {
            float f = i / (float)(points - 1);
            Vector3 p = Vector3.Lerp(from, to, f);
            if (i > 0 && i < points - 1) p += new Vector3(Random.Range(-spread, spread), 0f, Random.Range(-spread, spread));
            lr.SetPosition(i, p);
        }
    }

    // Cheap rain over an area: no physics collisions or splash sub-emitters, and each drop only lives
    // long enough to reach the floor, so far fewer particles exist at once.
    static void LightRain(ParticleSystem ps, float sizeX, float sizeZ, float density, float height)
    {
        var main = ps.main;
        var shape = ps.shape;
        shape.scale = new Vector3(sizeX, sizeZ, 1f);
        var col = ps.collision; col.enabled = false;
        var sub = ps.subEmitters; sub.enabled = false;
        float speed = Mathf.Max(1f, main.startSpeed.constantMax);
        float life = Mathf.Clamp((height + 0.3f) / speed, 0.1f, Mathf.Max(0.1f, main.startLifetime.constantMax));
        main.startLifetime = life;
        float rate = Mathf.Max(5f, sizeX * sizeZ * density);
        var em = ps.emission;
        em.rateOverTimeMultiplier = rate;
        main.maxParticles = Mathf.CeilToInt(rate * life * 1.3f) + 10;
    }

    Hider[] watched;
    float nextCheck;

    [Header("Round end")]
    [Tooltip("Seconds the hazard takes to die down once the round is over (effects, sounds, lights and darkness fade out).")]
    public float endFadeTime = 1.5f;
    bool ended;

    void Update()
    {
        // the round is over: like the TV and the vacuum, the hazard switches off (here it dies down smoothly)
        if (!ended && State != Phase.Idle && RoundManager.Instance != null && RoundManager.Instance.RoundOver)
        {
            ended = true;
            StopAllCoroutines(); // no more alarms, ignitions, flames spreading or lightning
            StartCoroutine(EndHazard());
            return;
        }
        if (ended) return;
        if (!deadlyWhileActive || State != Phase.Sealed) return;
        // 10 checks a second is plenty, and the player list is looked up once instead of every frame
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + 0.1f;
        if (watched == null) watched = FindObjectsByType<Hider>(FindObjectsSortMode.None);
        foreach (Hider h in watched)
        {
            if (h == null || h.IsCaught || !Contains(h.transform.position)) continue;
            foreach (HidingSpot s in FindObjectsByType<HidingSpot>(FindObjectsSortMode.None))
                if (s.Occupant == h) { s.Exit(h); break; }
            if (victimEffectPrefab != null) { GameObject v = Instantiate(victimEffectPrefab, h.transform.position + Vector3.up * 1.2f, Quaternion.identity); Destroy(v, 4f); }
            KillHider(h);
        }
    }

    // Fade the hazard out at the end of the round. Runs on real time: the house freezes (time scale 0) a moment after the round ends.
    IEnumerator EndHazard()
    {
        // effects stop emitting; the particles already in the air keep moving (on real time) and die out
        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.useUnscaledTime = true;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        foreach (GameObject c in clouds)
            if (c != null)
                foreach (ParticleSystem ps in c.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    main.useUnscaledTime = true;
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }

        // what is fading
        AudioSource[] sounds = GetComponentsInChildren<AudioSource>(true);
        float[] vol = new float[sounds.Length];
        for (int i = 0; i < sounds.Length; i++) if (sounds[i] != null) vol[i] = sounds[i].volume;
        float[] lightFrom = new float[roomLights != null ? roomLights.Length : 0];
        Color[] colFrom = new Color[lightFrom.Length];
        for (int i = 0; i < lightFrom.Length; i++) if (roomLights[i] != null) { lightFrom[i] = roomLights[i].intensity; colFrom[i] = roomLights[i].color; }
        Transform dark = transform.Find("RoomDarkness");
        Material dm = dark != null ? dark.GetComponent<MeshRenderer>().sharedMaterial : null;
        Color dFrom = dm != null && dm.HasProperty("_BaseColor") ? dm.GetColor("_BaseColor") : Color.clear;

        float t = 0f, time = Mathf.Max(0.1f, endFadeTime);
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / time);
            for (int i = 0; i < sounds.Length; i++) if (sounds[i] != null) sounds[i].volume = vol[i] * (1f - k);
            for (int i = 0; i < lightFrom.Length; i++)
            {
                Light l = roomLights[i];
                if (l == null) continue;
                l.intensity = Mathf.Lerp(lightFrom[i], lightIntensities[i], k); // the room's lights come back as they were
                l.color = Color.Lerp(colFrom[i], lightColors[i], k);
            }
            if (dm != null)
            {
                if (darkenFloorOnly) dm.SetColor("_BaseColor", Color.Lerp(dFrom, Color.white, k));
                else { Color c = dFrom; c.a = dFrom.a * (1f - k); dm.SetColor("_BaseColor", c); }
            }
            yield return null;
        }
        foreach (AudioSource s in sounds) if (s != null) s.Stop();
        if (dark != null) Destroy(dark.gameObject);
    }

    // a soft black volume fills the room and fades in: it looks charred / dark from every camera
    IEnumerator Darken()
    {
        if (area == null || darknessMaterial == null) yield break;
        Bounds b = area.bounds;
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = "RoomDarkness";
        Destroy(box.GetComponent<Collider>());
        box.transform.SetParent(transform, true);
        float h = Mathf.Max(0.1f, darknessHeight);
        box.transform.position = new Vector3(b.center.x, b.min.y + h * 0.5f, b.center.z);
        if (darkenFloorOnly)
        {
            float floorY = b.min.y;
            NavMeshHit fh;
            if (NavMesh.SamplePosition(b.center, out fh, b.size.y, NavMesh.AllAreas)) floorY = fh.position.y;
            h = 0.02f;
            box.transform.position = new Vector3(b.center.x, floorY + 0.015f, b.center.z);
        }
        box.transform.rotation = Quaternion.identity;
        box.transform.localScale = new Vector3(b.size.x - 0.04f, h, b.size.z - 0.04f);
        MeshRenderer r = box.GetComponent<MeshRenderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        Material m = new Material(darknessMaterial);
        if (darkenFloorOnly)
        {
            // multiply the floor by (1 - darkness): it really darkens a lit floor, and is drawn before the flames
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.DstColor);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            m.renderQueue = 2450;
        }
        r.sharedMaterial = m;
        Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.black;
        float t = 0f, time = Mathf.Max(0.1f, fadeToBlackTime);
        while (t < time)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, darkenAfterSeal, t / time);
            if (darkenFloorOnly) m.SetColor("_BaseColor", new Color(1f - k, 1f - k, 1f - k, 1f));
            else { c.a = k; m.SetColor("_BaseColor", c); }
            yield return null;
        }
        if (darkenFloorOnly) m.SetColor("_BaseColor", new Color(1f - darkenAfterSeal, 1f - darkenAfterSeal, 1f - darkenAfterSeal, 1f));
        else { c.a = darkenAfterSeal; m.SetColor("_BaseColor", c); }
    }

    // An appliance (TV, vacuum, clock, piano) running in this room loses power: switch it off now, so the killer
    // doesn't keep walking to a switch that is about to be behind a locked door. (They won't start again either.)
    void SwitchOffAppliancesInside()
    {
        foreach (MonoBehaviour mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            System.Type t = mb.GetType();
            var posField = t.GetField("turnOffPosition");
            var isOn = t.GetProperty("IsOn");
            var off = t.GetMethod("TurnOff", System.Type.EmptyTypes);
            if (posField == null || isOn == null || off == null) continue;
            Transform p = posField.GetValue(mb) as Transform;
            Vector3 at = p != null ? p.position : mb.transform.position;
            if (Contains(at) && (bool)isOn.GetValue(mb, null)) off.Invoke(mb, null);
        }
    }

    // loop an effect until we stop it (and keep CFXR from auto-destroying it)
    static void KeepGoing(GameObject fx)
    {
        foreach (MonoBehaviour mb in fx.GetComponentsInChildren<MonoBehaviour>(true))
            if (mb.GetType().Name == "CFXR_Effect") Destroy(mb);
        foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.loop = true;
            main.stopAction = ParticleSystemStopAction.None;
            ps.Play();
        }
    }

    // brightness relative to the light's own (1 = normal)
    void SetLights(float brightness, Color tint)
    {
        if (roomLights == null) return;
        for (int i = 0; i < roomLights.Length; i++)
        {
            Light l = roomLights[i];
            if (l == null) continue;
            l.color = tint.a > 0f ? tint : lightColors[i]; // Color.clear = keep the light's own colour
            l.intensity = lightIntensities[i] * brightness;
        }
    }

    void OnDrawGizmos()
    {
        if (area == null) return;
        Gizmos.color = new Color(0.4f, 1f, 0.2f, State == Phase.Idle ? 0.15f : 0.4f);
        Gizmos.matrix = area.transform.localToWorldMatrix;
        Gizmos.DrawCube(area.center, area.size);
    }


// is the killer standing in this room right now?
    bool KillerInside()
    {
        foreach (EnemyVision k in FindObjectsByType<EnemyVision>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (k != null && k.gameObject.activeInHierarchy && Contains(k.transform.position)) return true;
        return false;
    }
}
