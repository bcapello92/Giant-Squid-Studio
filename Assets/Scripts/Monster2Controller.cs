using System.Collections.Generic;
using UnityEngine;
using System;


[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class EnemyController : RoomEnemy, IDamageable
{
    [Header("Target")]
    public Transform target;
    public string targetTag = "Player";

    [Header("Movement")]
    public bool sideScroller = false;
    public float moveSpeed = 3f;
    public float detectionRange = 8f;
    public float stopDistance = 1.0f;
    public float acceleration = 25f;

    [Header("Retreat After Attack")]
    [Tooltip("If true, enemy will move away from the player for a short time after each attack.")]
    public bool retreatAfterAttack = true;
    [Tooltip("How long after an attack the enemy retreats (seconds).")]
    public float retreatDuration = 0.8f;
    [Tooltip("Speed multiplier while retreating.")]
    public float retreatSpeedMultiplier = 1.1f;

    [Header("Health")]
    public int maxHP = 40;
    [SerializeField] int currentHP;
    bool isDead;

    [Header("Attacks")]
    public float attackRange = 1.2f;
    public float attackCooldown = 0.7f;
    public Vector2 attackIndices = new Vector2(0, 2);

    [Header("Death")]
    public float deathDespawnDelay = 1.5f;
    public bool disablePhysicsOnDeath = true;
    public Behaviour[] componentsToDisableOnDeath;
    Collider2D[] cols;

    [Header("Hit Logic (OverlapCircle)")]
    public LayerMask targetLayers;
    public int damage = 10;
    public float knockbackForce = 4f;
    public float attackRadius = 0.6f;
    [Tooltip("Local-space offset when facing RIGHT. X auto-flips when facing left.")]
    public Vector2 attackOffset = new Vector2(0.6f, 0.0f);
    [Tooltip("Only used if sideScroller=true, to lift/lower hit circle.")]
    public float sideScrollerYOffset = 0f;

    public event Action<int, int> OnHealthChanged;
    public event Action<EnemyController> OnDied;

    [Header("Audio")]
    public AudioSource attackAudio;
    public AudioSource hurtAudio;

    [Header("Debug")]
    public bool debugAttack = false;
    public KeyCode manualAttackKey = KeyCode.K;

    // Animator hashes
    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int VSpeedHash = Animator.StringToHash("VerticalSpeed");
    static readonly int MoveXHash = Animator.StringToHash("MoveX");
    static readonly int MoveYHash = Animator.StringToHash("MoveY");
    static readonly int AttackIdxHash = Animator.StringToHash("AttackIndex");
    static readonly int AttackTrig = Animator.StringToHash("Attack");
    static readonly int HitTrig = Animator.StringToHash("Hit");
    static readonly int DeathTrig = Animator.StringToHash("Death");

    Rigidbody2D rb;
    Animator anim;

    [SerializeField] SpriteRenderer[] spriteRenderers;
    bool useScaleFlipFallback;

    float lastAttackTime = -999f;
    bool attackWindowOpen = false;
    readonly HashSet<Collider2D> hitThisSwing = new HashSet<Collider2D>();

    void Start()
    {
        if (EnemyHealthBarManager.Instance != null)
        {
            EnemyHealthBarManager.Instance.RegisterEnemy(this);
        }

        OnHealthChanged?.Invoke(currentHP, maxHP);
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();

        if (!target)
        {
            var go = GameObject.FindGameObjectWithTag(targetTag);
            if (go) target = go.transform;
        }

        currentHP = Mathf.Max(1, maxHP);
        OnHealthChanged?.Invoke(currentHP, maxHP);

        cols = GetComponentsInChildren<Collider2D>(true);

        rb.gravityScale = sideScroller ? 1f : 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        if (spriteRenderers == null || spriteRenderers.Length == 0)
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        useScaleFlipFallback = (spriteRenderers == null || spriteRenderers.Length == 0);

        if (targetLayers.value == 0)
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0) targetLayers |= (1 << playerLayer);
        }

        if (attackRange <= stopDistance && debugAttack)
            Debug.LogWarning("[Enemy] attackRange should be > stopDistance");
    }

    void Update()
    {
        if (debugAttack && Input.GetKeyDown(manualAttackKey))
        {
            Debug.Log("[Enemy] Manual attack key pressed");
            DealDamageEvent();
        }
    }

    void FixedUpdate()
    {
        if (!target || isDead) return;

        Vector2 toTarget = (Vector2)(target.position - transform.position);
        float dist = toTarget.magnitude;

        // --- Are we in retreat phase? ---
        bool isRetreating = retreatAfterAttack && (Time.time < lastAttackTime + retreatDuration);

        // --- Movement ---
        Vector2 desiredVel = Vector2.zero;

        if (isRetreating)
        {
            // Move away from the player
            if (sideScroller)
            {
                float dirX = -Mathf.Sign(toTarget.x); // opposite direction
                desiredVel = new Vector2(dirX * moveSpeed * retreatSpeedMultiplier, rb.linearVelocity.y);
            }
            else
            {
                desiredVel = -toTarget.normalized * moveSpeed * retreatSpeedMultiplier;
            }
        }
        else
        {
            // Normal chase behavior
            if (dist <= detectionRange && dist > stopDistance)
            {
                if (sideScroller)
                {
                    float dirX = Mathf.Sign(toTarget.x);
                    desiredVel = new Vector2(dirX * moveSpeed, rb.linearVelocity.y);
                }
                else
                {
                    desiredVel = toTarget.normalized * moveSpeed;
                }
            }
            else
            {
                desiredVel = sideScroller ? new Vector2(0f, rb.linearVelocity.y) : Vector2.zero;
            }
        }

        Vector2 vel = rb.linearVelocity;
        Vector2 step = Vector2.ClampMagnitude(desiredVel - vel, acceleration * Time.fixedDeltaTime);
        rb.linearVelocity = vel + step;

        // --- Animator locomotion ---
        if (sideScroller)
        {
            anim.SetFloat(SpeedHash, Mathf.Abs(rb.linearVelocity.x));
            anim.SetFloat(VSpeedHash, rb.linearVelocity.y);
        }
        else
        {
            anim.SetFloat(SpeedHash, rb.linearVelocity.magnitude);
            anim.SetFloat(MoveXHash, rb.linearVelocity.x);
            anim.SetFloat(MoveYHash, rb.linearVelocity.y);
        }

        // --- Facing (flip left/right) ---
        float faceX = sideScroller ? rb.linearVelocity.x :
                      (rb.linearVelocity.sqrMagnitude > 0.001f ? rb.linearVelocity.x : toTarget.x);

        if (Mathf.Abs(faceX) > 0.01f)
        {
            if (!useScaleFlipFallback)
            {
                bool flip = faceX < 0f;
                for (int i = 0; i < spriteRenderers.Length; i++)
                    if (spriteRenderers[i]) spriteRenderers[i].flipX = flip;
            }
            else
            {
                var s = transform.localScale;
                s.x = Mathf.Abs(s.x) * Mathf.Sign(faceX);
                transform.localScale = s;
            }
        }

        // --- Attack trigger (ONLY if not retreating) ---
        if (!isRetreating && dist <= attackRange && Time.time >= lastAttackTime + attackCooldown)
        {
            int min = Mathf.RoundToInt(attackIndices.x);
            int max = Mathf.RoundToInt(attackIndices.y);
            int pick = UnityEngine.Random.Range(min, max + 1);

            if (debugAttack) Debug.Log($"[Enemy] Attack fire: dist={dist:F2}, idx={pick}");

            anim.ResetTrigger(AttackTrig);
            anim.SetInteger(AttackIdxHash, pick);
            anim.SetTrigger(AttackTrig);

            lastAttackTime = Time.time; // also marks start of retreat window
        }

        // Continuous hurtbox (if using Start/End window events)
        if (attackWindowOpen)
            DoHitOverlap(singleFrame: false);
    }

    // ===== Health / Damage =====
    public void TakeDamage(int amount)
    {
        if (isDead) return;

        currentHP = Mathf.Max(0, currentHP - Mathf.Abs(amount));
        OnHealthChanged?.Invoke(currentHP, maxHP);

        if (currentHP == 0)
        {
            Die();
        }
        else
        {
            anim.SetTrigger(HitTrig);
        }
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        if (hurtAudio) hurtAudio.Play();

        DieInRoom();
        OnDied?.Invoke(this);

        if (rb)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            if (disablePhysicsOnDeath) rb.simulated = false;
        }

        if (componentsToDisableOnDeath != null)
            foreach (var b in componentsToDisableOnDeath)
                if (b) b.enabled = false;

        if (cols != null)
            foreach (var c in cols)
                if (c) c.enabled = false;

        anim.SetTrigger(DeathTrig);
        StartCoroutine(DespawnAfterDelay());
    }

    System.Collections.IEnumerator DespawnAfterDelay()
    {
        yield return new WaitForSeconds(deathDespawnDelay);
        Destroy(gameObject);
    }

    // ====== DAMAGE HOOKS (Animation Events) ======
    public void DealDamageEvent()
    {
        if (debugAttack) Debug.Log("[Enemy] DealDamageEvent()");
        if (attackAudio) attackAudio.Play();
        DoHitOverlap(singleFrame: true);
    }

    public void StartAttackWindow()
    {
        if (debugAttack) Debug.Log("[Enemy] StartAttackWindow()");
        attackWindowOpen = true;
        hitThisSwing.Clear();
    }

    public void EndAttackWindow()
    {
        if (debugAttack) Debug.Log("[Enemy] EndAttackWindow()");
        attackWindowOpen = false;
        hitThisSwing.Clear();
    }

    void DoHitOverlap(bool singleFrame)
    {
        float facingSign = GetFacingSign();
        Vector2 offset = attackOffset;
        offset.x *= facingSign;
        if (sideScroller) offset.y += sideScrollerYOffset;
        Vector2 attackPos = (Vector2)transform.position + offset;

        var hits = Physics2D.OverlapCircleAll(attackPos, attackRadius, targetLayers);

        if (debugAttack)
        {
            string maskBits = System.Convert.ToString(targetLayers.value, 2);
            Debug.Log($"[Enemy] Overlap @ {attackPos}, r={attackRadius}, hits={hits.Length}, mask={maskBits}");
            foreach (var h in hits) Debug.Log($"[Enemy]   -> collider '{h.name}' on layer {h.gameObject.layer}");
        }

        if (hits == null || hits.Length == 0) return;

        foreach (var h in hits)
        {
            if (h == null) continue;
            if (!singleFrame && hitThisSwing.Contains(h)) continue;

            var dmg = h.GetComponentInParent<IDamageable>() ?? h.GetComponentInChildren<IDamageable>();
            if (dmg != null)
            {
                dmg.TakeDamage(damage);
                ApplyKnockback(h);
                if (!singleFrame) hitThisSwing.Add(h);
                if (debugAttack) Debug.Log($"[Enemy]   -> damage applied to {h.name}");
            }
            else if (debugAttack)
            {
                Debug.Log($"[Enemy]   -> {h.name} not damageable (no IDamageable found)");
            }
        }
    }

    void ApplyKnockback(Collider2D h)
    {
        var prb = h.attachedRigidbody;
        if (prb)
        {
            Vector2 dir = ((Vector2)h.transform.position - (Vector2)transform.position).normalized;
            prb.AddForce(dir * knockbackForce, ForceMode2D.Impulse);
        }
    }

    float GetFacingSign()
    {
        if (!useScaleFlipFallback && spriteRenderers != null && spriteRenderers.Length > 0 && spriteRenderers[0] != null)
            return spriteRenderers[0].flipX ? -1f : 1f;

        float sx = transform.localScale.x;
        if (Mathf.Abs(sx) < 0.0001f) return 1f;
        return Mathf.Sign(sx);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.95f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = new Color(1f, 0f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.red;
        float sign = Application.isPlaying ? GetFacingSign() :
                     Mathf.Sign(transform.localScale.x == 0 ? 1 : transform.localScale.x);
        Vector2 pos = (Vector2)transform.position + new Vector2(attackOffset.x * sign, attackOffset.y + (sideScroller ? sideScrollerYOffset : 0f));
        Gizmos.DrawWireSphere(pos, attackRadius);
    }

    public void PlayHit() => anim.SetTrigger(HitTrig);
    public void PlayDeath() => anim.SetTrigger(DeathTrig);
}
