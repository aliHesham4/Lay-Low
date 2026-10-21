using System.Collections;
using UnityEngine;

public class ItemSpawner : MonoBehaviour
{
    [Header("Spawner Setup")]
    public GameObject stonePrefab;
    public Transform[] spawnPoints;

    [Header("Timing Settings")]
    [Tooltip("Minimum time (in seconds) before a new stone spawns.")]
    public float minSpawnTime = 15f;
    [Tooltip("Maximum time (in seconds) before a new stone spawns.")]
    public float maxSpawnTime = 25f;

    [Header("Limits")]
    [Tooltip("Stop spawning if there are already this many stones sitting on the map.")]
    public int maxStonesInMap = 2;

    void Start()
    {
        // Start the infinite spawning loop as soon as the game begins
        StartCoroutine(SpawnRoutine());
    }

    IEnumerator SpawnRoutine()
    {
        while (true)
        {
            // 1. Pick a random wait time (e.g., between 15 and 25 seconds)
            float waitTime = Random.Range(minSpawnTime, maxSpawnTime);

            // 2. Pause this script for that amount of time
            yield return new WaitForSeconds(waitTime);

            // 3. Count how many stones are currently on the map
            // Note: Your stone prefab MUST have the tag "CollectibleStone" for this to work
            GameObject[] existingStones = GameObject.FindGameObjectsWithTag("CollectibleStone");

            // 4. If we haven't hit the limit, spawn a new one
            if (existingStones.Length < maxStonesInMap)
            {
                SpawnStone();
            }
        }
    }

    void SpawnStone()
    {
        if (stonePrefab == null || spawnPoints.Length == 0) return;

        // Pick a random spawn point that is free: not where a stone already lies, not right next to a
        // player, and not the same one as last time.
        GameObject[] stones = GameObject.FindGameObjectsWithTag("CollectibleStone");
        Hider[] players = FindObjectsByType<Hider>(FindObjectsSortMode.None);
        var free = new System.Collections.Generic.List<Transform>();
        foreach (Transform p in spawnPoints)
        {
            if (p == null || p == lastPoint) continue;
            bool blocked = false;
            foreach (GameObject st in stones)
                if (Vector3.Distance(st.transform.position, p.position) < 2f) { blocked = true; break; }
            if (!blocked)
                foreach (Hider h in players)
                    if (Vector3.Distance(h.transform.position, p.position) < 2.5f) { blocked = true; break; }
            if (!blocked) free.Add(p);
        }
        if (free.Count == 0) return;

        Transform randomPoint = free[Random.Range(0, free.Count)];
        lastPoint = randomPoint;
        Instantiate(stonePrefab, randomPoint.position, Quaternion.identity);
    }

    private Transform lastPoint;
}