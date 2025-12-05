using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class Boss2Controller2D : RoomEnemy, IDamageable
{
    [Header("Target")]
    public Transform target;
    public string targetTag = "Player";
    [Header("Death")]
    public float deathDespawnDelay = 2.5f;
    [Header("Movement")]
    public float moveSpeed = 1.8f;
    public float detectionRange = 12f;
    public float stopDistance = 2.5f;
    public float acceleration = 20f;

    [Header("Recovery / Vulnerability")]
    [Tooltip("How long after entering recovery the boss is locked & vulnerable.")]
    public float recoveryDuration = 2.5f;

    [Tooltip("How many attacks must finish before entering recovery.")]
    public int attacksBeforeRecovery = 2;   // <-- NEW

    [Header("Health")]
    public int maxHP = 300;
    [SerializeField] private int currentHP;
    public event Action<int, int> OnHealthChanged;
    public event Action<Boss2Controller2D> OnDied;

    [Header("Attacks")]
    public float attackRange = 3.0f;
    public float attackCooldown = 2.0f;
    public string attack1Trigger = "attack";     // single-side
    public string attack2Trigger = "attack 2";   // both sides
    [Range(0f, 1f)] public float heavyAttackChance = 0.4f;

    [Header("Attack Hitbox")]
    public Transform attackPoint;
    public float attackRadius = 2f;
    public LayerMask targetLayers;
    public int attack1Damage = 15;
    public int attack2Damage = 25;
    public float knockbackForce = 6f;

    [Header("Animation & Visuals")]
    public string speedParam = "speed";      // drives idle/run
    public string hitTriggerParam = "damaged"; // recovery anim
    public string deathTriggerParam = "death";
    public SpriteRenderer[] spriteRenderers;

    [Header("Audio")]
    public AudioSource attackAudio;
    public AudioSource hurtAudio;

    [Header("Debug")]
    public bool debugLog = false;

    Rigidbody2D rb;
    Animator anim;

    bool useScaleFlipFallback;
    bool isDead = false;
    bool isAttacking = false;
    bool isRecovering = false;   // locked in damaged state
    bool isVulnerable = false;   // can actually take damage
    float recoveryEndTime = 0f;
    float lastAttackTime = -999f;
    int attacksSinceLastRecovery = 0;        // <-- NEW

    Collider2D[] cols;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();

        if (!target)
        {
            var go = GameObject.FindGameObjectWithTag(targetTag);
            if (go) target = go.transform;
        }

        currentHP = maxHP;
        OnHealthChanged?.Invoke(currentHP, maxHP);

        cols = GetComponentsInChildren<Collider2D>(true);

        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        if (spriteRenderers == null || spriteRenderers.Length == 0)
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        useScaleFlipFallback = (spriteRenderers == null || spriteRenderers.Length == 0);

        if (targetLayers.value == 0)
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0)
                targetLayers |= (1 << playerLayer);
        }

        if (EnemyHealthBarManager.Instance != null)
        {
            EnemyHealthBarManager.Instance.RegisterBoss2(this);
        }
    }

    void Start()
    {
        OnHealthChanged?.Invoke(currentHP, maxHP);
    }

    void Update()
    {
        if (isDead) return;

        // End recovery window when time is up
        if (isRecovering && Time.time >= recoveryEndTime)
        {
            isRecovering = false;
            isVulnerable = false;
            if (debugLog) Debug.Log("[Boss2] Recovery finished, no longer vulnerable");
        }
    }

    void FixedUpdate()
    {
        if (isDead || target == null)
        {
            rb.linearVelocity = Vector2.zero;
            SetAnimSpeed(0f);
            return;
        }

        Vector2 toTarget = (Vector2)(target.position - transform.position);
        float dist = toTarget.magnitude;

        UpdateFacing(toTarget);

        // No movement while attacking OR recovering
        if (isAttacking || isRecovering)
        {
            rb.linearVelocity = Vector2.zero;
        }
        else
        {
            Vector2 desiredVel = Vector2.zero;

            if (dist <= detectionRange && dist > stopDistance)
            {
                desiredVel = toTarget.normalized * moveSpeed;
            }

            Vector2 vel = rb.linearVelocity;
            Vector2 step = Vector2.ClampMagnitude(desiredVel - vel, acceleration * Time.fixedDeltaTime);
            rb.linearVelocity = vel + step;
        }

        // Attack when close enough and off cooldown
        if (!isAttacking && !isRecovering &&
            dist <= attackRange &&
            Time.time >= lastAttackTime + attackCooldown)
        {
            StartAttack();
        }

        SetAnimSpeed(rb.linearVelocity.magnitude);
    }

    void SetAnimSpeed(float v)
    {
        if (anim && !string.IsNullOrEmpty(speedParam))
            anim.SetFloat(speedParam, v);
    }

    // ---------------- Facing ----------------
    void UpdateFacing(Vector2 toTarget)
    {
        float faceX = toTarget.x;
        if (Mathf.Abs(faceX) > 0.01f)
        {
            if (!useScaleFlipFallback)
            {
                bool flip = faceX < 0f;
                foreach (var sr in spriteRenderers)
                    if (sr) sr.flipX = flip;
            }
            else
            {
                var s = transform.localScale;
                s.x = Mathf.Abs(s.x) * Mathf.Sign(faceX);
                transform.localScale = s;
            }
        }
    }

    // ---------------- Attacking ----------------
    void StartAttack()
    {
        isAttacking = true;
        rb.linearVelocity = Vector2.zero;

        lastAttackTime = Time.time;

        bool useHeavy = (UnityEngine.Random.value < heavyAttackChance);
        string trig = useHeavy ? attack2Trigger : attack1Trigger;

        if (!string.IsNullOrEmpty(trig))
        {
            anim.ResetTrigger(trig);
            anim.SetTrigger(trig);
        }

        if (debugLog)
            Debug.Log($"[Boss2] StartAttack: {(useHeavy ? "attack 2" : "attack")}");
    }

    // Animation event: hit frame of "attack"
    public void AnimEvent_DealAttack1Damage()
    {
        DoAttackDamage(attack1Damage, hitBothSides: false);
    }

    // Animation event: hit frame of "attack 2"
    public void AnimEvent_DealAttack2Damage()
    {
        DoAttackDamage(attack2Damage, hitBothSides: true); // <-- hits both sides
    }

    void DoAttackDamage(int dmg, bool hitBothSides)
    {
        if (attackPoint == null)
        {
            Debug.LogWarning("[Boss2] No attackPoint assigned.");
            return;
        }

        if (attackAudio) attackAudio.Play();

        var uniqueHits = new System.Collections.Generic.HashSet<Collider2D>();

        Vector2 pivot = transform.position;
        Vector2 frontCenter = attackPoint.position;

        // always hit in front
        CollectHits(frontCenter, dmg, uniqueHits);

        if (hitBothSides)
        {
            // Mirror front around boss center to get a back center
            Vector2 offset = frontCenter - pivot;
            Vector2 backCenter = pivot - offset;
            CollectHits(backCenter, dmg, uniqueHits);
        }
    }

    void CollectHits(Vector2 center, int dmg, System.Collections.Generic.HashSet<Collider2D> seen)
    {
        var hits = Physics2D.OverlapCircleAll(center, attackRadius, targetLayers);
        foreach (var h in hits)
        {
            if (!h || seen.Contains(h)) continue;
            seen.Add(h);

            var d = h.GetComponentInParent<IDamageable>() ?? h.GetComponentInChildren<IDamageable>();
            if (d == null) continue;

            d.TakeDamage(dmg);

            var prb = h.attachedRigidbody;
            if (prb)
            {
                Vector2 dir = ((Vector2)h.transform.position - (Vector2)transform.position).normalized;
                prb.AddForce(dir * knockbackForce, ForceMode2D.Impulse);
            }
        }
    }

    // Animation event: last frame of each attack
    public void AnimEvent_AttackFinished()
    {
        if (debugLog)
            Debug.Log("[Boss2] Attack finished");

        isAttacking = false;

        // Count how many attacks we've done since last recovery
        attacksSinceLastRecovery++;

        // Only enter recovery/damaged after N attacks
        if (attacksSinceLastRecovery >= Mathf.Max(1, attacksBeforeRecovery))
        {
            attacksSinceLastRecovery = 0;

            isRecovering = true;
            isVulnerable = true;
            recoveryEndTime = Time.time + recoveryDuration;

            if (debugLog)
                Debug.Log("[Boss2] Entering recovery/damaged state (vulnerable)");

            if (anim && !string.IsNullOrEmpty(hitTriggerParam))
                anim.SetTrigger(hitTriggerParam); // plays 'damaged' state
        }
        else
        {
            if (debugLog)
                Debug.Log($"[Boss2] No recovery yet ({attacksSinceLastRecovery}/{attacksBeforeRecovery})");
        }
    }

    // ---------------- Damage & Health ----------------
    public void TakeDamage(int amount)
    {
        if (isDead || amount <= 0) return;

        // Only take damage while in recovery/damaged window
        if (!isVulnerable)
        {
            if (debugLog)
                Debug.Log("[Boss2] Hit ignored (not in recovery window)");
            return;
        }

        int prev = currentHP;
        currentHP = Mathf.Max(0, currentHP - Mathf.Abs(amount));

        if (currentHP != prev)
        {
            OnHealthChanged?.Invoke(currentHP, maxHP);

            // Optional: retrigger damaged anim when actually hit
            if (currentHP > 0 && anim && !string.IsNullOrEmpty(hitTriggerParam))
                anim.SetTrigger(hitTriggerParam);
        }

        if (currentHP == 0)
        {
            Die();
        }
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        if (debugLog) Debug.Log("[Boss2] Die()");

        if (hurtAudio) hurtAudio.Play();

        DieInRoom();
        OnDied?.Invoke(this);

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.simulated = false;

        if (cols != null)
            foreach (var c in cols)
                if (c) c.enabled = false;

        if (anim && !string.IsNullOrEmpty(deathTriggerParam))
            anim.SetTrigger(deathTriggerParam);
        else
            Destroy(gameObject);

        // NEW: fallback in case the animation event never fires
        StartCoroutine(DeathDespawnAfterDelay());
    }
    System.Collections.IEnumerator DeathDespawnAfterDelay()
    {
        yield return new WaitForSeconds(deathDespawnDelay);
        if (this != null)    // just in case event already destroyed us
            Destroy(gameObject);
    }

    // Animation event: last frame of death
    public void AnimEvent_DeathFinished()
    {
        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, attackRadius);

            // Also show back hit center for debugging
            Vector2 pivot = transform.position;
            Vector2 frontCenter = attackPoint.position;
            Vector2 offset = (Vector2)frontCenter - pivot;
            Vector2 backCenter = pivot - offset;

            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.6f);
            Gizmos.DrawWireSphere(backCenter, attackRadius);
        }

        Gizmos.color = new Color(1f, 0.9f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
