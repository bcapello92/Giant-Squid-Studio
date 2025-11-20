using System.Collections;
using UnityEngine;

public class SniperEnemy : MonoBehaviour
{
    [Header("References")]
    public Transform player;               // Player reference
    public GameObject bulletPrefab;        // Bullet prefab
    public Transform muzzlePoint;          // Bullet spawn point
    private SpriteRenderer spriteRenderer; // For color flash
    private LineRenderer laser;            // Laser beam

    [Header("Movement")]
    public float moveSpeed = 2f;           // Enemy movement speed
    public float minDistance = 5f;         // Minimum distance to player
    public float maxDistance = 10f;        // Maximum distance to start attacking

    [Header("Attack")]
    public float attackCooldown = 3f;      // Time between attacks
    public float attackWindup = 1f;        // Time before shooting
    private bool canAttack = true;

    [Header("Health")]
    public int maxHealth = 5;
    private int currentHealth;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        laser = GetComponent<LineRenderer>();

        if(laser != null)
            laser.enabled = false;
    }

    void Update()
    {
        if (player == null) return;

        MoveBehavior();
        RotateToPlayer();
        HandleAttack();
    }

    // --- Movement ---
    void MoveBehavior()
    {
        float distance = Vector3.Distance(transform.position, player.position);

        // Move away if too close
        if (distance < minDistance)
        {
            Vector3 dir = (transform.position - player.position).normalized;
            transform.position += dir * moveSpeed * Time.deltaTime;
        }
        // Move closer if too far
        else if (distance > maxDistance)
        {
            Vector3 dir = (player.position - transform.position).normalized;
            transform.position += dir * moveSpeed * Time.deltaTime;
        }
    }

    // --- Rotation ---
    void RotateToPlayer()
    {
        Vector3 direction = player.position - transform.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    // --- Attack ---
    void HandleAttack()
    {
        float distance = Vector3.Distance(transform.position, player.position);

        if (canAttack && distance <= maxDistance && distance >= minDistance)
        {
            StartCoroutine(ShootWithWindup());
        }
    }

    IEnumerator ShootWithWindup()
    {
        canAttack = false;

        // Flash red
        if (spriteRenderer != null)
            spriteRenderer.color = Color.green;

        // Enable and point laser
        if (laser != null)
            laser.enabled = true;

        float elapsed = 0f;
        while (elapsed < attackWindup)
        {
            if (laser != null)
            {
                laser.SetPosition(0, muzzlePoint.position);
                laser.SetPosition(1, player.position);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Shoot bullet
        Instantiate(bulletPrefab, muzzlePoint.position, muzzlePoint.rotation);

        // Reset visuals
        if (spriteRenderer != null)
            spriteRenderer.color = Color.white;
        if (laser != null)
            laser.enabled = false;

        // Cooldown
        yield return new WaitForSeconds(attackCooldown);
        canAttack = true;
    }

     // --- Health ---
    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        StartCoroutine(HitFlash());

        if(currentHealth <= 0)
        {
            Die();
        }
    }

    IEnumerator HitFlash()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            spriteRenderer.color = Color.white;
        }
    }

    void Die()
    {
        // Optional: play death animation, drop loot, etc.
        Destroy(gameObject);
    }
}