using System.Collections;
using UnityEngine;

public class FountainController : MonoBehaviour
{
    public GameObject bulletPrefab;

    [Header("Burst Settings")]
    public float timeBetweenShots = 1f;

    private bool isShooting = false;

    void Update()
    {
        if (!isShooting)
            StartCoroutine(FireBullet());
    }

    IEnumerator FireBullet()
    {
        isShooting = true;

        Instantiate(bulletPrefab, transform.position, Quaternion.Euler(0f, 0f, Random.Range(0, 360)));

        yield return new WaitForSeconds(timeBetweenShots);
        isShooting = false;   
    }

}

