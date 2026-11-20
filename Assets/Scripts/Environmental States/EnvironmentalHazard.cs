using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// One room that can 'malfunction' (environmental state). When it starts:
//   1. an alert pops up for everyone (bottom-left) and counts down warningSeconds,
//   2. then the effect fills the room, every doorway into it slams shut for the rest of the round,
//      and anyone still inside (players or AI) dies. The killer is pushed out; nobody can enter after.
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
        if (area == null) return false;
        Vector3 local = area.transform.InverseTransformPoint(p) - area.center;
        Vector3 half = area.size * 0.5f;
        return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.z) <= half.z && Mathf.Abs(local.y) <= half.y + 1f;
    }

    // a room that is about to seal or already sealed
    public static bool IsDangerous(Vector3 p)
    {
        foreach (EnvironmentalHazard h in all) if (h != null && h.State != Phase.Idle && h.Contains(p)) return true;
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
            Bounds bb = area.bounds;
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
            if (!Mathf.Approximately(warningEffectScale, 1f))
                foreach (ParticleSystem ps in warnFx.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    main.startSpeedMultiplier *= warningEffectScale;
                    main.startSizeMultiplier *= warningEffectScale;
                }
        }
        float end = Time.time + warningSeconds;
        bool on = true;
        while (Time.time < end)
        {
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
        if (sealSound != null && area != null) AudioSource.PlayClipAtPoint(sealSound, area.bounds.center);

        SpawnEffects();
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
            h.Freeze();
        }

        // the killer is not affected, he just gets pushed out of the room
        foreach (EnemyVision k in FindObjectsByType<EnemyVision>(FindObjectsSortMode.None))
        {
            if (k == null || !Contains(k.transform.position)) continue;
            Vector3 exit;
            NavMeshAgent a = k.GetComponent<NavMeshAgent>();
            if (a != null && a.enabled && ExitPoint(k.transform.position, out exit)) a.Warp(exit);
        }

        HazardAlertUI.ShowSealed(alertTitle, sealedMessage);
    }

    void SpawnEffects()
    {
        if (effectPrefab == null || area == null) return;
        Bounds b = area.bounds;
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
            GameObject fx = Instantiate(effectPrefab, p, effectRandomRotation ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) : transform.rotation, transform);
            clouds.Add(fx);
            float own = 1f + Random.Range(-effectSizeVariation, effectSizeVariation);
            bool keepSmoke = Random.value < smokeChance;
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
}
