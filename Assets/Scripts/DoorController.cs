using UnityEngine;

public class DoorController : MonoBehaviour
{
    private GameObject gameController;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gameController = GameObject.FindWithTag("GenerationController");
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        gameController.GetComponent<GenerationController>().roomDone = true;
    }

}
