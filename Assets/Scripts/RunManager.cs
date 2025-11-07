using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RunManager : MonoBehaviour
{
    public static RunManager I { get; private set; }

    [Header("Rooms")]
    public RoomSequence roomSequence;
    public bool loopAndReshuffle = true;

    [Header("Boss Flow")]
    [Min(1)] public int roomsUntilBoss = 4;       // boss after N normal rooms
    public GameObject bossRoomPrefab;             // room that contains the boss (or a BossSpawn marker)

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

    // runtime
    List<GameObject> runRooms = new();
    int currentIndex = 0;
    int loopCount = 0;
    int roomsClearedThisRun = 0;
    bool inBossRoom = false;

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

    public void StartNewRun()
    {
        _runVersion++;
        if (_boot != null) { StopCoroutine(_boot); _boot = null; }

        runActive = true;
        RunActiveChanged?.Invoke(true);

        currentHP = maxHP;
        buffs?.Clear();

        roomsClearedThisRun = 0;
        inBossRoom = false;

        if (runRooms == null) runRooms = new List<GameObject>();
        runRooms.Clear();
        if (roomSequence != null && roomSequence.rooms != null && roomSequence.rooms.Length > 0)
        {
            runRooms.AddRange(roomSequence.rooms);
            Shuffle(runRooms);
        }
        else
        {
            Debug.LogError("[Run] No rooms in RoomSequence.");
            return;
        }

        currentIndex = 0;
        loopCount = 0;

        _boot = StartCoroutine(BootstrapRun(_runVersion));
    }

    public void StopRun()
    {
        runActive = false;
        RunActiveChanged?.Invoke(false);
        if (_boot != null) { StopCoroutine(_boot); _boot = null; }
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
        // Scale during normal rooms; boss difficulty can be handled in boss controller if needed
        int roomsPassed = loopCount * runRooms.Count + currentIndex;
        int diff = difficultyBase + roomsPassed * difficultyPerRoom;
        return Mathf.Max(1, diff);
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
