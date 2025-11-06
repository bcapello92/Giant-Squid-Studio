using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DoorController : MonoBehaviour
{
    [Header("UI Prompt")]
    public GameObject promptRoot;               // child GO with "Press E to exit"
    public KeyCode interactKey = KeyCode.E;

    [Header("State")]
    [SerializeField] bool locked = true;

    // <<< renamed to avoid collisions >>>
    [SerializeField] private RoomManager _roomManager;
    public RoomManager RoomManagerRef => _roomManager;

    bool _playerInRange;

    void Awake()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
        if (promptRoot) promptRoot.SetActive(false);
    }

    // Called by RoomManager after instantiation
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

        if (_roomManager) _roomManager.OnExitDoorUsed();
        else Debug.LogWarning("[Door] No RoomManager set.");
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
