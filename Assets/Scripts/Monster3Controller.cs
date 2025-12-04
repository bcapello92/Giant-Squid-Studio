using System;
using UnityEngine;
using static UnityEngine.RuleTile.TilingRuleOutput;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyShooter : RoomEnemy, IDamageable
{
    [Header("Health")]
    public int maxHP = 30;
    [SerializeField] private int currentHP;
    public event Action<int, int> OnHealthChanged;        // (current, max)
    public event Action<EnemyShooter> OnDied;          // (this)

    [Header("Movement")]
    public float moveSpeed = 3f;
    [Tooltip("Max distance at which the enemy will stop and shoot.")]
    public float attackRange = 6f;

    [Header("Target")]
    public UnityEngine.Transform target;          // Player. If null, will search by tag "Player".

    [Header("Shooting")]
    public GameObject bulletPrefab;   // Your Bullet2D prefab
    public UnityEngine.Transform firePoint;       // Where bullets spawn
    public float attackCooldown = 1.5f;

    [Header("Animation & Visuals")]
    public Animator anim;
    public string speedParam = "Speed";
    public string attackTrigger = "Attack";
    public SpriteRenderer spriteRenderer; // body sprite to flip on X
    public string hitTriggerParam = "";   // optional: name of a hit trigger in Animator

    private Rigidbody2D rb;
    private float attackTimer = 0f;
    private bool isAttacking = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;

        if (!anim) anim = GetComponentInChildren<Animator>();
        if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        // Auto-find player by tag if not assigned
        if (!target)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p) target = p.transform;
        }

        // Init health
        currentHP = maxHP;
        OnHealthChanged?.Invoke(currentHP, maxHP);

        // Register with health bar manager
        if (EnemyHealthBarManager.Instance != null)
        {
            EnemyHealthBarManager.Instance.RegisterShooter(this);
        }
    }

    void Update()
    {
        if (!target || currentHP <= 0) return;

        attackTimer -= Time.deltaTime;

        Vector2 toTarget = target.position - transform.position;
        float distance = toTarget.magnitude;

        if (isAttacking)
        {
            // Must stop while shooting
            rb.linearVelocity = Vector2.zero;
        }
        else
        {
            if (attackTimer <= 0f && distance <= attackRange)
            {
                // In range and off cooldown -> stop & shoot
                TriggerAttack();
            }
            else
            {
                // Between shots: move away from the player on the X axis
                float dirX = Mathf.Sign(transform.position.x - target.position.x);
                Vector2 vel = new Vector2(dirX * moveSpeed, 0f);
                rb.linearVelocity = vel;

                // Flip sprite towards direction of movement on X
                if (vel.x > 0.01f)
                    spriteRenderer.flipX = false;   // facing right
                else if (vel.x < -0.01f)
                    spriteRenderer.flipX = true;    // facing left
            }
        }

        // Animator Speed parameter
        if (anim && !string.IsNullOrEmpty(speedParam))
        {
            float speed = rb.linearVelocity.magnitude;
            anim.SetFloat(speedParam, speed);
        }
    }

    void TriggerAttack()
    {
        attackTimer = attackCooldown;
        isAttacking = true;

        // Stop moving
        rb.linearVelocity = Vector2.zero;

        // Face the player when attacking
        if (spriteRenderer && target)
        {
            bool playerIsLeft = target.position.x < transform.position.x;
            spriteRenderer.flipX = playerIsLeft;
        }

        if (anim && !string.IsNullOrEmpty(attackTrigger))
            anim.SetTrigger(attackTrigger);

        // Recommended: call SpawnBullet from an Animation Event in the attack clip
        // SpawnBullet(); // if you want it instantaneous instead
    }

    /// <summary>
    /// Called from an Animation Event in the Attack animation.
    /// </summary>
    public void SpawnBullet()
    {
        if (!bulletPrefab || !firePoint) return;

        GameObject bulletGO = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);

        Bullet2D bullet = bulletGO.GetComponent<Bullet2D>();
        if (bullet == null)
        {
            Debug.LogWarning("Spawned bullet prefab has no Bullet2D component.", bulletGO);
            return;
        }

        // Ensure bullet doesn't hit this enemy
        bullet.owner = gameObject;

        // Aim at player or use facing direction as fallback
        Vector2 dir;
        if (target != null)
        {
            dir = ((Vector2)target.position - (Vector2)firePoint.position).normalized;
        }
        else
        {
            dir = (spriteRenderer != null && spriteRenderer.flipX) ? Vector2.left : Vector2.right;
        }

        bulletGO.transform.right = dir;

        Rigidbody2D brb = bulletGO.GetComponent<Rigidbody2D>();
        if (brb != null)
        {
            brb.gravityScale = 0f;
            brb.linearVelocity = dir * bullet.speed;
        }
    }

    /// <summary>
    /// Call this from an Animation Event at the end of the Attack clip.
    /// </summary>
    public void EndAttack()
    {
        isAttacking = false;
    }

    // ------------------- IDamageable ----------------------
    public void TakeDamage(int amount)
    {
        if (amount <= 0 || currentHP <= 0) return;

        int prev = currentHP;
        currentHP = Mathf.Max(0, currentHP - amount);

        if (currentHP != prev)
        {
            OnHealthChanged?.Invoke(currentHP, maxHP);

            // Optional hit animation
            if (anim && !string.IsNullOrEmpty(hitTriggerParam))
            {
                anim.SetTrigger(hitTriggerParam);
            }
        }

        if (currentHP <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        OnDied?.Invoke(this);
        Destroy(gameObject);
    }
}
