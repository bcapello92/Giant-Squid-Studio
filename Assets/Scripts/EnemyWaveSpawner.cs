using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Game/Enemy Wave Spawner (Poisson)")]
public class EnemyWaveSpawnerPoisson : MonoBehaviour
{
    // -------- Enemy data --------
    [System.Serializable]
    public class EnemyData
    {
        public GameObject prefab;
        [Min(1)] public int difficultyCost = 1;
        [Range(0f, 1f)] public float weight = 1f;
    }

    // -------- Area mode --------
    public enum AreaMode { Circle, Rect }

    [Header("Area / Poisson Settings")]
    public AreaMode areaMode = AreaMode.Circle;
    public Transform roomCenter;                 // defaults to this.transform
    public float circleRadius = 8f;             // if Circle
    public Vector2 rectSize = new Vector2(16, 10); // if Rect (X=width, Y=height)
    [Tooltip("Minimum distance between spawn points (and roughly between enemies).")]
    public float poissonRadius = 2.0f;
    [Tooltip("How many attempts to find a valid neighbor per active point (Bridson k).")]
    [Range(10, 60)] public int samplesPerPoint = 30;
    [Tooltip("Maximum points to generate (0 = auto)")]
    public int maxPoissonPoints = 0;
    bool _hasStarted = false;

    [Header("Physics Avoidance")]
    [Tooltip("Layers to avoid when placing (e.g., Walls | Enemy | Player).")]
    public LayerMask avoidLayers;
    [Tooltip("Extra clearance against avoidLayers checks (added to poissonRadius/2).")]
    public float overlapPadding = 0.15f;

    [Header("Waves / Difficulty")]
    [Min(1)] public int roomDifficulty = 8;
    [Range(0.1f, 0.9f)] public float wave1BudgetPercent = 0.6f;
    public float timeBetweenWaves = 4f;

    [Header("Enemy Pool")]
    public List<EnemyData> enemyPool = new();

    [Header("Debug")]
    public bool logSpawns = false;
    public bool drawPoissonPoints = true;
    RoomManager roomManager;
    [Header("Safety")]
    [Tooltip("Hard cap per wave to avoid runaway spawns, even if budget is huge.")]
    public int maxSpawnPerWave = 50;

    // --- runtime
    List<Vector2> spawnPoints;
    int nextPointIndex;
    List<EnemyData> weightedPool;
    // Add this helper:
    void EnsureInitialized()
    {
        if (!roomCenter) roomCenter = transform;

        if (weightedPool == null)
            weightedPool = BuildWeightedList(enemyPool);

        if (spawnPoints == null)
            spawnPoints = GenerateSpawnField();

        if (spawnPoints.Count == 0)
        {
            Debug.LogWarning("[SpawnerPoisson] No spawn points generated. Falling back to room center.");
            spawnPoints.Add(roomCenter.position);
        }
    }

    void Start()
    {
        EnsureInitialized();
        if (!roomCenter) roomCenter = transform;

        weightedPool = BuildWeightedList(enemyPool);
        spawnPoints = GenerateSpawnField();

        if (spawnPoints.Count == 0)
        {
            Debug.LogWarning("[SpawnerPoisson] No spawn points generated. Falling back to room center.");
            spawnPoints.Add(roomCenter.position);
        }
        // 3) Run the two waves
       // StartCoroutine(RunTwoWaves());
    }

    IEnumerator RunTwoWaves()
    {
        int wave1 = Mathf.Max(1, Mathf.RoundToInt(roomDifficulty * wave1BudgetPercent));
        int wave2 = Mathf.Max(1, roomDifficulty - wave1);

        if (logSpawns) Debug.Log($"[SpawnerPoisson] Wave budgets: {wave1} / {wave2}");

        SpawnWave(wave1, 1);
        yield return new WaitForSeconds(timeBetweenWaves);
        SpawnWave(wave2, 2);
    }
    public void BeginSpawning()
    {
        if (_hasStarted) return;
        _hasStarted = true;

        EnsureInitialized();               // <— make sure spawnPoints exists
        StartCoroutine(RunTwoWaves());
    }

    public void SetDifficulty(int difficulty)
    {
        roomDifficulty = Mathf.Max(1, difficulty);
    }

    
    public void SetRoomManager(RoomManager rm)
    {
        roomManager = rm;
    }
    void SpawnWave(int budget, int waveIndex)
    {
        if (budget <= 0) return;
        if (enemyPool == null || enemyPool.Count == 0) return;

        int remaining = budget;
        int spawnedThisWave = 0;

        // build a list of enemies that can actually fit in this budget right now
        // (cost <= budget)
        var affordable = enemyPool.FindAll(e => e != null && e.prefab != null && e.difficultyCost <= budget);
        if (affordable.Count == 0)
        {
            if (logSpawns) Debug.LogWarning($"[SpawnerPoisson] {name} wave {waveIndex}: no affordable enemies for budget={budget}");
            return;
        }

        int safety = 200; // smaller, to make logs readable

        while (remaining > 0 && safety-- > 0)
        {
            // hard cap to prevent explosions
            if (spawnedThisWave >= maxSpawnPerWave)
            {
                if (logSpawns) Debug.LogWarning($"[SpawnerPoisson] {name} wave {waveIndex}: hit maxSpawnPerWave={maxSpawnPerWave}");
                break;
            }

            // pick a random affordable enemy
            var pick = affordable[Random.Range(0, affordable.Count)];

            // if for some reason pick is still too expensive, stop
            if (pick.difficultyCost > remaining)
            {
                // try to see if ANYTHING fits the *current* remaining
                var cheaper = affordable.FindAll(e => e.difficultyCost <= remaining);
                if (cheaper.Count == 0)
                    break; // nothing fits, end wave

                pick = cheaper[Random.Range(0, cheaper.Count)];
            }

            // spawn it
            var pos = GetNextFreeSpawnPosition();
            var go = Instantiate(pick.prefab, pos, Quaternion.identity);
            spawnedThisWave++;
            remaining -= pick.difficultyCost;  // <-- ACTUALLY spend budget

            // register with room
            if (go != null && roomManager != null)
            {
                var re = go.GetComponent<RoomEnemy>();
                if (re != null)
                {
                    re.SetRoomManager(roomManager);
                    roomManager.RegisterEnemy(re);
                }
            }

            if (logSpawns)
                Debug.Log($"[SpawnerPoisson] {name} wave {waveIndex}: {pick.prefab.name} @ {pos}  (rem {remaining})");
        }

        if (logSpawns)
            Debug.Log($"[SpawnerPoisson] {name} wave {waveIndex} done. spawned={spawnedThisWave}, leftover={remaining}");
    }


    Vector3 GetNextFreeSpawnPosition()
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            // last-ditch init + fallback
            EnsureInitialized();
            return roomCenter ? roomCenter.position : transform.position;
        }

        int tries = spawnPoints.Count;
        while (tries-- > 0)
        {
            var p = spawnPoints[nextPointIndex];
            nextPointIndex = (nextPointIndex + 1) % spawnPoints.Count;

            if (IsFreeWorld(p))
                return p;
        }

        // If all are blocked, jitter around room center as fallback
        for (int i = 0; i < 50; i++)
        {
            var off = Random.insideUnitCircle * Mathf.Max(1f, poissonRadius);
            var cand = (Vector2)roomCenter.position + off;
            if (IsFreeWorld(cand)) return cand;
        }
        return roomCenter.position;
    }

    bool IsFreeWorld(Vector2 worldPos)
    {
        float r = Mathf.Max(poissonRadius * 0.5f, 0.2f) + Mathf.Max(0f, overlapPadding);
        if (avoidLayers.value == 0) return true; // no avoidance requested
        var hit = Physics2D.OverlapCircle(worldPos, r, avoidLayers);
        return hit == null;
    }

    // --------- Build Poisson field ----------
    List<Vector2> GenerateSpawnField()
    {
        var pointsLocal = Poisson.Bridson(
            areaMode,
            areaMode == AreaMode.Circle ? circleRadius : 0f,
            areaMode == AreaMode.Rect ? rectSize : Vector2.zero,
            poissonRadius,
            samplesPerPoint,
            maxPoissonPoints
        );

        // Convert local -> world around roomCenter, then filter by physics if requested
        var list = new List<Vector2>(pointsLocal.Count);
        foreach (var local in pointsLocal)
        {
            var world = (Vector2)roomCenter.position + local;
            if (IsFreeWorld(world))
                list.Add(world);
        }
        return list;
    }

    // --------- Weighted selection ----------
    List<EnemyData> BuildWeightedList(List<EnemyData> pool)
    {
        var list = new List<EnemyData>();
        foreach (var e in pool)
        {
            if (e == null || e.prefab == null) continue;
            int count = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(0.01f, e.weight) * 10f), 1, 50);
            for (int i = 0; i < count; i++) list.Add(e);
        }
        return list.Count > 0 ? list : pool;
    }

    EnemyData PickEnemy(List<EnemyData> weighted)
    {
        if (weighted == null || weighted.Count == 0) return null;
        return weighted[Random.Range(0, weighted.Count)];
    }

    // --------- Gizmos ----------
    void OnDrawGizmosSelected()
    {
        var center = roomCenter ? roomCenter.position : transform.position;

        Gizmos.color = new Color(0.2f, 1f, 0.9f, 0.25f);
        if (areaMode == AreaMode.Circle) Gizmos.DrawWireSphere(center, circleRadius);
        else Gizmos.DrawWireCube(center, new Vector3(rectSize.x, rectSize.y, 0f));

        if (drawPoissonPoints && spawnPoints != null)
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.7f);
            foreach (var p in spawnPoints)
                Gizmos.DrawSphere(p, 0.08f);
        }
    }

    // ============================================================
    //               Poisson-disc sampler (Bridson)
    // ============================================================
    static class Poisson
    {
        public static List<Vector2> Bridson(AreaMode mode, float radiusCircle, Vector2 rect, float r, int k, int maxPoints)
        {
            // Acceleration grid size
            float cell = r / Mathf.Sqrt(2f);

            // Bounds & mapping funcs in local space
            Rect bounds = new Rect();
            Vector2 center = Vector2.zero;

            if (mode == AreaMode.Circle)
            {
                bounds = new Rect(-radiusCircle, -radiusCircle, radiusCircle * 2f, radiusCircle * 2f);
            }
            else
            {
                bounds = new Rect(-rect.x * 0.5f, -rect.y * 0.5f, rect.x, rect.y);
            }

            // Grid
            int gw = Mathf.CeilToInt(bounds.width / cell);
            int gh = Mathf.CeilToInt(bounds.height / cell);
            var grid = new int[gw * gh];
            for (int i = 0; i < grid.Length; i++) grid[i] = -1;

            var points = new List<Vector2>();
            var active = new List<int>();

            // Helper: inside area?
            bool Inside(Vector2 p)
            {
                if (!bounds.Contains(p)) return false;
                if (mode == AreaMode.Circle)
                    return (p - center).sqrMagnitude <= radiusCircle * radiusCircle;
                return true;
            }

            // Helper: grid index
            Vector2Int GridCoord(Vector2 p)
            {
                int gx = Mathf.Clamp((int)((p.x - bounds.xMin) / cell), 0, gw - 1);
                int gy = Mathf.Clamp((int)((p.y - bounds.yMin) / cell), 0, gh - 1);
                return new Vector2Int(gx, gy);
            }

            // Place first point
            for (int safety = 0; safety < 50; safety++)
            {
                var first = new Vector2(
                    Random.Range(bounds.xMin, bounds.xMax),
                    Random.Range(bounds.yMin, bounds.yMax)
                );
                if (!Inside(first)) continue;

                points.Add(first);
                var gc = GridCoord(first);
                grid[gc.x + gc.y * gw] = 0;
                active.Add(0);
                break;
            }
            if (points.Count == 0) return points;

            // Main loop
            while (active.Count > 0)
            {
                int ai = active[Random.Range(0, active.Count)];
                var baseP = points[ai];
                bool found = false;

                for (int i = 0; i < k; i++)
                {
                    float ang = Random.value * Mathf.PI * 2f;
                    float rad = Random.Range(r, 2f * r);
                    var cand = baseP + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;

                    if (!Inside(cand)) continue;

                    var gc = GridCoord(cand);
                    bool ok = true;

                    // Check neighboring cells
                    for (int ny = -2; ny <= 2 && ok; ny++)
                        for (int nx = -2; nx <= 2 && ok; nx++)
                        {
                            int cx = gc.x + nx;
                            int cy = gc.y + ny;
                            if (cx < 0 || cy < 0 || cx >= gw || cy >= gh) continue;
                            int idx = grid[cx + cy * gw];
                            if (idx >= 0)
                            {
                                float d2 = (points[idx] - cand).sqrMagnitude;
                                if (d2 < r * r) ok = false;
                            }
                        }

                    if (ok)
                    {
                        points.Add(cand);
                        grid[gc.x + gc.y * gw] = points.Count - 1;
                        active.Add(points.Count - 1);
                        found = true;

                        if (maxPoints > 0 && points.Count >= maxPoints)
                            active.Clear(); // force exit

                        break;
                    }
                }

                if (!found)
                {
                    // retire this active point
                    active.Remove(ai);
                }
            }

            return points;
        }
    }
}
