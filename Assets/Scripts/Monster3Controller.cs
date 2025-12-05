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
    public event Action<EnemyShooter> OnDied;             // (this)

    [Header("Movement")]
    public float moveSpeed = 3f;
    [Tooltip("Max distance at which the enemy will stop and shoot.")]
    public float attackRange = 6f;

    [Header("Target")]
    public UnityEngine.Transform target;          // Player. If null, will search by tag "Player".

    [Header("Shooting")]
    public GameObject bulletPrefab;               // Your Bullet2D prefab
    public UnityEngine.Transform firePoint;       // Where bullets spawn
    public float attackCooldown = 1.5f;

    [Header("Animation & Visuals")]
    public Animator anim;
    public string speedParam = "Speed";
    public string attackTrigger = "Attack";
    public SpriteRenderer spriteRenderer;         // body sprite to flip on X
    public string hitTriggerParam = "Hit";        // name of a hit trigger in Animator
    public string deathTriggerParam = "Die";      // death trigger

    [Header("Death")]
    [Tooltip("Time after triggering death before the enemy object is destroyed.")]
    public float destroyDelayAfterDeath = 0.6f;

    private Rigidbody2D rb;
    private float attackTimer = 0f;
    private bool isAttacking = false;
    private bool isDead = false;
    void Start()
    {
        
        // try to find the current RoomManager and register ourselves.
        if (roomManager == null)
        {
            var rm = FindObjectOfType<RoomManager>();
            if (rm != null)
            {
                rm.RegisterEnemy(this);
            }
            else
            {
                Debug.LogWarning("[EnemyShooter] No RoomManager found to register with.", this);
            }
        }
    }
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

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
        if (isDead)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (!target || currentHP <= 0) return;

        attackTimer -= Time.deltaTime;

        Vector2 toTarget = (Vector2)(target.position - transform.position);
        float distance = toTarget.magnitude;

        // Small band around desired range to avoid jitter
        float desiredRange = attackRange;   // e.g. 6f
        float rangeTolerance = 0.5f;        // acceptable +/- range

        Vector2 desiredVelocity = Vector2.zero;

        if (isAttacking)
        {
            // Must stop while shooting
            desiredVelocity = Vector2.zero;
        }
        else
        {
            // If in range and off cooldown → attack
            if (attackTimer <= 0f && distance <= attackRange)
            {
                TriggerAttack();
                // TriggerAttack sets isAttacking = true and zeroes velocity
                desiredVelocity = Vector2.zero;
            }
            else
            {
                // Maintain distance band:
                // too far -> move closer
                // too close -> back away
                if (distance > desiredRange + rangeTolerance)
                {
                    // Move toward player
                    desiredVelocity = toTarget.normalized * moveSpeed;
                }
                else if (distance < desiredRange - rangeTolerance)
                {
                    // Move away from player
                    desiredVelocity = -toTarget.normalized * moveSpeed;
                }
                else
                {
                    // In the sweet spot, hover
                    desiredVelocity = Vector2.zero;
                }
            }
        }

        rb.linearVelocity = desiredVelocity;

        // Flip sprite to face the player (or velocity) on X
        if (spriteRenderer != null)
        {
            // Face the player horizontally
            if (toTarget.x > 0.01f)
                spriteRenderer.flipX = false;   // facing right
            else if (toTarget.x < -0.01f)
                spriteRenderer.flipX = true;    // facing left
        }

        // Animator Speed parameter
        if (anim && !string.IsNullOrEmpty(speedParam))
        {
            float speed = rb.linearVelocity.magnitude;
            anim.SetFloat(speedParam, speed);
        }
    }


    // ------------------- Death ----------------------
    void Die()
    {
        if (isDead) return;
        isDead = true;
        DieInRoom();//register with roommanager
        // Stop movement & attacks
        rb.linearVelocity = Vector2.zero;
        isAttacking = false;

        // Notify listeners (health bar manager will remove the bar)
        OnDied?.Invoke(this);

        // Disable colliders so it no longer blocks or takes hits
        var cols = GetComponents<Collider2D>();
        for (int i = 0; i < cols.Length; i++)
            cols[i].enabled = false;

        // Play death animation if available
        if (anim && !string.IsNullOrEmpty(deathTriggerParam))
        {
            anim.SetTrigger(deathTriggerParam);

            // Auto-destroy after a short delay (approx length of death anim)
            Destroy(gameObject, destroyDelayAfterDeath);
        }
        else
        {
            // No death animation set up -> just destroy immediately
            Destroy(gameObject);
        }
    }

    // Optional: still here if you want to use an Animation Event instead of the delay.
    public void OnDeathAnimationComplete()
    {
        Destroy(gameObject);
    }

    // ------------------- Attacking ----------------------
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

    public void EndAttack()
    {
        isAttacking = false;
    }

    // ------------------- IDamageable ----------------------
    public void TakeDamage(int amount)
    {
        if (amount <= 0 || currentHP <= 0 || isDead) return;

        int prev = currentHP;
        currentHP = Mathf.Max(0, currentHP - amount);

        if (currentHP != prev)
        {
            OnHealthChanged?.Invoke(currentHP, maxHP);

            // Trigger hit reaction animation
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
}
