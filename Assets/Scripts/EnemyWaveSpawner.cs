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

    // --- HAZARDS ---
    [System.Serializable]
    public class HazardData
    {
        public GameObject prefab;
        [Range(0f, 1f)] public float weight = 1f;
    }

    // -------- Area mode --------
    public enum AreaMode { Circle, Rect, Polygon }

    [Header("Area / Poisson Settings")]
    public AreaMode areaMode = AreaMode.Circle;
    public Transform roomCenter;                 // defaults to this.transform
    public float circleRadius = 8f;             // if Circle
    public Vector2 rectSize = new Vector2(16, 10); // if Rect (X=width, Y=height)

    [Tooltip("Only used if AreaMode = Polygon. Points in world space defining the spawn polygon (clockwise or CCW).")]
    public List<Transform> polygonPoints = new();

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

    // --- HAZARDS ---
    [Header("Hazard Pool")]
    [Tooltip("Random hazards to place at room start. Weighted by 'weight'.")]
    public List<HazardData> hazardPool = new();
    [Tooltip("Maximum hazards to place in a room.")]
    [Min(0)] public int maxHazardsPerRoom = 4;
    [Tooltip("Spawn hazards immediately when BeginSpawning() is called.")]
    public bool spawnHazardsAtStart = true;

    [Header("Hazard Spacing")]
    [Tooltip("Minimum distance between hazard spawn positions.")]
    public float hazardMinDistance = 4f;

    [Header("Hazard Rotation")]
    [Tooltip("Randomize only turret hazards (objects with BurstTurret component).")]
    public bool randomizeTurretRotation = true;
    [Tooltip("Random Z-angle range for turret hazards (degrees).")]
    public float turretMinAngle = 0f;
    public float turretMaxAngle = 360f;

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
    List<HazardData> weightedHazards;

    int hazardsSpawnedThisRoom = 0;
    List<Vector2> hazardPositions = new List<Vector2>(); // track placed hazards

    // ------------------------------------------------------
    // Init
    // ------------------------------------------------------
    void EnsureInitialized()
    {
        if (!roomCenter) roomCenter = transform;

        if (weightedPool == null)
            weightedPool = BuildWeightedList(enemyPool);

        if (weightedHazards == null)
            weightedHazards = BuildWeightedList(hazardPool);

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
        weightedHazards = BuildWeightedList(hazardPool);
        spawnPoints = GenerateSpawnField();

        if (spawnPoints.Count == 0)
        {
            Debug.LogWarning("[SpawnerPoisson] No spawn points generated. Falling back to room center.");
            spawnPoints.Add(roomCenter.position);
        }
    }

    // ------------------------------------------------------
    // Waves
    // ------------------------------------------------------
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

        EnsureInitialized();

        // Hazards spawn first
        if (spawnHazardsAtStart)
            SpawnRandomHazards();

        // Then waves
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

    // ------------------------------------------------------
    // Hazards
    // ------------------------------------------------------
    void SpawnRandomHazards()
    {
        if (hazardPool == null || hazardPool.Count == 0 || maxHazardsPerRoom <= 0)
            return;

        hazardsSpawnedThisRoom = 0;
        hazardPositions.Clear();

        var usable = hazardPool.FindAll(h => h != null && h.prefab != null);
        if (usable.Count == 0) return;

        weightedHazards = BuildWeightedList(usable);

        int safety = 200;
        while (hazardsSpawnedThisRoom < maxHazardsPerRoom && safety-- > 0)
        {
            var pick = PickHazard(weightedHazards);
            if (pick == null || pick.prefab == null) break;

            var pos = GetNextHazardSpawnPosition();

            // Instantiate with prefab's default rotation
            var go = Instantiate(pick.prefab, pos, pick.prefab.transform.rotation);
            hazardsSpawnedThisRoom++;

            // Only rotate turret hazards (objects with BurstTurret)
            if (randomizeTurretRotation && go != null && go.GetComponent<BurstTurret>() != null)
            {
                float angle = Random.Range(turretMinAngle, turretMaxAngle);
                go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }

            if (logSpawns)
                Debug.Log($"[SpawnerPoisson] {name} hazard: {pick.prefab.name} @ {pos}  ({hazardsSpawnedThisRoom}/{maxHazardsPerRoom})");
        }

        if (logSpawns)
            Debug.Log($"[SpawnerPoisson] {name} hazards done. spawned={hazardsSpawnedThisRoom}");
    }

    Vector3 GetNextHazardSpawnPosition()
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            EnsureInitialized();
            return roomCenter ? roomCenter.position : transform.position;
        }

        float minDist = Mathf.Max(0f, hazardMinDistance);
        float minDistSqr = minDist * minDist;

        int tries = spawnPoints.Count;
        while (tries-- > 0)
        {
            var p = spawnPoints[nextPointIndex];
            nextPointIndex = (nextPointIndex + 1) % spawnPoints.Count;

            if (!IsFreeWorld(p))
                continue;

            bool farEnough = true;
            for (int i = 0; i < hazardPositions.Count; i++)
            {
                if ((hazardPositions[i] - p).sqrMagnitude < minDistSqr)
                {
                    farEnough = false;
                    break;
                }
            }

            if (farEnough)
            {
                hazardPositions.Add(p);
                return p;
            }
        }

        // fallback if no well-spaced point found
        var fallback = GetNextFreeSpawnPosition();
        hazardPositions.Add(fallback);
        return fallback;
    }

    // ------------------------------------------------------
    // Enemies
    // ------------------------------------------------------
    void SpawnWave(int budget, int waveIndex)
    {
        if (budget <= 0) return;
        if (enemyPool == null || enemyPool.Count == 0) return;

        int remaining = budget;
        int spawnedThisWave = 0;

        var affordable = enemyPool.FindAll(e => e != null && e.prefab != null && e.difficultyCost <= budget);
        if (affordable.Count == 0)
        {
            if (logSpawns) Debug.LogWarning($"[SpawnerPoisson] {name} wave {waveIndex}: no affordable enemies for budget={budget}");
            return;
        }

        int safety = 200;

        while (remaining > 0 && safety-- > 0)
        {
            if (spawnedThisWave >= maxSpawnPerWave)
            {
                if (logSpawns) Debug.LogWarning($"[SpawnerPoisson] {name} wave {waveIndex}: hit maxSpawnPerWave={maxSpawnPerWave}");
                break;
            }

            var pick = affordable[Random.Range(0, affordable.Count)];

            if (pick.difficultyCost > remaining)
            {
                var cheaper = affordable.FindAll(e => e.difficultyCost <= remaining);
                if (cheaper.Count == 0)
                    break;

                pick = cheaper[Random.Range(0, cheaper.Count)];
            }

            var pos = GetNextFreeSpawnPosition();
            var go = Instantiate(pick.prefab, pos, pick.prefab.transform.rotation);
            spawnedThisWave++;
            remaining -= pick.difficultyCost;

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

        // jitter around center as fallback
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

    // ------------------------------------------------------
    // Build Poisson field
    // ------------------------------------------------------
    List<Vector2> GenerateSpawnField()
    {
        List<Vector2> pointsLocal;

        if (areaMode == AreaMode.Polygon)
        {
            if (!roomCenter) roomCenter = transform;

            var polyLocal = new List<Vector2>();
            foreach (var t in polygonPoints)
            {
                if (!t) continue;
                Vector2 local = roomCenter.InverseTransformPoint(t.position);
                polyLocal.Add(local);
            }

            if (polyLocal.Count < 3)
            {
                Debug.LogWarning("[SpawnerPoisson] Polygon mode selected but polygonPoints has < 3 valid points. Falling back to circle.");
                pointsLocal = Poisson.Bridson(
                    AreaMode.Circle,
                    circleRadius,
                    Vector2.zero,
                    poissonRadius,
                    samplesPerPoint,
                    maxPoissonPoints,
                    null
                );
            }
            else
            {
                pointsLocal = Poisson.Bridson(
                    AreaMode.Polygon,
                    0f,
                    Vector2.zero,
                    poissonRadius,
                    samplesPerPoint,
                    maxPoissonPoints,
                    polyLocal
                );
            }
        }
        else
        {
            pointsLocal = Poisson.Bridson(
                areaMode,
                areaMode == AreaMode.Circle ? circleRadius : 0f,
                areaMode == AreaMode.Rect ? rectSize : Vector2.zero,
                poissonRadius,
                samplesPerPoint,
                maxPoissonPoints,
                null
            );
        }

        if (!roomCenter) roomCenter = transform;

        var list = new List<Vector2>(pointsLocal.Count);
        foreach (var local in pointsLocal)
        {
            var world = (Vector2)roomCenter.position + local;
            if (IsFreeWorld(world))
                list.Add(world);
        }
        return list;
    }

    // ------------------------------------------------------
    // Weighted lists
    // ------------------------------------------------------
    List<HazardData> BuildWeightedList(List<HazardData> pool)
    {
        var list = new List<HazardData>();
        foreach (var h in pool)
        {
            if (h == null || h.prefab == null) continue;
            int count = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(0.01f, h.weight) * 10f), 1, 50);
            for (int i = 0; i < count; i++) list.Add(h);
        }
        return list.Count > 0 ? list : pool;
    }

    HazardData PickHazard(List<HazardData> weighted)
    {
        if (weighted == null || weighted.Count == 0) return null;
        return weighted[Random.Range(0, weighted.Count)];
    }

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

    // ------------------------------------------------------
    // Gizmos
    // ------------------------------------------------------
    void OnDrawGizmosSelected()
    {
        var center = roomCenter ? roomCenter.position : transform.position;

        if (areaMode == AreaMode.Circle)
        {
            Gizmos.color = new Color(0.2f, 1f, 0.9f, 0.25f);
            Gizmos.DrawWireSphere(center, circleRadius);
        }
        else if (areaMode == AreaMode.Rect)
        {
            Gizmos.color = new Color(0.2f, 1f, 0.9f, 0.25f);
            Gizmos.DrawWireCube(center, new Vector3(rectSize.x, rectSize.y, 0f));
        }
        else if (areaMode == AreaMode.Polygon)
        {
            Gizmos.color = new Color(0.2f, 1f, 0.9f, 0.6f);
            if (polygonPoints != null && polygonPoints.Count >= 2)
            {
                for (int i = 0; i < polygonPoints.Count; i++)
                {
                    var a = polygonPoints[i];
                    var b = polygonPoints[(i + 1) % polygonPoints.Count];
                    if (!a || !b) continue;
                    Gizmos.DrawLine(a.position, b.position);
                }
            }
        }

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
        public static List<Vector2> Bridson(
            AreaMode mode,
            float radiusCircle,
            Vector2 rect,
            float r,
            int k,
            int maxPoints,
            List<Vector2> polygon // null unless Polygon mode
        )
        {
            float cell = r / Mathf.Sqrt(2f);

            Rect bounds = new Rect();
            Vector2 center = Vector2.zero;

            if (mode == AreaMode.Circle)
            {
                bounds = new Rect(-radiusCircle, -radiusCircle, radiusCircle * 2f, radiusCircle * 2f);
            }
            else if (mode == AreaMode.Rect)
            {
                bounds = new Rect(-rect.x * 0.5f, -rect.y * 0.5f, rect.x, rect.y);
            }
            else if (mode == AreaMode.Polygon)
            {
                if (polygon == null || polygon.Count < 3)
                    return new List<Vector2>();

                float minX = polygon[0].x;
                float maxX = polygon[0].x;
                float minY = polygon[0].y;
                float maxY = polygon[0].y;
                for (int i = 1; i < polygon.Count; i++)
                {
                    var p = polygon[i];
                    if (p.x < minX) minX = p.x;
                    if (p.x > maxX) maxX = p.x;
                    if (p.y < minY) minY = p.y;
                    if (p.y > maxY) maxY = p.y;
                }
                bounds = new Rect(minX, minY, maxX - minX, maxY - minY);
            }

            int gw = Mathf.CeilToInt(bounds.width / cell);
            int gh = Mathf.CeilToInt(bounds.height / cell);
            var grid = new int[gw * gh];
            for (int i = 0; i < grid.Length; i++) grid[i] = -1;

            var points = new List<Vector2>();
            var active = new List<int>();

            bool Inside(Vector2 p)
            {
                if (!bounds.Contains(p)) return false;

                if (mode == AreaMode.Circle)
                    return (p - center).sqrMagnitude <= radiusCircle * radiusCircle;

                if (mode == AreaMode.Rect)
                    return true;

                if (mode == AreaMode.Polygon)
                    return PointInPolygon(p, polygon);

                return true;
            }

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
                            active.Clear();

                        break;
                    }
                }

                if (!found)
                {
                    active.Remove(ai);
                }
            }

            return points;
        }

        // Ray-cast point-in-polygon
        static bool PointInPolygon(Vector2 p, List<Vector2> poly)
        {
            bool inside = false;
            int count = poly.Count;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                Vector2 pi = poly[i];
                Vector2 pj = poly[j];

                bool intersect =
                    ((pi.y > p.y) != (pj.y > p.y)) &&
                    (p.x < (pj.x - pi.x) * (p.y - pi.y) / (pj.y - pi.y + Mathf.Epsilon) + pi.x);

                if (intersect) inside = !inside;
            }
            return inside;
        }
    }
}
