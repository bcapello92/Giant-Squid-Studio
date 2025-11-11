using System.Collections;
using UnityEngine;

public class OctopusScript : RoomEnemy, IDamageable
{
    [Header("Health")]
    public int maxHP = 75;
    [SerializeField] int currentHP;

    public float deathDespawnDelay = 1.5f;
    private bool disablePhysicsOnDeath = true;

    [Header("Fade & Attack")]
    public float fadeSpeed = 2f;
    public float invisibleTime = 3f;
    public float visibleTime = 1.5f;
    public float attackRange = 2f;
    public float attackForce = 8f;
    public float attackCooldown = 2f;
    public float latchDuration = 2f;
    public int damagePerSecond = 1;

    [Header("Roaming")]
    public float roamSpeed = 2f;
    public float roamRadius = 3f; // max distance from roam origin
    private Vector2 roamTarget;
    private bool hasRoamTarget = false;
    private Vector2 currentRoamOrigin; // center for roaming
    Animator anim;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;
    private Collider2D cols;
    private Transform player;
    private Vector2 startPos;
    private bool isInvisible = false;
    private bool canAttack = true;
    private bool isLatched = false;
    bool isDead = false;
    static readonly int HitTrig = Animator.StringToHash("IsHit");
    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        cols = GetComponent<Collider2D>();
        anim = GetComponent<Animator>();
        currentHP = Mathf.Max(1, maxHP);

        // Rigidbody settings to prevent dropping
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.linearVelocity = Vector2.zero;

        startPos = transform.position;
        currentRoamOrigin = startPos;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;

        StartCoroutine(InvisibilityCycle());
    }

    void Update()
    {
        if (player == null) return;

        float distance = Vector2.Distance(transform.position, player.position);

        // Attack if visible and in range
        if (!isInvisible && canAttack && distance < attackRange)
        {
            StartCoroutine(AttackAndLatch());
        }

        // Flip sprite toward player
        spriteRenderer.flipX = (player.position.x < transform.position.x);

        // Stay attached while latched
        if (isLatched)
        {
            Vector3 latchOffset = new Vector3(0f, 0.3f, 0f);
            transform.position = player.position + latchOffset;
        }
        // Roam while invisible
        else if (isInvisible && !isLatched)
        {
            Roam();
        }
    }

    private IEnumerator InvisibilityCycle()
    {
        while (true)
        {
            // Go invisible
            yield return StartCoroutine(FadeTo(0.3f, fadeSpeed));
            isInvisible = true;
            yield return new WaitForSeconds(invisibleTime);

            // Reappear
            yield return StartCoroutine(FadeTo(1f, fadeSpeed));
            isInvisible = false;

            // Reset roam target for next cycle
            hasRoamTarget = false;

            yield return new WaitForSeconds(visibleTime);
        }
    }

    private IEnumerator FadeTo(float targetAlpha, float speed)
    {
        Color color = spriteRenderer.color;
        while (!Mathf.Approximately(color.a, targetAlpha))
        {
            color.a = Mathf.MoveTowards(color.a, targetAlpha, Time.deltaTime * speed);
            spriteRenderer.color = color;
            yield return null;
        }
    }

    private IEnumerator AttackAndLatch()
    {
        canAttack = false;

        // Dash toward player
        Vector2 direction = (player.position - transform.position).normalized;
        rb.linearVelocity = direction * attackForce;

        // Move until close enough or timeout
        float timer = 0f;
        while (Vector2.Distance(transform.position, player.position) > 0.3f && timer < 1f)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        // Stop and latch
        rb.linearVelocity = Vector2.zero;
        isLatched = true;

        // Start dealing damage
        StartCoroutine(DealDamageWhileLatched());

        yield return new WaitForSeconds(latchDuration);

        // Detach
        isLatched = false;

        // Update roam origin to point of detachment
        currentRoamOrigin = transform.position;

        // Short delay
        yield return new WaitForSeconds(0.2f);

        // Return partially toward start if desired, or just roam from detach
        float maxReturnTime = 3f;
        timer = 0f;
        rb.linearVelocity = Vector2.zero;

        while (Vector2.Distance(transform.position, startPos) > 0.1f && timer < maxReturnTime)
        {
            timer += Time.deltaTime;
            Vector2 moveDir = (startPos - (Vector2)transform.position).normalized;
            Vector2 newPos = (Vector2)transform.position + moveDir * Time.deltaTime * (attackForce * 0.3f);

            if (Vector2.Distance(newPos, startPos) < 0.05f)
                newPos = startPos;

            rb.MovePosition(newPos);
            yield return null;
        }

        rb.linearVelocity = Vector2.zero;
        // Optional: do not snap to startPos if you want roaming to continue from detach
        // transform.position = startPos;

        yield return new WaitForSeconds(attackCooldown);
        canAttack = true;
    }

    private IEnumerator DealDamageWhileLatched()
    {
        if (player == null) yield break;

        IDamageable damageable = player.GetComponent<IDamageable>();
        if (damageable == null) yield break;

        while (isLatched)
        {
            damageable.TakeDamage(damagePerSecond);
            yield return new WaitForSeconds(1f);
        }
    }

    // ------------------- Roaming -------------------
    private void Roam()
    {
        // Pick a new roam target if needed
        if (!hasRoamTarget || Vector2.Distance(transform.position, roamTarget) < 0.1f)
        {
            Vector2 randomOffset = Random.insideUnitCircle * roamRadius;
            Vector2 potentialTarget = currentRoamOrigin + randomOffset;

            // Clamp to camera bounds
            Vector3 viewportPos = Camera.main.WorldToViewportPoint(potentialTarget);
            viewportPos.x = Mathf.Clamp01(viewportPos.x);
            viewportPos.y = Mathf.Clamp01(viewportPos.y);
            roamTarget = Camera.main.ViewportToWorldPoint(viewportPos);

            hasRoamTarget = true;
        }

        Vector2 direction = (roamTarget - (Vector2)transform.position).normalized;
        Vector2 newPos = (Vector2)transform.position + direction * roamSpeed * Time.deltaTime;

        if (Vector2.Distance(newPos, roamTarget) < 0.05f)
            newPos = roamTarget;

        rb.MovePosition(newPos);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }

    IEnumerator DespawnAfterDelay()
    {
        yield return new WaitForSeconds(deathDespawnDelay);
        Destroy(gameObject);
    }

    public void TakeDamage(int amount)
    {
        if (isDead) return;

        currentHP = Mathf.Max(0, currentHP - Mathf.Abs(amount));
        if (currentHP == 0)
        {
            Die();
        }
        else
        {
            if (anim) anim.SetTrigger(HitTrig);
        }
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        // notify room ONCE
        DieInRoom();

        // the rest of your death stuff...
        if (rb)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            if (disablePhysicsOnDeath) rb.simulated = false;
        }

        if (cols != null)
            cols.enabled = false;

        StartCoroutine(DespawnAfterDelay());
    }
}