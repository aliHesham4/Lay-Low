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
    [Tooltip("Layer given to instants so they are easy to recognise.")]
    public string instantLayerName = "Pepper";

    private Transform lastPoint;

    void Start()
    {
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
               FindObjectsByType<FeatherCollectible>(FindObjectsSortMode.None).Length;
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

        Transform point = PickFreePoint();
        if (point == null) return;
        lastPoint = point;

        GameObject item = Instantiate(prefab, point.position, Quaternion.identity);
        if (instant)
        {
            int layer = LayerMask.NameToLayer(instantLayerName);
            if (layer >= 0) foreach (Transform t in item.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }
    }

    // A random spawn point that is free: no item already there, nobody standing next to it, not the last one used
    Transform PickFreePoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return null;
        GameObject[] stones = GameObject.FindGameObjectsWithTag("CollectibleStone");
        PepperCollectible[] peppers = FindObjectsByType<PepperCollectible>(FindObjectsSortMode.None);
        WhistleCollectible[] whistles = FindObjectsByType<WhistleCollectible>(FindObjectsSortMode.None);
        FeatherCollectible[] feathers = FindObjectsByType<FeatherCollectible>(FindObjectsSortMode.None);
        Hider[] players = FindObjectsByType<Hider>(FindObjectsSortMode.None);
        var free = new System.Collections.Generic.List<Transform>();
        foreach (Transform p in spawnPoints)
        {
            if (p == null || p == lastPoint) continue;
            bool blocked = false;
            foreach (GameObject st in stones)
                if (Vector3.Distance(st.transform.position, p.position) < 2f) { blocked = true; break; }
            if (!blocked)
                foreach (PepperCollectible pc in peppers)
                    if (Vector3.Distance(pc.transform.position, p.position) < 2f) { blocked = true; break; }
            if (!blocked)
                foreach (WhistleCollectible wc in whistles)
                    if (Vector3.Distance(wc.transform.position, p.position) < 2f) { blocked = true; break; }
            if (!blocked)
                foreach (FeatherCollectible fc in feathers)
                    if (Vector3.Distance(fc.transform.position, p.position) < 2f) { blocked = true; break; }
            if (!blocked)
                foreach (Hider h in players)
                    if (Vector3.Distance(h.transform.position, p.position) < 2.5f) { blocked = true; break; }
            if (!blocked) free.Add(p);
        }
        if (free.Count == 0) return null;
        return free[Random.Range(0, free.Count)];
    }
}
