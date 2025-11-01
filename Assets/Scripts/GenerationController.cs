using UnityEngine;

public class GenerationController : MonoBehaviour
{
    public GameObject[] roomList;
    private GameObject currentRoom;
    private int ranNum = 0;
    private bool roomRunning = false;
    public bool roomDone = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (roomRunning == false)
        {
            roomRunning = true;
            ranNum = Random.Range(0, 3);
            Instantiate(roomList[ranNum], new Vector3(0, 0, 0), Quaternion.identity);
            currentRoom = GameObject.FindWithTag("Room");
        }
        if (roomDone == true)
        {
            roomDone = false;
            Destroy(currentRoom);
            roomRunning = false;
        }
    }
}
