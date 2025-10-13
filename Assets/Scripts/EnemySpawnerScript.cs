using UnityEngine;
using System.Collections.Generic;
using System.Collections;

[System.Serializable]
public class EnemyData : MonoBehaviour
{
    public GameObject prefab;
    public int difficultyCost = 1;
}

public class EnemyWaveSpawner : MonoBehaviour
{
    [Header("Room Settings")]
    public int roomDifficulty = 6;
    public float spawnRadius = 5f;
    public Transform roomCenter;

    [Header("Waves")]
    public float waveDelay = 5f;
    public int waveCount = 2;

    [Header("Enemy Pool")]
    public List<EnemyData> enemyPool;

    [Header("Spawn Points (Optional)")]
    public List<Transform> customSpawnPoints;

    private int currentWave = 0;

    void Start()
    {
        if (roomCenter == null) roomCenter = transform;
        StartCoroutine(SpawnRoomWaves());
    }

    IEnumerator SpawnRoomWaves()
    {
        for (int i = 0; i < waveCount; i++)
        {
            currentWave = i + 1;
            Debug.Log($"[Spawner] Starting Wave {currentWave}");

            SpawnWaveEnemies(roomDifficulty);

            // Wait for the next wave
            if (i < waveCount - 1)
                yield return new WaitForSeconds(waveDelay);
        }
    }

    void SpawnWaveEnemies(int difficultyBudget)
    {
        int remaining = difficultyBudget;

        while (remaining > 0 && enemyPool.Count > 0)
        {
            EnemyData candidate = enemyPool[Random.Range(0, enemyPool.Count)];

            if (candidate.difficultyCost <= remaining)
            {
                SpawnEnemy(candidate.prefab);
                remaining -= candidate.difficultyCost;
            }
            else
            {
                // If no enemy fits remaining budget, break to prevent infinite loop
                if (!enemyPool.Exists(e => e.difficultyCost <= remaining))
                    break;
            }
        }

        Debug.Log($"[Spawner] Wave {currentWave} complete. Total difficulty used: {difficultyBudget - remaining}");
    }

    void SpawnEnemy(GameObject prefab)
    {
        Vector3 spawnPos;

        if (customSpawnPoints != null && customSpawnPoints.Count > 0)
            spawnPos = customSpawnPoints[Random.Range(0, customSpawnPoints.Count)].position;
        else
        {
            // Random point within radius
            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            spawnPos = roomCenter.position + new Vector3(offset.x, offset.y, 0);
        }

        Instantiate(prefab, spawnPos, Quaternion.identity);
    }
}
