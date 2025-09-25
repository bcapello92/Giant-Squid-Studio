using System.Collections;
using System.Collections.Generic;
using UnityEngine;



[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class BlobMonsterController : MonoBehaviour, IDamageable

{
    [Header("Target")]
    public Transform target;
    public string targetTag = "Player";

    [Header("Movement (slow blob)")]
    public float moveSpeed = 1.25f;
    public float detectionRange = 6f;
    public float stopDistance = 1.2f;
    public float acceleration = 12f;

    [Header("Shock AoE (burst)")]
    public float shockRange = 1.0f;     // radius around blob
    public int shockDamage = 8;
    public float shockKnockback = 2.5f;
    public float windupTime = 0.25f;    // pause before burst
    public float attackCooldown = 1.0f; // time between bursts
    public LayerMask targetLayers;      // set to Player only in Inspector

    [Header("Animator Params (hashed)")]
    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int AttackTrig = Animator.StringToHash("Attack");
    static readonly int HitTrig = Animator.StringToHash("Hit");
    static readonly int DeathTrig = Animator.StringToHash("Death");

    [Header("Debug")]
    public bool debugLogs = false;

    Rigidbody2D rb;
    Animator anim;
    bool isAttacking;
    float lastAttackTime = -999f;

    // (Optional) blob HP — implement if you want it damageable
    [Header("Health (optional)")]
    public int maxHP = 30;
    [SerializeField] int currentHP = 30;
    bool isDead = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();

        // Top-down physics
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        if (!target)
        {
            var go = GameObject.FindGameObjectWithTag(targetTag);
            if (go) target = go.transform;
        }

        // Ensure mask includes Player; if unset, default to Player or Everything (for testing)
        if (targetLayers.value == 0)
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0) targetLayers = 1 << playerLayer;
            else targetLayers = ~0; // Everything (test only)
        }

        if (currentHP <= 0) currentHP = maxHP;
    }

    void FixedUpdate()
    {
        if (isDead || !target) return;

        Vector2 toTarget = (Vector2)(target.position - transform.position);
        float dist = toTarget.magnitude;

        // Move (slow) until stopDistance, unless attacking
        Vector2 desiredVel = Vector2.zero;
        if (!isAttacking && dist <= detectionRange && dist > stopDistance)
            desiredVel = toTarget.normalized * moveSpeed;

        Vector2 vel = rb.linearVelocity;
        Vector2 step = Vector2.ClampMagnitude(desiredVel - vel, acceleration * Time.fixedDeltaTime);
        rb.linearVelocity = vel + step;

        anim.SetFloat(SpeedHash, rb.linearVelocity.magnitude);

        // Trigger attack if close enough and off cooldown
        if (!isAttacking && dist <= Mathf.Max(shockRange, stopDistance) && Time.time >= lastAttackTime + attackCooldown)
            StartCoroutine(AttackRoutine());
    }

    IEnumerator AttackRoutine()
    {
        isAttacking = true;

        // Play attack animation
        anim.ResetTrigger(AttackTrig);
        anim.SetTrigger(AttackTrig);

        // Wind-up (pause)
        Vector2 preVel = rb.linearVelocity;
        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(windupTime);

        // Single AoE burst
        DoShockBurst();

        // Cooldown
        lastAttackTime = Time.time;
        isAttacking = false;
    }

    void DoShockBurst()
    {
        Vector2 center = transform.position;

        // Overlap only on targetLayers (set to Player in Inspector)
        var hits = Physics2D.OverlapCircleAll(center, shockRange, targetLayers);

        if (debugLogs) Debug.Log($"[Blob] Shock burst @ {center}, r={shockRange}, hits={hits.Length}");

        foreach (var h in hits)
        {
            if (!h) continue;

            // Extra safeguard: only damage objects tagged Player (optional)
            if (!h.CompareTag(targetTag)) continue;

            var dmg = h.GetComponentInParent<IDamageable>() ?? h.GetComponentInChildren<IDamageable>();
            if (dmg != null)
            {
                dmg.TakeDamage(shockDamage);

                // Small outward knockback
                var prb = h.attachedRigidbody;
                if (prb)
                {
                    Vector2 dir = ((Vector2)h.transform.position - center).normalized;
                    prb.AddForce(dir * shockKnockback, ForceMode2D.Impulse);
                }

                if (debugLogs) Debug.Log($"[Blob]  -> damaged {h.name}");
            }
        }
    }

    // Optional: make blob damageable
    public void TakeDamage(int amount)
    {
        if (isDead) return;
        currentHP = Mathf.Max(0, currentHP - Mathf.Abs(amount));
        if (currentHP == 0)
        {
            isDead = true;
            rb.linearVelocity = Vector2.zero;
            anim.SetTrigger(DeathTrig);
        }
        else
        {
            anim.SetTrigger(HitTrig);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.95f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, stopDistance);

        Gizmos.color = new Color(1f, 0f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, shockRange);
    }
}
