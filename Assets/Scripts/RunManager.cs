using System.Collections.Generic;
using UnityEditor.EditorTools;
using UnityEngine;

public class RunManager : MonoBehaviour
{
    public static RunManager I { get; private set; }

    public RoomSequence roomSequence;   // holds the full pool
    List<GameObject> runRooms = new List<GameObject>();
    int currentIndex = 0;

    [Header("Player State")]
    public int maxHP = 100;
    public int currentHP = 100;
    public List<string> buffs = new List<string>();

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
        // copy + shuffle
        runRooms.Clear();
        if (roomSequence != null && roomSequence.rooms != null)
            runRooms.AddRange(roomSequence.rooms);

        Shuffle(runRooms);
        currentIndex = 0;
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
        var rm = FindObjectOfType<RoomManager>();
        if (rm == null) { Debug.LogError("[RunManager] No RoomManager in scene."); return; }
        if (runRooms.Count == 0) { Debug.LogError("[RunManager] No rooms!"); return; }

        currentIndex = Mathf.Clamp(currentIndex, 0, runRooms.Count - 1);
        rm.LoadRoom(runRooms[currentIndex]);
    }

    public void GoToNextRoom()
    {
        currentIndex++;
        if (currentIndex >= runRooms.Count)
        {
            // reached end -> reshuffle for the next loop
            StartNewRun();
            return;
        }
        LoadCurrentRoom();
    }

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
        StartNewRun();
    }
}
