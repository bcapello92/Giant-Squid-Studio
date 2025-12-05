using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
public class RunManager : MonoBehaviour
{
    public static RunManager I { get; private set; }
    [Header("Levels / Progression")]
    public int finalLevel = 2;   // last level in the game (2 for now)

    [Header("Rooms")]
    public RoomSequence roomSequence;
    public bool loopAndReshuffle = true;

    [Header("Boss Flow")]
    [Min(1)] public int roomsUntilBoss = 4;       // boss after N normal rooms
    public GameObject bossRoomPrefab;
    [Header("Second Boss Run (Level 2)")]
    public RoomSequence secondRunRoomSequence;      // rooms for level 2 run
    [Min(1)] public int secondRunRoomsUntilBoss = 2;
    public GameObject secondBossRoomPrefab;

    [Header("Difficulty Curve")]
    [Min(1)] public int difficultyBase = 1;
    [Min(0)] public int difficultyPerRoom = 1;

    [Header("Player State")]
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
    // runtime
    List<GameObject> runRooms = new();
    int currentIndex = 0;
    int loopCount = 0;
    int roomsClearedThisRun = 0;
    bool inBossRoom = false;
    public int CurrentLevel => loopCount + 1;
    RoomManager rm;
    Coroutine _boot;
    int _runVersion = 0;
    
    public System.Action<bool> RunActiveChanged;
    public bool runActive { get; private set; } = false;
    public bool IsRunActive => runActive;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        
    }
    public void StartRunAtLevel(int level)
    {
        _runVersion++;
        if (_boot != null) { StopCoroutine(_boot); _boot = null; }

        runActive = true;
        RunActiveChanged?.Invoke(true);

        currentLevel = level;
        currentHP = maxHP;
        buffs?.Clear();

        roomsClearedThisRun = 0;
        inBossRoom = false;
        loopCount = 0;
        currentIndex = 0;

        if (runRooms == null) runRooms = new List<GameObject>();
        runRooms.Clear();

        // Pick the right room sequence for that level
        RoomSequence seq = null;
        switch (level)
        {
            case 1:
                seq = level1Rooms != null ? level1Rooms : roomSequence;
                break;
            case 2:
                seq = level2Rooms;
                break;
            default:
                Debug.LogError($"[Run] Unsupported level index {level}.");
                return;
        }

        if (seq == null || seq.rooms == null || seq.rooms.Length == 0)
        {
            Debug.LogError($"[Run] No rooms configured for level {level}.");
            return;
        }

        runRooms.AddRange(seq.rooms);
        Shuffle(runRooms);

        // Decide which scene to load
        string sceneName = level == 1 ? level1SceneName : level2SceneName;

        SceneManager.LoadScene(sceneName);

        // After scene loads, BootstrapRun will find RoomManager + Player
        _boot = StartCoroutine(BootstrapRun(_runVersion));
    }

    public void StartNewRun()
    {
        StartRunAtLevel(1);
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

        // Reset per-level progress (we're starting fresh in Level 2)
        roomsClearedThisRun = 0;
        inBossRoom = false;
        loopCount = 0;
        currentIndex = 0;

        if (runRooms == null) runRooms = new List<GameObject>();
        runRooms.Clear();

        RoomSequence seq = null;

        if (currentLevel == 2)
        {
            seq = level2Rooms;
        }
        else
        {
            // Future: add more levels here
            Debug.LogWarning($"[Run] GoToNextLevel called for unsupported level {currentLevel}. Using level2Rooms as fallback.");
            seq = level2Rooms;
        }

        if (seq == null || seq.rooms == null || seq.rooms.Length == 0)
        {
            Debug.LogError($"[Run] No rooms configured for level {currentLevel}.");
            return;
        }

        runRooms.AddRange(seq.rooms);
        Shuffle(runRooms);

        _runVersion++;
        if (_boot != null) { StopCoroutine(_boot); _boot = null; }

        // Load the new scene for this level
        string sceneName = currentLevel == 2 ? level2SceneName : level1SceneName;
        SceneManager.LoadScene(sceneName);

        // After the scene loads, BootstrapRun will find the new RoomManager & Player
        _boot = StartCoroutine(BootstrapRun(_runVersion));
    }

    public void StopRun()
    {
        runActive = false;
        RunActiveChanged?.Invoke(false);
        if (_boot != null) { StopCoroutine(_boot); _boot = null; }
    }
    public void StartSecondRun()
    {
        _runVersion++;
        if (_boot != null)
        {
            StopCoroutine(_boot);
            _boot = null;
        }

        runActive = true;
        RunActiveChanged?.Invoke(true);

        roomsClearedThisRun = 0;
        inBossRoom = false;

        // Use the second-run configuration
        if (secondRunRoomSequence == null ||
            secondRunRoomSequence.rooms == null ||
            secondRunRoomSequence.rooms.Length == 0)
        {
            Debug.LogError("[Run] No rooms in secondRunRoomSequence.");
            return;
        }

        roomSequence = secondRunRoomSequence;
        roomsUntilBoss = secondRunRoomsUntilBoss;
        bossRoomPrefab = secondBossRoomPrefab;

        if (runRooms == null) runRooms = new List<GameObject>();
        runRooms.Clear();
        runRooms.AddRange(roomSequence.rooms);
        Shuffle(runRooms);

        currentIndex = 0;
        loopCount = 0;

        _boot = StartCoroutine(BootstrapRun(_runVersion));
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

    IEnumerator BootstrapRun(int version)
    {
        yield return null; // let scene objects initialize

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
        if (!foundRM) { Debug.LogError("[Run] No RoomManager in scene after load."); yield break; }

        rm = foundRM;
        // Reapply any buffs from previous rooms/levels
        if (player != null)
        {
            ApplyPersistentBuffs(player);
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
        if (rm == null) { Debug.LogError("[Run] No RoomManager in scene."); return; }

        // Decide which room to load:
        GameObject prefabToLoad;

        if (inBossRoom)
        {
            if (!bossRoomPrefab)
            {
                Debug.LogError("[Run] inBossRoom = true but bossRoomPrefab is not assigned.");
                return;
            }
            prefabToLoad = bossRoomPrefab;
        }
        else
        {
            if (runRooms == null || runRooms.Count == 0) { Debug.LogError("[Run] No rooms in sequence."); return; }
            currentIndex = Mathf.Clamp(currentIndex, 0, runRooms.Count - 1);
            prefabToLoad = runRooms[currentIndex];
            if (!prefabToLoad) { Debug.LogError($"[Run] Room at index {currentIndex} is null."); return; }
        }

        rm.LoadRoom(prefabToLoad);
    }

    public void GoToNextRoom()
    {
        if (!runActive) return;

        // If we just cleared a normal room, count it
        if (!inBossRoom)
            roomsClearedThisRun++;

        // Should we switch to boss now?
        if (!inBossRoom && roomsClearedThisRun >= roomsUntilBoss)
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


    // ==== Player state ====
    public void ApplyDamage(int amount)
    {
        currentHP = Mathf.Max(0, currentHP - Mathf.Abs(amount));
        if (currentHP == 0) OnPlayerDied();
    }

    public void Heal(int amount)
    {
        currentHP = Mathf.Min(maxHP, currentHP + Mathf.Abs(amount));
    }

    public void AddBuff(string id)
    {
        if (!buffs.Contains(id)) buffs.Add(id);
    }
}
