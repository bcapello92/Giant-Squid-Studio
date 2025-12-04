using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DoorController : MonoBehaviour
{
    [Header("UI Prompt")]
    public GameObject promptRoot;
    public KeyCode interactKey = KeyCode.E;

    [Header("State")]
    [SerializeField] bool locked = true;

    [SerializeField] private RoomManager _roomManager;
    public RoomManager RoomManagerRef => _roomManager;

    [Header("Level Transition")]
    [Tooltip("If true, using this door will start the next level (scene) instead of just the next room.")]
    public bool startsNextLevel = false;

    bool _playerInRange;

    void Awake()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
        if (promptRoot) promptRoot.SetActive(false);
    }

    public void SetRoomManager(RoomManager rm) { _roomManager = rm; }

    public void Lock()
    {
        locked = true;
        if (promptRoot) promptRoot.SetActive(false);
    }

    public void Unlock()
    {
        locked = false;
        if (_playerInRange && promptRoot) promptRoot.SetActive(true);
    }

    void Update()
    {
        if (locked || !_playerInRange) return;
        if (Input.GetKeyDown(interactKey)) UseDoor();
    }

    void UseDoor()
    {
        if (locked) return;
        if (promptRoot) promptRoot.SetActive(false);
        _playerInRange = false;

        if (startsNextLevel)
        {
            // Special door: go to next LEVEL (scene)
            RunManager.I?.GoToNextLevel();
        }
        else
        {
            // Normal door: just go to the next ROOM in this level
            if (_roomManager) _roomManager.OnExitDoorUsed();
            else Debug.LogWarning("[Door] No RoomManager set.");
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInRange = true;
        if (!locked && promptRoot) promptRoot.SetActive(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInRange = false;
        if (promptRoot) promptRoot.SetActive(false);
    }
}
