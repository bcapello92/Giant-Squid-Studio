using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    [Header("Target")]
    public Transform player;                 // Drag your player here (or find by tag in Start)
    public LayerMask playerLayer;

    [Header("Movement")]
    public float baseSpeed = 3f;
    [Range(0.1f, 5f)] public float speedModifier = 1f;  // movement multiplier
    public float detectionRange = 8f;
    public float attackRange = 1.2f;
    public float stopDistance = 0.8f;                   // how close to stop before attack wind-up

    [Header("Attack")]
    public float attackCooldown = 0.7f;                 // seconds between attacks
    public int damage = 10;
    public float knockbackForce = 4f;

    private Rigidbody2D rb;
    private Animator anim;
    private Vector2 desiredMove;
    private float lastAttackTime = -999f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        if (!player)
        {
            GameObject go = GameObject.FindGameObjectWithTag("Player");
            if (go) player = go.transform;
        }
    }

    void Update()
    {
        if (!player) return;

        Vector2 toPlayer = (player.position - transform.position);
        float dist = toPlayer.magnitude;

        // Decide state
        bool canSeePlayer = dist <= detectionRange;
        bool inAttack = dist <= attackRange;

        if (canSeePlayer && !inAttack)
        {
            desiredMove = toPlayer.normalized; // chase
        }
        else
        {
            desiredMove = Vector2.zero;        // idle or attacking => stop moving
        }

        // Animation parameters (adjust to your Animator)
        if (anim)
        {
            anim.SetFloat("Speed", desiredMove.sqrMagnitude); // >0 => run, else idle
            anim.SetFloat("MoveX", desiredMove.x);
            anim.SetFloat("MoveY", desiredMove.y);
        }

        // Try attack when in range & off cooldown
        if (inAttack && Time.time >= lastAttackTime + attackCooldown)
        {
            lastAttackTime = Time.time;
            if (anim) anim.SetTrigger("Attack"); else DealDamage(); // fallback
        }

        // Optional: face player left/right (side scroller)
        if (Mathf.Abs(toPlayer.x) > 0.05f)
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * Mathf.Sign(toPlayer.x);
            transform.localScale = scale;
        }
    }

    void FixedUpdate()
    {
        if (!player) return;

        float currentSpeed = baseSpeed * speedModifier;
        Vector2 currentPos = rb.position;

        // Stop a little before the player to avoid jitter
        if (desiredMove != Vector2.zero)
        {
            Vector2 toPlayer = (Vector2)player.position - currentPos;
            if (toPlayer.magnitude > stopDistance)
            {
                Vector2 next = currentPos + desiredMove * currentSpeed * Time.fixedDeltaTime;
                rb.MovePosition(next);
            }
        }
    }

    // Called by an Animation Event on the attack clip at the “hit” frame
    public void DealDamage()
    {
        // Small overlap circle at NPC’s front (or just centered)
        Vector2 hitPos = (Vector2)transform.position;
        Collider2D hit = Physics2D.OverlapCircle(hitPos, attackRange, playerLayer);
        if (hit == null) return;

        // Apply damage if player has a Health component
      //  var hp = hit.GetComponent<Health>();
       // if (hp) hp.TakeDamage(damage);

        // Optional knockback
        var prb = hit.attachedRigidbody;
        if (prb)
        {
            Vector2 dir = ((Vector2)hit.transform.position - (Vector2)transform.position).normalized;
            prb.AddForce(dir * knockbackForce, ForceMode2D.Impulse);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
