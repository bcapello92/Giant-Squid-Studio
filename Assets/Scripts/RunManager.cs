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

    [Header("Difficulty Curve")]
    [Min(1)] public int difficultyBase = 1;
    [Min(0)] public int difficultyPerRoom = 1;

    [Header("Player State")]
    public int maxHP = 100;
    public int currentHP = 100;
    public List<string> buffs = new List<string>();

    [Header("UI (optional)")]
    public GameObject gameOverUIPrefab;     // world- or screen-space canvas prefab
    GameObject gameOverUIInstance;

    // runtime
    List<GameObject> runRooms = new();
    int currentIndex = 0;
    int loopCount = 0;
    RoomManager rm;
    Coroutine _boot;
    int _runVersion = 0;          // cancels stale boots

    public System.Action<bool> RunActiveChanged;
    public bool runActive { get; private set; } = false;                 // <- gate loading/advancing while dead

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
       
        StartNewRun();
    }

    public void StartNewRun()
    {
        _runVersion++;

        // Stop any in-flight boot
        if (_boot != null) { StopCoroutine(_boot); _boot = null; }

        // Fresh state
        runActive = true;
        RunActiveChanged?.Invoke(true);

        currentHP = maxHP;
        buffs?.Clear();

        // Rebuild & shuffle the room list for this run
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

        // Begin boot AFTER the scene is ready
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

        // Show Game Over overlay if assigned
        if (gameOverUIPrefab && !gameOverUIInstance)
            gameOverUIInstance = Instantiate(gameOverUIPrefab);

        // Optional: pause game behind UI; UI should use unscaled time
        Time.timeScale = 0f;

        // (Optional) log once for debugging
        Debug.Log("[Run] EndRun: run deactivated, Game Over UI shown.");
    }
    // Wait until the scene actually has what we need, then load the room and wire the camera
    IEnumerator BootstrapRun(int version)
    {
        // Give scene a frame
        yield return null;

        // Wait up to 2s for RoomManager & Player to exist in the loaded scene
        float t = 0f, timeout = 2f;
        RoomManager foundRM = null;
        TestPlayerController player = null;

        while (t < timeout)
        {
            if (version != _runVersion) yield break; // stale boot

            foundRM = FindObjectOfType<RoomManager>();
            player = FindObjectOfType<TestPlayerController>();
            if (foundRM && player) break;

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (version != _runVersion) yield break; // stale boot
        if (!foundRM)
        {
            Debug.LogError("[Run] No RoomManager in scene after load.");
            yield break;
        }

        rm = foundRM;

        // Load first room (RoomManager moves player to entrance & wires camera)
        LoadCurrentRoom();

        // Belt & suspenders: ensure camera follows the player
        var cam = Camera.main;
        if (cam != null)
        {
            var follow = cam.GetComponent<CameraFollow>();
            if (follow != null && player != null) follow.SetTarget(player.transform);
        }

        if (version == _runVersion) _boot = null;
    }

        void Shuffle<T>(IList<T> list)
        {

            for (int i = 0; i < list.Count; i++) { int j = Random.Range(i, list.Count); (list[i], list[j]) = (list[j], list[i]); }
        }
    

    public void LoadCurrentRoom()
    {
        if (!runActive) return;

        // Reacquire rm every call (fresh scene)
        if (rm == null) rm = FindObjectOfType<RoomManager>();
        if (rm == null) { Debug.LogError("[Run] No RoomManager in scene."); return; }

        if (runRooms == null || runRooms.Count == 0)
        {
            Debug.LogError("[Run] No rooms in sequence.");
            return;
        }

        currentIndex = Mathf.Clamp(currentIndex, 0, runRooms.Count - 1);
        var prefab = runRooms[currentIndex];
        if (!prefab) { Debug.LogError($"[Run] Room at index {currentIndex} is null."); return; }

        rm.LoadRoom(prefab);
    }

    public void GoToNextRoom()
    {
        if (!runActive) return;

        currentIndex++;
        if (currentIndex >= runRooms.Count)
        {
            // End or reshuffle loop here (your preference)
            // Example: reshuffle and continue
            Shuffle(runRooms);
            currentIndex = 0;
        }
        LoadCurrentRoom();
    }

    public int GetCurrentDifficulty()
    {
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
