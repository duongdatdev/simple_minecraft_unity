using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ZombieSpawner : MonoBehaviour
{
    [Header("Settings")]
    public GameObject zombiePrefab;
    public GameObject pigPrefab; // Add Pig Prefab
    public int maxZombies = 10;
    public int maxPigs = 5; // Max pigs
    public float spawnInterval = 5f;
    public float minSpawnDistance = 20f;
    public float maxSpawnDistance = 50f;
    
    [Header("References")]
    public Transform player;

    private List<GameObject> spawnedZombies = new List<GameObject>();
    private List<GameObject> spawnedPigs = new List<GameObject>();
    private float spawnTimer;

    private void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        // Try to find zombie prefab if not assigned
        if (zombiePrefab == null)
        {
            // This is a fallback, user should assign it
            Debug.LogWarning("ZombieSpawner: Zombie Prefab not assigned! Please assign it in the Inspector.");
        }
    }

    private void Update()
    {
        if (player == null) return;

        // Cleanup dead entities
        CleanupList(spawnedZombies);
        CleanupList(spawnedPigs);

        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnInterval)
        {
            spawnTimer = 0;
            if (spawnedZombies.Count < maxZombies && zombiePrefab != null)
            {
                SpawnEntity(zombiePrefab, spawnedZombies);
            }
            if (spawnedPigs.Count < maxPigs && pigPrefab != null)
            {
                SpawnEntity(pigPrefab, spawnedPigs);
            }
        }
    }

    void CleanupList(List<GameObject> list)
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i] == null)
            {
                list.RemoveAt(i);
            }
        }
    }

    void SpawnEntity(GameObject prefab, List<GameObject> list)
    {
        Vector2 randomCircle = Random.insideUnitCircle.normalized * Random.Range(minSpawnDistance, maxSpawnDistance);
        Vector3 spawnPos = player.position + new Vector3(randomCircle.x, 0, randomCircle.y);
        
        // Find ground height using Raycast
        if (Physics.Raycast(new Vector3(spawnPos.x, 256, spawnPos.z), Vector3.down, out RaycastHit hit, 300f))
        {
            spawnPos.y = hit.point.y + 1f;
            GameObject entity = Instantiate(prefab, spawnPos, Quaternion.identity);
            list.Add(entity);
        }
    }
}
