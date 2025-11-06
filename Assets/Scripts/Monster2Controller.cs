using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class EnemyController : RoomEnemy
{
    [Header("Target")]
    public Transform target;                 // Drag the player here, or will find by tag
    public string targetTag = "Player";

    [Header("Movement")]
    public bool sideScroller = false;        // true = X-only (platformer), false = top-down XY
    public float moveSpeed = 3f;
    public float detectionRange = 8f;        // start chasing inside this radius
    public float stopDistance = 1.0f;        // stop just short of target
    public float acceleration = 25f;         // smoothing

    [Header("Attacks")]
    public float attackRange = 1.2f;         // MUST be > stopDistance
    public float attackCooldown = 0.7f;
    public Vector2 attackIndices = new Vector2(0, 2); // inclusive [min,max] (0=Attack1,1=Attack2,2=ATTACK)

    [Header("Hit Logic (OverlapCircle)")]
    public LayerMask targetLayers;           // include Player layer
    public int damage = 10;
    public float knockbackForce = 4f;
    public float attackRadius = 0.6f;
    [Tooltip("Local-space offset when facing RIGHT. X auto-flips when facing left.")]
    public Vector2 attackOffset = new Vector2(0.6f, 0.0f);
    [Tooltip("Only used if sideScroller=true, to lift/lower hit circle.")]
    public float sideScrollerYOffset = 0f;

    [Header("Debug")]
    public bool debugAttack = false;                 // show detailed logs
    public KeyCode manualAttackKey = KeyCode.K;      // press to force an overlap test

    // Animator hashes
    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int VSpeedHash = Animator.StringToHash("VerticalSpeed"); // side-scroller only
    static readonly int MoveXHash = Animator.StringToHash("MoveX");         // top-down optional
    static readonly int MoveYHash = Animator.StringToHash("MoveY");         // top-down optional
    static readonly int AttackIdxHash = Animator.StringToHash("AttackIndex");
    static readonly int AttackTrig = Animator.StringToHash("Attack");
    static readonly int HitTrig = Animator.StringToHash("Hit");
    static readonly int DeathTrig = Animator.StringToHash("Death");

    Rigidbody2D rb;
    Animator anim;

    // Flipping support (child renderers or fallback to scale)
    [SerializeField] SpriteRenderer[] spriteRenderers; // optional: assign explicitly
    bool useScaleFlipFallback;

    // Attack timing state
    float lastAttackTime = -999f;
    bool attackWindowOpen = false;
    readonly HashSet<Collider2D> hitThisSwing = new HashSet<Collider2D>();

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();

        if (!target)
        {
            var go = GameObject.FindGameObjectWithTag(targetTag);
            if (go) target = go.transform;
        }

        // Physics defaults
        rb.gravityScale = sideScroller ? 1f : 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        // Find child renderers if not assigned
        if (spriteRenderers == null || spriteRenderers.Length == 0)
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        useScaleFlipFallback = (spriteRenderers == null || spriteRenderers.Length == 0);

        // If mask empty, try to include "Player"
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
        // Manual debug overlap (bypasses animation event)
        if (debugAttack && Input.GetKeyDown(manualAttackKey))
        {
            Debug.Log("[Enemy] Manual attack key pressed");
            DealDamageEvent();
        }
    }

    void FixedUpdate()
    {
        if (!target) return;

        Vector2 toTarget = (Vector2)(target.position - transform.position);
        float dist = toTarget.magnitude;

        // --- Movement ---
        Vector2 desiredVel = Vector2.zero;
        if (dist <= detectionRange && dist > stopDistance)
        {
            if (sideScroller)
            {
                float dirX = Mathf.Sign(toTarget.x);
                desiredVel = new Vector2(dirX * moveSpeed, rb.linearVelocity.y); // keep gravity Y
            }
            else
            {
                desiredVel = toTarget.normalized * moveSpeed;              // top-down XY
            }
        }
        else
        {
            desiredVel = sideScroller ? new Vector2(0f, rb.linearVelocity.y) : Vector2.zero;
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

        // --- Attack trigger ---
        if (dist <= attackRange && Time.time >= lastAttackTime + attackCooldown)
        {
            int min = Mathf.RoundToInt(attackIndices.x);
            int max = Mathf.RoundToInt(attackIndices.y);
            int pick = Random.Range(min, max + 1);

            if (debugAttack) Debug.Log($"[Enemy] Attack fire: dist={dist:F2}, idx={pick}");

            anim.ResetTrigger(AttackTrig);           // defensive clear
            anim.SetInteger(AttackIdxHash, pick);    // ensure AttackIndex is INT in Animator
            anim.SetTrigger(AttackTrig);

            lastAttackTime = Time.time;
        }

        // Continuous hurtbox (if using Start/End window events)
        if (attackWindowOpen)
            DoHitOverlap(singleFrame: false);
    }

    // ====== DAMAGE HOOKS (Animation Events) ======
    public void DealDamageEvent()
    {
        if (debugAttack) Debug.Log("[Enemy] DealDamageEvent()");
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

            // Look for damage receiver anywhere in the hit hierarchy
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
        // Ranges
        Gizmos.color = new Color(1f, 0.95f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = new Color(1f, 0f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Hit circle
        Gizmos.color = Color.red;
        float sign = Application.isPlaying ? GetFacingSign() :
                     Mathf.Sign(transform.localScale.x == 0 ? 1 : transform.localScale.x);
        Vector2 pos = (Vector2)transform.position + new Vector2(attackOffset.x * sign, attackOffset.y + (sideScroller ? sideScrollerYOffset : 0f));
        Gizmos.DrawWireSphere(pos, attackRadius);
    }

    // Optional external hooks
    public void PlayHit() => anim.SetTrigger(HitTrig);
    public void PlayDeath() => anim.SetTrigger(DeathTrig);
}
