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
    [Tooltip("Seconds left in the round when the final minute starts.")]
    public float huntSeconds = 60f;
    [Tooltip("Seconds between the message and the knife landing.")]
    public float dropDelay = 4f;

    [Header("Message")]
    public string title = "ONLY ONE PERSON IS COMING HOME ALIVE";
    [TextArea] public string message = "A golden knife is about to drop.\nWhoever grabs it hunts the rest. Take it, or hide.";

    [Header("Golden knife")]
    [Tooltip("The golden collectible (glass bubble with the bloody knife inside).")]
    public GameObject knifeCollectiblePrefab;
    [Tooltip("The bloody knife model that ends up in the holder's right hand.")]
    public GameObject knifeModelPrefab;
    public AudioClip dropSound;
    [Range(0f, 1f)] public float dropVolume = 0.9f;
    public AudioClip grabSound;
    [Range(0f, 1f)] public float grabVolume = 0.8f;
    public AudioClip stabSound;
    [Tooltip("Bloody knife picture shown beside the holder's face on the HUD card.")]
    public Texture knifeIcon;
    [Tooltip("The knife never lands closer than this to another collectible (metres).")]
    public float minDistanceFromCollectibles = 1.4f;
    [Tooltip("The knife never lands closer than this to a player (metres).")]
    public float minDistanceFromPlayers = 4f;

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
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        // wait for the final minute
        while (RoundManager.Instance == null || RoundManager.Instance.TimeLeft > huntSeconds)
        {
            if (RoundManager.Instance != null && RoundManager.Instance.RoundOver) yield break;
            yield return null;
        }

        HazardAlertUI.ShowAnnouncement(title, message, dropDelay + 3f);
        yield return new WaitForSeconds(dropDelay);
        if (RoundManager.Instance.RoundOver) yield break;
        SpawnAtRandomPoint();
    }

    // ---------------- the collectible ----------------

    void SpawnAtRandomPoint()
    {
        if (knifeCollectiblePrefab == null) return;
        ItemSpawner spawner = FindFirstObjectByType<ItemSpawner>();
        Transform[] pts = spawner != null ? spawner.spawnPoints : null;
        if (pts == null || pts.Length == 0) return;

        Hider[] players = FindObjectsByType<Hider>(FindObjectsSortMode.None);
        var ok = new System.Collections.Generic.List<Transform>();
        for (float minDist = minDistanceFromPlayers; minDist >= 0f && ok.Count == 0; minDist -= 2f)
        {
            foreach (Transform p in pts)
            {
                if (p == null || EnvironmentalHazard.IsDangerous(p.position) || NearCollectible(p.position)) continue;
                bool near = false;
                foreach (Hider h in players)
                    if (h != null && !h.IsCaught && Vector3.Distance(h.transform.position, p.position) < minDist) { near = true; break; }
                if (!near) ok.Add(p);
            }
        }
        if (ok.Count == 0) return;
        Place(ok[Random.Range(0, ok.Count)].position);
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
        foreach (CollectibleSpin c in FindObjectsByType<CollectibleSpin>(FindObjectsSortMode.None))
        {
            if (c == null || !c.gameObject.activeInHierarchy || (current != null && c.gameObject == current)) continue;
            Vector3 d = c.transform.position - p; d.y = 0f;
            if (d.magnitude < minDistanceFromCollectibles) return true;
        }
        return false;
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
        if (RoundManager.Instance != null) RoundManager.Instance.Toast(h.displayName + " grabbed the GOLDEN KNIFE!");
    }
}
