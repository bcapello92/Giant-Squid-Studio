using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RunManager : MonoBehaviour
{
    public static RunManager I { get; private set; }

    // ========= CONFIG =========

    [Header("Levels / Progression")]
    public int finalLevel = 2;   // last level in the game

    [Header("Rooms (Default / Legacy)")]
    public RoomSequence roomSequence;       // fallback for level 1 if level1Rooms is empty
    public bool loopAndReshuffle = true;

    [Header("Boss Flow (Level 1)")]
    [Min(1)] public int roomsUntilBoss = 4;       // boss after N normal rooms on level 1
    public GameObject bossRoomPrefab;             // level 1 boss room

    [Header("Boss Flow (Level 2)")]
    public RoomSequence secondRunRoomSequence;    // optional alternative room set for level 2
    [Min(1)] public int secondRunRoomsUntilBoss = 2;
    public GameObject secondBossRoomPrefab;       // level 2 boss room

    [Header("Difficulty Curve")]
    [Min(1)] public int difficultyBase = 1;
    [Min(0)] public int difficultyPerRoom = 1;

    [Header("Player State (Persistent for the run)")]
    public int maxHP = 100;
    public int currentHP = 100;
    public List<string> buffs = new List<string>();

    [Header("UI (optional)")]
    public GameObject gameOverUIPrefab;     // Game Over (lose)
    public GameObject youWinUIPrefab;       // You Win (boss defeated)

    [Header("Scenes")]
    public string level1SceneName = "Level1";   // set in Inspector
    public string level2SceneName = "Level2";   // set in Inspector

    [Header("Room Sequences per Level")]
    public RoomSequence level1Rooms;
    public RoomSequence level2Rooms;

    [Header("Level State")]
    public int currentLevel = 1;

    // ========= RUNTIME STATE =========

    List<GameObject> runRooms = new();
    int currentIndex = 0;
    int loopCount = 0;
    int roomsClearedThisRun = 0;
    bool inBossRoom = false;

    // NOTE: This property name is from your original script and actually
    // represents loopCount + 1, not the level index.
    public int CurrentLevel => loopCount + 1;

    RoomManager rm;
    Coroutine _boot;
    int _runVersion = 0;

    TestPlayerController _player;

    public System.Action<bool> RunActiveChanged;
    public bool runActive { get; private set; } = false;
    public bool IsRunActive => runActive;

    // ========= SINGLETON SETUP =========

    void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }
        I = this;
        DontDestroyOnLoad(gameObject);

        Debug.Log($"[Run] Awake. bossRoomPrefab = {bossRoomPrefab?.name ?? "null"}, " +
                  $"secondBossRoomPrefab = {secondBossRoomPrefab?.name ?? "null"}");
    }

    void Start()
    {
        // Start runs via StartRunAtLevel / StartNewRun / StartSecondRun
    }

    // ========= PER-LEVEL HELPERS =========

    RoomSequence GetRoomSequenceForLevel(int level)
    {
        switch (level)
        {
            case 1:
                // Use level1Rooms if set, otherwise fallback to legacy roomSequence
                if (level1Rooms != null && level1Rooms.rooms != null && level1Rooms.rooms.Length > 0)
                    return level1Rooms;
                return roomSequence;

            case 2:
                // Prefer level2Rooms; fallback to secondRunRoomSequence if provided
                if (level2Rooms != null && level2Rooms.rooms != null && level2Rooms.rooms.Length > 0)
                    return level2Rooms;
                return secondRunRoomSequence;

            default:
                return null;
        }
    }

    string GetSceneNameForLevel(int level)
    {
        switch (level)
        {
            case 1: return level1SceneName;
            case 2: return level2SceneName;
            default: return null;
        }
    }

    int GetRoomsUntilBossForCurrentLevel()
    {
        if (currentLevel == 2)
            return Mathf.Max(1, secondRunRoomsUntilBoss);

        // default / level 1
        return Mathf.Max(1, roomsUntilBoss);
    }

    GameObject GetBossPrefabForCurrentLevel()
    {
        if (currentLevel == 2)
            return secondBossRoomPrefab;

        return bossRoomPrefab;
    }

    // ========= RUN LIFECYCLE =========

    /// <summary>
    /// Start a fresh run at a given level (1 or 2).
    /// Resets HP and buffs for a new run.
    /// </summary>
    public void StartRunAtLevel(int level)
    {
        _runVersion++;
        if (_boot != null)
        {
            StopCoroutine(_boot);
            _boot = null;
        }

        runActive = true;
        RunActiveChanged?.Invoke(true);

        currentLevel = level;

        // New run → reset HP and buffs
        currentHP = maxHP;
        buffs?.Clear();

        roomsClearedThisRun = 0;
        inBossRoom = false;
        loopCount = 0;
        currentIndex = 0;

        if (runRooms == null) runRooms = new List<GameObject>();
        runRooms.Clear();

        // Get rooms & scene for this level
        RoomSequence seq = GetRoomSequenceForLevel(level);
        if (seq == null || seq.rooms == null || seq.rooms.Length == 0)
        {
            Debug.LogError($"[Run] No rooms configured for level {level}.");
            return;
        }

        runRooms.AddRange(seq.rooms);
        Shuffle(runRooms);

        string sceneName = GetSceneNameForLevel(level);
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError($"[Run] Scene name for level {level} is not configured.");
            return;
        }

        SceneManager.LoadScene(sceneName);

        // After scene loads, BootstrapRun will find RoomManager + Player
        _boot = StartCoroutine(BootstrapRun(_runVersion));
    }

    public void StartNewRun()
    {
        StartRunAtLevel(1);
    }

    // Legacy entry point – simple convenience wrapper for Level 2
    public void StartSecondRun()
    {
        StartRunAtLevel(2);
    }

    public void OnBossRoomCleared()
    {
        if (!runActive) return;

        // If this is the final level's boss, it's the real win:
        if (currentLevel >= finalLevel)
        {
            OnBossDefeated();
        }
        else
        {
            // Mid-run boss (e.g. Level 1 boss) → move to next level instead of winning.
            Debug.Log($"[Run] Boss cleared on level {currentLevel}. Advancing to next level.");
            GoToNextLevel();
        }
    }

    public void GoToNextLevel()
    {
        if (!runActive) return;

        currentLevel++;

        // Reset per-level progress (we're starting fresh in the new level)
        roomsClearedThisRun = 0;
        inBossRoom = false;
        loopCount = 0;
        currentIndex = 0;

        if (runRooms == null) runRooms = new List<GameObject>();
        runRooms.Clear();

        RoomSequence seq = GetRoomSequenceForLevel(currentLevel);
        if (seq == null || seq.rooms == null || seq.rooms.Length == 0)
        {
            Debug.LogError($"[Run] No rooms configured for level {currentLevel}.");
            return;
        }

        runRooms.AddRange(seq.rooms);
        Shuffle(runRooms);

        _runVersion++;
        if (_boot != null)
        {
            StopCoroutine(_boot);
            _boot = null;
        }

        string sceneName = GetSceneNameForLevel(currentLevel);
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError($"[Run] Scene name for level {currentLevel} is not configured.");
            return;
        }

        SceneManager.LoadScene(sceneName);

        // After the scene loads, BootstrapRun will find the new RoomManager & Player
        _boot = StartCoroutine(BootstrapRun(_runVersion));
    }

    public void StopRun()
    {
        runActive = false;
        RunActiveChanged?.Invoke(false);

        if (_boot != null)
        {
            StopCoroutine(_boot);
            _boot = null;
        }

        Time.timeScale = 1f;
    }

    public void OnPlayerDied()
    {
        EndRun();
    }

    void EndRun()
    {
        if (!runActive) return;
        runActive = false;
        RunActiveChanged?.Invoke(false);

        if (gameOverUIPrefab && !GameObject.FindObjectOfType<Canvas>()) // optional guard
            Instantiate(gameOverUIPrefab);

        Time.timeScale = 0f; // pause behind UI (your UI should use unscaled time)
        Debug.Log("[Run] EndRun: Game Over.");
    }

    public void OnBossDefeated()
    {
        if (!runActive) return; // ignore if already ended
        runActive = false;
        RunActiveChanged?.Invoke(false);

        if (youWinUIPrefab)
            Instantiate(youWinUIPrefab);

        Time.timeScale = 0f;
        Debug.Log("[Run] You Win! Boss defeated.");
    }

    // ========= BUFFS & PLAYER SYNC =========

    // Called from DamageUp / SpeedUp pickups etc.
    public void AddBuff(string id)
    {
        if (!buffs.Contains(id)) buffs.Add(id);
    }

    // Sync from player → RunManager whenever HP changes
    void OnPlayerHealthChanged(int current, int max)
    {
        currentHP = current;
        maxHP = max;
    }

    public void ApplyPersistentBuffs(TestPlayerController player)
    {
        if (player == null) return;

        // --- Get attack area (for damage buffs) ---
        CharacterCombat combat = player.GetComponent<CharacterCombat>();
        AttackArea damageMod = null;

        if (combat != null && combat.WeaponInstance != null)
            damageMod = combat.WeaponInstance.attackArea;

        if (damageMod == null)
            damageMod = player.GetComponentInChildren<AttackArea>();

        if (damageMod == null)
            Debug.LogWarning("[RunManager] No AttackArea found on player's weapon when applying buffs.");

        // Cache base values (for debug if you like)
        float baseMove = player.moveSpeed;
        float baseDash = player.dashSpeed;

        // Reapply each buff stack
        foreach (string id in buffs)
        {
            switch (id)
            {
                case "DamageUp":
                    if (damageMod != null)
                        damageMod.damage++;
                    break;

                case "SpeedUp":
                    // Make sure these match your SpeedUp pickup increments
                    player.moveSpeed += 1.5f;
                    player.dashSpeed += 2f;
                    break;
            }
        }

        Debug.Log($"[RunManager] Applied {buffs.Count} buffs. " +
                  $"MoveSpeed {baseMove} -> {player.moveSpeed}, Dash {baseDash} -> {player.dashSpeed}" +
                  (damageMod != null ? $", Damage = {damageMod.damage}" : ""));
    }

    // ========= BOOTSTRAP / ROOM FLOW =========

    IEnumerator BootstrapRun(int version)
    {
        // Let scene objects initialize
        yield return null;

        float t = 0f, timeout = 2f;
        RoomManager foundRM = null;
        TestPlayerController player = null;

        while (t < timeout)
        {
            if (version != _runVersion) yield break; // stale

            foundRM = FindObjectOfType<RoomManager>();
            player = FindObjectOfType<TestPlayerController>();

            if (foundRM && player) break;

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (version != _runVersion) yield break;
        if (!foundRM)
        {
            Debug.LogError("[Run] No RoomManager in scene after load.");
            yield break;
        }

        rm = foundRM;

        if (player != null)
        {
            // Unsubscribe old player
            if (_player != null)
                _player.HealthChanged -= OnPlayerHealthChanged;

            _player = player;
            _player.HealthChanged += OnPlayerHealthChanged;

            // Push saved health from RunManager into this scene's player
            _player.SetHealth(currentHP, maxHP);

            // Reapply buffs for this scene’s player
            ApplyPersistentBuffs(_player);
        }

        LoadCurrentRoom();

        var cam = Camera.main;
        if (cam)
        {
            var follow = cam.GetComponent<CameraFollow>();
            if (follow && player) follow.SetTarget(player.transform);
        }

        if (version == _runVersion) _boot = null;
    }

    void Shuffle<T>(IList<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int j = Random.Range(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    public void LoadCurrentRoom()
    {
        if (!runActive) return;

        if (rm == null) rm = FindObjectOfType<RoomManager>();
        if (rm == null)
        {
            Debug.LogError("[Run] No RoomManager in scene.");
            return;
        }

        GameObject prefabToLoad;

        if (inBossRoom)
        {
            // Always pick boss based on the currentLevel
            GameObject bossPrefab = GetBossPrefabForCurrentLevel();
            if (!bossPrefab)
            {
                Debug.LogError($"[Run] inBossRoom = true but no boss prefab assigned for level {currentLevel}.");
                return;
            }
            prefabToLoad = bossPrefab;
            Debug.Log($"[Run] Loading BOSS room for level {currentLevel}: {bossPrefab.name}");
        }
        else
        {
            if (runRooms == null || runRooms.Count == 0)
            {
                Debug.LogError("[Run] No rooms in sequence.");
                return;
            }

            currentIndex = Mathf.Clamp(currentIndex, 0, runRooms.Count - 1);
            prefabToLoad = runRooms[currentIndex];

            if (!prefabToLoad)
            {
                Debug.LogError($"[Run] Room at index {currentIndex} is null.");
                return;
            }

            Debug.Log($"[Run] Loading room index {currentIndex} of {runRooms.Count}, level {currentLevel}: {prefabToLoad.name}");
        }

        rm.LoadRoom(prefabToLoad);
    }

    public void GoToNextRoom()
    {
        if (!runActive) return;

        // If we just cleared a normal room, count it
        if (!inBossRoom)
            roomsClearedThisRun++;

        int roomsNeeded = GetRoomsUntilBossForCurrentLevel();
        Debug.Log($"[Run] GoToNextRoom: level {currentLevel}, roomsClearedThisRun = {roomsClearedThisRun}, roomsUntilBoss = {roomsNeeded}");

        // Should we switch to boss now?
        if (!inBossRoom && roomsClearedThisRun >= roomsNeeded)
        {
            inBossRoom = true;
            LoadCurrentRoom();
            return;
        }

        // Otherwise advance normal rooms
        currentIndex++;
        if (currentIndex >= runRooms.Count)
        {
            if (loopAndReshuffle)
            {
                Shuffle(runRooms);
                currentIndex = 0;
                loopCount++;
            }
            else
            {
                // If you don't want looping, you could end run here instead
                currentIndex = runRooms.Count - 1;
            }
        }

        LoadCurrentRoom();
    }

    public int GetCurrentDifficulty()
    {
        int roomsPassed = loopCount * (runRooms?.Count ?? 0) + currentIndex;
        int diff = difficultyBase + roomsPassed * difficultyPerRoom;
        diff = Mathf.Max(1, diff);

        // Soften first room of Level 2
        if (currentLevel == 2 && currentIndex == 0)
        {
            diff = Mathf.Max(1, diff / 2);
        }

        return diff;
    }

    // ========= PLAYER STATE HELPERS (optional external use) =========

    public void ApplyDamage(int amount)
    {
        currentHP = Mathf.Max(0, currentHP - Mathf.Abs(amount));
        if (currentHP == 0) OnPlayerDied();
    }

    public void Heal(int amount)
    {
        currentHP = Mathf.Min(maxHP, currentHP + Mathf.Abs(amount));
    }
}
