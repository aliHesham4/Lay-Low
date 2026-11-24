using System.Collections;
using UnityEngine;

// ROUND 4: "only one person is coming home alive".
// The first minute is a normal round. With 'huntSeconds' left, a message announces the final minute and,
// a few seconds later, a golden knife collectible drops at a random spot (with a beam and a chime).
// The first player or AI to reach it holds the knife and may kill everyone else (see KnifeHolder).
// If the holder is caught, the knife drops where they fell and anyone can take it.
public class FinalHuntRound : MonoBehaviour
{
    [Header("When")]
    [Tooltip("This scene's round is the knife round when the match is at this round.")]
    public int activeRound = 4;
    [Tooltip("Seconds left in the round when the knife drops (it also drops right after the first player is caught, whichever comes first).")]
    public float huntSeconds = 90f;
    [Tooltip("Seconds between the message and the knife landing.")]
    public float dropDelay = 4f;

    [Header("Fog")]
    [Tooltip("Light fog fills the house during this round.")]
    public bool fogEnabled = true;
    public Color fogColor = new Color(0.36f, 0.38f, 0.42f);
    [Tooltip("Exponential-squared density. ~0.06 = clear up close, hazy across a room, thick down long hallways.")]
    public float fogDensity = 0.06f;
    [Tooltip("Seconds for the fog to roll in at the start of the round.")]
    public float fogFadeSeconds = 4f;

    
[Header("Message")]
    public string title = "ONLY ONE OF YOU LEAVES THIS HOUSE ALIVE";
    [TextArea] public string message = "A golden knife is about to drop. Whoever grabs it hunts the rest.";

    [Header("Golden knife")]
    [Tooltip("The golden collectible (glass bubble with the bloody knife inside).")]
    public GameObject knifeCollectiblePrefab;
    [Tooltip("The bloody knife model that ends up in the holder's right hand.")]
    public GameObject knifeModelPrefab;
    public AudioClip dropSound;
    [Range(0f, 1f)] public float dropVolume = 0.5f;
    public AudioClip grabSound;
    [Range(0f, 1f)] public float grabVolume = 0.8f;
    public AudioClip stabSound;
    [Tooltip("Bloody knife picture shown beside the holder's face on the HUD card.")]
    public Texture knifeIcon;
    [Tooltip("The knife never lands closer than this to another collectible (metres).")]
    public float minDistanceFromCollectibles = 1.4f;
    [Tooltip("The knife never lands closer than this to a player (metres).")]
    public float minDistanceFromPlayers = 4f;
    [Tooltip("The knife lands at a random one of this many spots, chosen from those farthest from every player.")]
    public int farthestSpotsPool = 3;

    static FinalHuntRound instance;
    public static FinalHuntRound Instance { get { return instance; } }
    // this scene's round is the knife round
    public static bool ThisRound { get { return instance != null && instance.isActiveAndEnabled && RoundManager.CurrentRound == instance.activeRound; } }

    GameObject current;   // the golden collectible lying on the map

    void Awake() { instance = this; }
    void OnDestroy() { if (instance == this) instance = null; }

void Start()
    {
        if (!ThisRound) { enabled = false; return; }
        if (fogEnabled) StartCoroutine(RollInFog());
        StartCoroutine(Run());
    }

// fog settings belong to the scene, so the next round's reload puts the old fog back on its own
    IEnumerator RollInFog()
    {
        Color startColor = RenderSettings.fogColor;
        float startDensity = RenderSettings.fog ? RenderSettings.fogDensity : 0f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        for (float t = 0f; t < fogFadeSeconds; t += Time.deltaTime)
        {
            float k = t / fogFadeSeconds;
            RenderSettings.fogColor = Color.Lerp(startColor, fogColor, k);
            RenderSettings.fogDensity = Mathf.Lerp(startDensity, fogDensity, k);
            yield return null;
        }
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;
    }


IEnumerator Run()
    {
        // wait for whichever comes first: 'huntSeconds' left on the clock, or the first player caught
        while (RoundManager.Instance == null || (RoundManager.Instance.TimeLeft > huntSeconds && !AnyoneCaught()))
        {
            if (RoundManager.Instance != null && RoundManager.Instance.RoundOver) yield break;
            yield return null;
        }
        if (RoundManager.Instance.RoundOver) yield break;

        HazardAlertUI.ShowAnnouncement(title, message, dropDelay + 3f);
        yield return new WaitForSeconds(dropDelay);
        if (RoundManager.Instance.RoundOver) yield break;
        SpawnAtRandomPoint();
    }

// true as soon as any player (human or AI) has been caught this round
    static bool AnyoneCaught()
    {
        foreach (Hider h in FindObjectsByType<Hider>(FindObjectsSortMode.None))
            if (h != null && h.IsCaught) return true;
        return false;
    }


    // ---------------- the collectible ----------------

// Lands at a random spot among the ones farthest from every free player, so nobody gets it for free
// Lands at a random spot among the ones farthest from every free player, so nobody gets it for free,
    // and never on (or right next to) another item
    void SpawnAtRandomPoint()
    {
        if (knifeCollectiblePrefab == null) return;
        ItemSpawner spawner = FindFirstObjectByType<ItemSpawner>();
        Transform[] pts = spawner != null ? spawner.spawnPoints : null;
        if (pts == null || pts.Length == 0) return;

        Hider[] players = FindObjectsByType<Hider>(FindObjectsSortMode.None);
        var items = ItemPositions();
        var spots = new System.Collections.Generic.List<KeyValuePairSpot>();
        Transform farthestAny = null; float farthestAnyDist = -1f; // fallback: farthest from players, even if an item is close
        foreach (Transform p in pts)
        {
            if (p == null || EnvironmentalHazard.IsDangerous(p.position)) continue;
            float gap = float.MaxValue; // distance to the closest item
            foreach (Vector3 it in items) { Vector3 d = it - p.position; d.y = 0f; gap = Mathf.Min(gap, d.magnitude); }
            float nearest = float.MaxValue; // distance to the closest free player
            foreach (Hider h in players)
                if (h != null && !h.IsCaught) nearest = Mathf.Min(nearest, Vector3.Distance(h.transform.position, p.position));
            if (nearest > farthestAnyDist) { farthestAnyDist = nearest; farthestAny = p; }
            if (gap < minDistanceFromCollectibles) continue;
            spots.Add(new KeyValuePairSpot(p, nearest));
        }

        if (spots.Count > 0)
        {
            // farthest first, then pick randomly among the top few
            spots.Sort((a, b) => b.distance.CompareTo(a.distance));
            int pool = Mathf.Clamp(farthestSpotsPool, 1, spots.Count);
            Place(spots[Random.Range(0, pool)].point.position);
        }
        else if (farthestAny != null)
        {
            // every spawn point has an item near it: still go far from the players, then slide clear of the item there
            DropKnife(farthestAny.position);
        }
    }

struct KeyValuePairSpot
    {
        public Transform point; public float distance;
        public KeyValuePairSpot(Transform p, float d) { point = p; distance = d; }
    }


    // The holder fell: the knife drops where they were (snapped to the floor)
    public void DropKnife(Vector3 where)
    {
        if (knifeCollectiblePrefab == null || RoundManager.Instance == null || RoundManager.Instance.RoundOver) return;
        RaycastHit hit;
        float y = where.y;
        if (Physics.Raycast(where + Vector3.up * 1f, Vector3.down, out hit, 3f, 1 << 9, QueryTriggerInteraction.Ignore)) y = hit.point.y;
        Vector3 at = new Vector3(where.x, y + 0.32f, where.z);
        // never on top of another collectible: slide to the nearest free spot around
        for (int ring = 1; ring <= 3 && NearCollectible(at); ring++)
            for (int a = 0; a < 8; a++)
            {
                float ang = a * 45f * Mathf.Deg2Rad;
                Vector3 c = new Vector3(where.x + Mathf.Cos(ang) * ring * 0.9f, at.y, where.z + Mathf.Sin(ang) * ring * 0.9f);
                UnityEngine.AI.NavMeshHit nh;
                if (!NearCollectible(c) && UnityEngine.AI.NavMesh.SamplePosition(c, out nh, 0.3f, UnityEngine.AI.NavMesh.AllAreas)) { at = c; ring = 99; break; }
            }
        Place(at);
    }

    static void Play2D(AudioClip clip, float volume)
    {
        GameObject g = new GameObject("FinalHunt Sound");
        AudioSource s = g.AddComponent<AudioSource>();
        s.clip = clip; s.volume = volume; s.spatialBlend = 0f; s.ignoreListenerPause = false;
        s.Play();
        Destroy(g, clip.length + 0.2f);
    }

bool NearCollectible(Vector3 p)
    {
        foreach (Vector3 it in ItemPositions())
        {
            Vector3 d = it - p; d.y = 0f;
            if (d.magnitude < minDistanceFromCollectibles) return true;
        }
        return false;
    }

    // Every item lying on the map: everything the ItemSpawner drops, plus anything else that spins as a collectible
    System.Collections.Generic.List<Vector3> ItemPositions()
    {
        var list = new System.Collections.Generic.List<Vector3>();
        System.Action<Component> add = c => { if (c != null && c.gameObject.activeInHierarchy && (current == null || c.gameObject != current)) list.Add(c.transform.position); };
        foreach (GameObject st in GameObject.FindGameObjectsWithTag("CollectibleStone")) if (st.activeInHierarchy) list.Add(st.transform.position);
        foreach (PepperCollectible c in FindObjectsByType<PepperCollectible>(FindObjectsSortMode.None)) add(c);
        foreach (WhistleCollectible c in FindObjectsByType<WhistleCollectible>(FindObjectsSortMode.None)) add(c);
        foreach (FeatherCollectible c in FindObjectsByType<FeatherCollectible>(FindObjectsSortMode.None)) add(c);
        foreach (DoorLockCollectible c in FindObjectsByType<DoorLockCollectible>(FindObjectsSortMode.None)) add(c);
        foreach (IceGunCollectible c in FindObjectsByType<IceGunCollectible>(FindObjectsSortMode.None)) add(c);
        foreach (CollectibleSpin c in FindObjectsByType<CollectibleSpin>(FindObjectsSortMode.None)) add(c);
        return list;
    }

    void Place(Vector3 pos)
    {
        if (current != null) Destroy(current);
        current = Instantiate(knifeCollectiblePrefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        if (dropSound != null)
        {
            Play2D(dropSound, dropVolume); // flat sound: everybody hears it the same, wherever they look
        }
        // the AI wants it too, after a short reaction time
        foreach (AIHider ai in FindObjectsByType<AIHider>(FindObjectsSortMode.None))
            if (ai != null && ai.GetComponent<AIKnifeSeeker>() == null) ai.gameObject.AddComponent<AIKnifeSeeker>();
    }

    // ---------------- taking it ----------------

    public void Grant(Hider h, GoldenKnifeCollectible from)
    {
        if (h == null || h.IsCaught || KnifeHolder.Current != null) return;
        if (from != null && from.gameObject == current) current = null;
        if (from != null) Destroy(from.gameObject);

        KnifeHolder k = h.gameObject.AddComponent<KnifeHolder>();
        k.knifeModelPrefab = knifeModelPrefab;
        k.stabSound = stabSound;
        k.knifeIcon = knifeIcon;
        KnifeHolder.Current = k;

        foreach (AIKnifeSeeker s in FindObjectsByType<AIKnifeSeeker>(FindObjectsSortMode.None)) Destroy(s);
        if (h.GetComponent<AIHider>() != null) h.gameObject.AddComponent<AIKnifeHunter>();

        if (grabSound != null) Play2D(grabSound, grabVolume);
        if (RoundManager.Instance != null) RoundManager.Instance.Toast(h.displayName + " grabbed the golden knife!");
    }
}
