using System.Collections;
using UnityEngine;

public class BurstTurret : MonoBehaviour
{
    public GameObject bulletPrefab;
    public GameObject muzzleFlashPrefab;
    public Transform firePoint;

    [Header("Burst Settings")]
    public int bulletsPerBurst = 3;
    public float timeBetweenShots = 0.1f;
    public float timeBetweenBursts = 1.5f;

    private bool isShooting = false;

    void Update()
    {
        if (!isShooting)
            StartCoroutine(FireBurst());
    }

    IEnumerator FireBurst()
    {
        isShooting = true;

        for (int i = 0; i < bulletsPerBurst; i++)
        {
            ShootOneBullet();
            yield return new WaitForSeconds(timeBetweenShots);
        }

        yield return new WaitForSeconds(timeBetweenBursts);
        isShooting = false;   
    }

    void ShootOneBullet()
    {
        Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);

        if (muzzleFlashPrefab != null)
            Instantiate(muzzleFlashPrefab, firePoint.position, firePoint.rotation, firePoint);
    }
}

