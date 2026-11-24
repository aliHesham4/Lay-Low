using System.Collections;
using UnityEngine;

// Drops a random mix of COLLECTIBLES (kept in the bag: rock, feather, door lock...) and
// INSTANTS (used on touch: pepper...) on random spawn points. Never more than maxItemsOnMap in total.
public class ItemSpawner : MonoBehaviour
{
    [Header("Spawn Points")]
    public Transform[] spawnPoints;

    [Header("What can spawn")]
    [Tooltip("Items that go into the bag. They must keep the tag CollectibleStone so players and the AI can find them.")]
    public GameObject[] collectiblePrefabs;
    [Tooltip("Items that act the moment someone touches them (pepper...).")]
    public GameObject[] instantPrefabs;
    [Range(0f, 1f)] [Tooltip("Chance that a new drop is an instant instead of a collectible.")]
    public float instantChance = 0.3f;

    [Header("Timing")]
    public float minSpawnTime = 5f;
    public float maxSpawnTime = 10f;

    [Header("Limits")]
    [Tooltip("Maximum collectibles + instants lying on the map at the same time.")]
    public int maxItemsOnMap = 3;
    [Tooltip("Maximum instants (peppers...) lying on the map at the same time.")]
    public int maxInstantsInMap = 2;
    [Tooltip("Maximum party whistles lying on the map at the same time.")]
    public int maxWhistlesInMap = 1;
    [Tooltip("Maximum feathers lying on the map at the same time.")]
    public int maxFeathersInMap = 1;
    [Tooltip("Maximum door locks lying on the map at the same time.")]
    public int maxDoorLocksInMap = 1;
    [Tooltip("Maximum ice guns lying on the map at the same time.")]
    public int maxIceGunsInMap = 1;
    [Tooltip("Layer given to instants so they are easy to recognise.")]
    public string instantLayerName = "Pepper";

    [Header("Spreading out")]
    [Tooltip("New items prefer points at least this far from the items already lying around.")]
    public float itemSpacing = 5f;
    [Tooltip("Points used for the last few drops are skipped, so items move around the house.")]
    public int rememberLastPoints = 4;

    private readonly System.Collections.Generic.Queue<Transform> recentPoints = new System.Collections.Generic.Queue<Transform>();

    [Tooltip("How many items are already lying around when the round starts.")]
    public int itemsAtStart = 3;

    void Start()
    {
        // blackout round: fewer items, some kinds left out
        if (BlackoutRound.ThisRound)
        {
            itemsAtStart = BlackoutRound.Instance.itemsAtStart;
            maxItemsOnMap = BlackoutRound.Instance.maxItemsOnMap;
            if (collectiblePrefabs != null) collectiblePrefabs = System.Array.FindAll(collectiblePrefabs, p => !BlackoutRound.Excluded(p));
            if (instantPrefabs != null) instantPrefabs = System.Array.FindAll(instantPrefabs, p => !BlackoutRound.Excluded(p));
        }
        // drop the starting items right away (each one still respects the limits and spacing)
        for (int i = 0; i < itemsAtStart; i++)
            if (CountCollectibles() + CountInstants() < maxItemsOnMap) SpawnRandomItem();

        StartCoroutine(SpawnRoutine());
    }

    IEnumerator SpawnRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minSpawnTime, maxSpawnTime));
            if (CountCollectibles() + CountInstants() < maxItemsOnMap) SpawnRandomItem();
        }
    }

    int CountCollectibles()
    {
        return GameObject.FindGameObjectsWithTag("CollectibleStone").Length +
               FindObjectsByType<WhistleCollectible>(FindObjectsSortMode.None).Length +
               FindObjectsByType<FeatherCollectible>(FindObjectsSortMode.None).Length +
               FindObjectsByType<DoorLockCollectible>(FindObjectsSortMode.None).Length +
               FindObjectsByType<IceGunCollectible>(FindObjectsSortMode.None).Length;
    }
    int CountInstants() { return FindObjectsByType<PepperCollectible>(FindObjectsSortMode.None).Length; }

    void SpawnRandomItem()
    {
        bool canInstant = instantPrefabs != null && instantPrefabs.Length > 0 && CountInstants() < maxInstantsInMap;
        bool canCollectible = collectiblePrefabs != null && collectiblePrefabs.Length > 0;
        if (!canInstant && !canCollectible) return;

        bool instant = canInstant && (!canCollectible || Random.value < instantChance);
        GameObject[] pool = instant ? instantPrefabs : collectiblePrefabs;
        GameObject prefab = pool[Random.Range(0, pool.Length)];
        if (prefab == null) return;
        // limit whistles: pick something else when one is already lying around
        if (prefab.GetComponent<WhistleCollectible>() != null &&
            FindObjectsByType<WhistleCollectible>(FindObjectsSortMode.None).Length >= maxWhistlesInMap)
        {
            var others = new System.Collections.Generic.List<GameObject>();
            foreach (GameObject g in pool) if (g != null && g.GetComponent<WhistleCollectible>() == null) others.Add(g);
            if (others.Count == 0) return;
            prefab = others[Random.Range(0, others.Count)];
        }

        // limit feathers the same way
        if (prefab.GetComponent<FeatherCollectible>() != null &&
            FindObjectsByType<FeatherCollectible>(FindObjectsSortMode.None).Length >= maxFeathersInMap)
        {
            var others = new System.Collections.Generic.List<GameObject>();
            foreach (GameObject g in pool) if (g != null && g.GetComponent<FeatherCollectible>() == null && g.GetComponent<WhistleCollectible>() == null) others.Add(g);
            if (others.Count == 0) return;
            prefab = others[Random.Range(0, others.Count)];
        }

        // limit door locks the same way
        if (prefab.GetComponent<DoorLockCollectible>() != null &&
            FindObjectsByType<DoorLockCollectible>(FindObjectsSortMode.None).Length >= maxDoorLocksInMap)
        {
            var others = new System.Collections.Generic.List<GameObject>();
            foreach (GameObject g in pool) if (g != null && g.GetComponent<DoorLockCollectible>() == null && g.GetComponent<FeatherCollectible>() == null && g.GetComponent<WhistleCollectible>() == null) others.Add(g);
            if (others.Count == 0) return;
            prefab = others[Random.Range(0, others.Count)];
        }

        // limit ice guns the same way
        if (prefab.GetComponent<IceGunCollectible>() != null &&
            FindObjectsByType<IceGunCollectible>(FindObjectsSortMode.None).Length >= maxIceGunsInMap)
        {
            var others = new System.Collections.Generic.List<GameObject>();
            foreach (GameObject g in pool) if (g != null && g.GetComponent<IceGunCollectible>() == null && g.GetComponent<DoorLockCollectible>() == null && g.GetComponent<FeatherCollectible>() == null && g.GetComponent<WhistleCollectible>() == null) others.Add(g);
            if (others.Count == 0) return;
            prefab = others[Random.Range(0, others.Count)];
        }

        Vector3 spawnPos;
        if (!PickSpawnPosition(out spawnPos)) return;

        GameObject item = Instantiate(prefab, spawnPos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        if (BlackoutRound.ThisRound) BlackoutRound.AddGlow(item); // faint glow so it can be found in the dark
        if (instant)
        {
            int layer = LayerMask.NameToLayer(instantLayerName);
            if (layer >= 0) foreach (Transform t in item.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }
    }

    // A random free spot, preferring points far from the items already lying around so they stay spread out.
    // Free = no item within itemSpacing, nobody standing next to it, not one of the last few points used.
    bool PickSpawnPosition(out Vector3 pos)
    {
        pos = Vector3.zero;
        if (spawnPoints == null || spawnPoints.Length == 0) return false;

        var items = new System.Collections.Generic.List<Vector3>();
        foreach (GameObject st in GameObject.FindGameObjectsWithTag("CollectibleStone")) items.Add(st.transform.position);
        foreach (PepperCollectible c in FindObjectsByType<PepperCollectible>(FindObjectsSortMode.None)) items.Add(c.transform.position);
        foreach (WhistleCollectible c in FindObjectsByType<WhistleCollectible>(FindObjectsSortMode.None)) items.Add(c.transform.position);
        foreach (FeatherCollectible c in FindObjectsByType<FeatherCollectible>(FindObjectsSortMode.None)) items.Add(c.transform.position);
        foreach (DoorLockCollectible c in FindObjectsByType<DoorLockCollectible>(FindObjectsSortMode.None)) items.Add(c.transform.position);
        foreach (IceGunCollectible c in FindObjectsByType<IceGunCollectible>(FindObjectsSortMode.None)) items.Add(c.transform.position);
        foreach (GoldenKnifeCollectible c in FindObjectsByType<GoldenKnifeCollectible>(FindObjectsSortMode.None)) items.Add(c.transform.position); // never drop an item on the golden knife
        Hider[] players = FindObjectsByType<Hider>(FindObjectsSortMode.None);

        var free = new System.Collections.Generic.List<Transform>();
        var weights = new System.Collections.Generic.List<float>();
        // if the house is too full for the spacing, accept closer spots rather than spawning nothing
        for (float spacing = itemSpacing; spacing >= 2f && free.Count == 0; spacing -= 1f)
        {
            foreach (Transform p in spawnPoints)
            {
                if (p == null || recentPoints.Contains(p)) continue;
                if (EnvironmentalHazard.IsDangerous(p.position)) continue; // not into a sealed room
                float nearestItem = 99f;
                foreach (Vector3 it in items) nearestItem = Mathf.Min(nearestItem, Flat(it, p.position));
                if (nearestItem < spacing) continue;
                bool blocked = false;
                foreach (Hider h in players)
                    if (Vector3.Distance(h.transform.position, p.position) < 2.5f) { blocked = true; break; }
                if (blocked) continue;
                free.Add(p);
                // farther from the other items = more likely (squared, so it clearly favours spreading out)
                float w = items.Count == 0 ? 1f : Mathf.Min(nearestItem, 12f);
                weights.Add(w * w);
            }
        }
        if (free.Count == 0) return false;

        float total = 0f;
        foreach (float w in weights) total += w;
        float r = Random.value * total;
        Transform pick = free[free.Count - 1];
        for (int i = 0; i < free.Count; i++) { r -= weights[i]; if (r <= 0f) { pick = free[i]; break; } }

        recentPoints.Enqueue(pick);
        while (recentPoints.Count > Mathf.Min(rememberLastPoints, spawnPoints.Length - 1)) recentPoints.Dequeue();

        pos = pick.position; // exactly where the spawn point was placed
        return true;
    }

    static float Flat(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }
}
