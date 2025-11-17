using UnityEngine;

public class MuzzleFlash : MonoBehaviour
{
    public float lifetime = 0.05f;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }
}
