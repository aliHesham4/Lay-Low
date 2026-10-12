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
    public int maxStonesInMap = 3;

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

        Transform randomPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
        Instantiate(stonePrefab, randomPoint.position, Quaternion.identity);
    }
}