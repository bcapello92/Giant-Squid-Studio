using System.Collections.Generic;
using UnityEngine;

public class RunManager : MonoBehaviour
{
    public static RunManager I { get; private set; }

    [Header("Rooms")]
    public RoomSequence roomSequence;           // holds the full pool
    [Tooltip("If true, reshuffle when you finish the list and keep going.")]
    public bool loopAndReshuffle = true;

    [Header("Difficulty Curve")]
    [Min(1)] public int difficultyBase = 1;     // starting difficulty
    [Min(0)] public int difficultyPerRoom = 1;  // + per room advanced

    [Header("Player State")]
    public int maxHP = 100;
    public int currentHP = 100;
    public List<string> buffs = new List<string>();

    // runtime
    List<GameObject> runRooms = new List<GameObject>();
    int currentIndex = 0;    // which room in the run we’re on (0..runRooms.Count-1)
    int loopCount = 0;       // how many times we’ve finished the list
    RoomManager rm;          // cached

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // cache RoomManager once (scene should already have one)
        rm = FindObjectOfType<RoomManager>();
        if (!rm) Debug.LogError("[RunManager] No RoomManager in scene.");
        StartNewRun();
    }

    public void StartNewRun()
    {
        runRooms.Clear();
        if (roomSequence != null && roomSequence.rooms != null)
            runRooms.AddRange(roomSequence.rooms);

        if (runRooms.Count == 0)
        {
            Debug.LogError("[RunManager] No rooms in RoomSequence.");
            return;
        }

        Shuffle(runRooms);
        currentIndex = 0;
        loopCount = 0;

        currentHP = maxHP;
        buffs.Clear();

        LoadCurrentRoom();
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
        if (!rm) { Debug.LogError("[RunManager] No RoomManager."); return; }
        if (runRooms.Count == 0) { Debug.LogError("[RunManager] No rooms!"); return; }

        currentIndex = Mathf.Clamp(currentIndex, 0, runRooms.Count - 1);
        rm.LoadRoom(runRooms[currentIndex]);
    }

    // === Difficulty exposed to RoomManager / Spawners ===
    public int GetCurrentDifficulty()
    {
        // Linear curve: base + rooms_passed * slope
        // rooms_passed counts across loops: loopCount*runRooms.Count + currentIndex
        int roomsPassed = loopCount * runRooms.Count + currentIndex;
        int diff = difficultyBase + roomsPassed * difficultyPerRoom;
        return Mathf.Max(1, diff);
    }

    public void GoToNextRoom()
    {
        if (runRooms.Count == 0) return;

        currentIndex++;

        if (currentIndex >= runRooms.Count)
        {
            if (!loopAndReshuffle)
            {
                // End of run; restart a fresh run
                StartNewRun();
                return;
            }

            // Loop: reshuffle and keep climbing difficulty
            loopCount++;
            Shuffle(runRooms);
            currentIndex = 0;
        }

        LoadCurrentRoom();
    }

    // --- Player state helpers ---
    public void ApplyDamage(int amount)
    {
        currentHP = Mathf.Max(0, currentHP - Mathf.Abs(amount));
        if (currentHP == 0)
            OnPlayerDied();
    }

    public void Heal(int amount) => currentHP = Mathf.Min(maxHP, currentHP + Mathf.Abs(amount));

    public void AddBuff(string id)
    {
        if (!buffs.Contains(id)) buffs.Add(id);
    }

    public void OnPlayerDied()
    {
        // Simple reset; you can add lose-screen flow here if you want
        StartNewRun();
    }
}
