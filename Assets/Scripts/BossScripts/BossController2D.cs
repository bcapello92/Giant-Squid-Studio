using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class BossController2D : MonoBehaviour, IDamageable
{
    [Header("Refs")]
    public Animator anim;
    public Rigidbody2D rb;
    public Transform player; // assign the player; or find at Start

    [Header("Animation (walk)")]
    public string isMovingParam = "IsMoving";
    public string moveXParam = "MoveX";
    public string moveYParam = "MoveY";
    public SpriteRenderer spriteForFlip; // optional: flip to face motion/player

    [Header("Ground Check (Ray)")]
    public LayerMask groundMask;         // set in Inspector (Ground/Default/Tilemap, etc.)
    public Vector2 feetOffset = new Vector2(0f, -0.5f);  // from boss center to feet
    public float groundRayLen = 0.75f;   // how far below feet to check

    [Header("Health")]
    public int maxHP = 300;
    [SerializeField] int currentHP = 300;
    public bool IsDead { get; private set; }

    [Header("I-frames")]
    public bool invulnerableDuringAppear = true;
    public bool invulnerableDuringSky = true;

    public bool IsInvulnerable { get; private set; }
    int iframeDepth = 0;

    void BeginIFrames()
    {
        iframeDepth++;
        if (iframeDepth == 1) IsInvulnerable = true;
    }
    bool IsGroundBelow(out RaycastHit2D hit)
    {
        Vector2 origin = (Vector2)transform.position + feetOffset;
        hit = Physics2D.Raycast(origin, Vector2.down, groundRayLen, groundMask);
        return hit.collider != null;
    }
    void EndIFrames()
    {
        if (iframeDepth <= 0) return;
        iframeDepth--;
        if (iframeDepth == 0) IsInvulnerable = false;
    }

    [Header("AI")]
    public float idleTimeBetweenAttacks = 0.8f;
    [Range(0f, 1f)] public float skyAttackChance = 0.35f;
    public float groundAttackRange = 3.5f;

    [Header("Movement")]
    public float chaseSpeed = 2.0f;
    public float skyRiseHeight = 6f;   // how high to go before diving
    public float skyRiseSpeed = 12f;
    public float skyDiveSpeed = 18f;

    [Header("Hitboxes")]
    public DamageHitbox2D groundHitbox;
    public DamageHitbox2D slamHitbox;

    [Header("VFX/SFX (optional)")]
    public GameObject appearVfx;
    public GameObject deathVfx;

    bool busy;         // true while locked in an attack or cinematic
    bool appeared;     // after Appear animation finishes
    Vector2 spawnPos;

    void Awake()
    {
        if (!anim) anim = GetComponent<Animator>();
        if (!rb) rb = GetComponent<Rigidbody2D>();
        currentHP = maxHP;
        spawnPos = transform.position;
        DisableAllHitboxes();
    }

    void Start()
    {
        // …
        if (invulnerableDuringAppear) BeginIFrames();
        StartCoroutine(AppearRoutine());
    }

    public void OnAppearDone()
    {
        if (appeared) return;
        anim.SetTrigger("Trig_AppearDone");
        appeared = true;
        busy = false;

        if (invulnerableDuringAppear) EndIFrames();   // <— vulnerable after appear
        StartCoroutine(AILoop());
    }


    void Update()
    {
        if (IsDead || !appeared) return;

        Vector2 vel = rb ? rb.linearVelocity : Vector2.zero;

        // Simple chase when not in an attack/cinematic
        if (!busy && player)
        {
            Vector2 toPlayer = (player.position - transform.position);
            float dist = toPlayer.magnitude;

            if (dist > groundAttackRange * 0.8f)
            {
                vel = toPlayer.normalized * chaseSpeed;
            }
            else
            {
                vel = Vector2.zero;
            }
        }

        // Apply velocity
        if (rb) rb.linearVelocity = vel;

        // ---- Walk animation params ----
        bool moving = vel.sqrMagnitude > 0.0001f;
        if (anim && !string.IsNullOrEmpty(isMovingParam))
            anim.SetBool(isMovingParam, moving);

        if (anim)
        {
            // Optional direction (for blend tree or just for facing)
            Vector2 dir = moving ? vel.normalized : (player ? (Vector2)(player.position - transform.position).normalized : Vector2.right);
            if (!string.IsNullOrEmpty(moveXParam)) anim.SetFloat(moveXParam, dir.x);
            if (!string.IsNullOrEmpty(moveYParam)) anim.SetFloat(moveYParam, dir.y);
        }

        // Optional sprite flip (face movement or the player)
        if (spriteForFlip)
        {
            float faceX = moving ? vel.x : (player ? (player.position.x - transform.position.x) : 1f);
            spriteForFlip.flipX = faceX < 0f;
        }
    }


    IEnumerator AppearRoutine()
    {
        busy = true;
        // Play Appear clip via state machine (already at Appear from Entry)
        // When your Appear clip reaches its last frame, call the Animation Event: OnAppearDone()
        yield return null; // wait at least one frame so Animator starts
    }

   
    IEnumerator AILoop()
    {
        var wait = new WaitForSeconds(idleTimeBetweenAttacks);

        while (!IsDead)
        {
            // Pick attack
            bool doSky = (Random.value < skyAttackChance);

            if (doSky && player)
            {
                yield return StartCoroutine(SkyAttackRoutine());
            }
            else
            {
                yield return StartCoroutine(GroundAttackRoutine());
            }

            // small idle
            float t = 0f;
            while (t < idleTimeBetweenAttacks && !IsDead)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }
    }

    IEnumerator GroundAttackRoutine()
    {
        busy = true;
        if (rb) rb.linearVelocity = Vector2.zero;
        if (anim && !string.IsNullOrEmpty(isMovingParam)) anim.SetBool(isMovingParam, false);

        rb.linearVelocity = Vector2.zero;
        DisableAllHitboxes();

        anim.ResetTrigger("Trig_Sky");
        anim.SetTrigger("Trig_Attack");

        // Animation Events will call StartGroundDamage() and StopGroundDamage()
        // Wait for the attack clip to finish by monitoring a short lock
        float safety = 2.5f; // safety timeout
        while (safety > 0f && !IsDead)
        {
            safety -= Time.deltaTime;
            // you can also check Animator state normalizedTime to exit earlier
            yield return null;
        }

        DisableAllHitboxes();
        busy = false;
    }

    IEnumerator SkyAttackRoutine()
    {
        busy = true;
        if (rb) rb.linearVelocity = Vector2.zero;
        DisableAllHitboxes();

        // Enter the sky attack state
        anim.ResetTrigger("Trig_Attack");
        anim.SetTrigger("Trig_Sky");

        if (invulnerableDuringSky) BeginIFrames(); // airborne i-frames ON

        // ---------- PHASE 1: Rise ----------
        // Go up to a fixed height above spawn (or current) Y
        float targetY = (spawnPos.y != 0f ? spawnPos.y : transform.position.y) + skyRiseHeight;
        const float riseEps = 0.03f;
        float riseTimeout = 2.0f;
        while (!IsDead && riseTimeout > 0f && transform.position.y < targetY - riseEps)
        {
            riseTimeout -= Time.deltaTime;
            Vector2 next = Vector2.MoveTowards(transform.position,
                                               new Vector2(transform.position.x, targetY),
                                               skyRiseSpeed * Time.deltaTime);
            rb.MovePosition(next);
            yield return null;
        }

        // ---------- PHASE 2: Align above player ----------
        if (!IsDead && player)
        {
            Vector2 align = new Vector2(player.position.x, transform.position.y);
            rb.MovePosition(align);
        }
        yield return new WaitForSeconds(0.15f); // small telegraph pause

        // ---------- PHASE 3: Dive ----------
        float diveTimeout = 3.0f; // safety: bail if ground never detected
                                  // Configure your ground check here:
                                  // - groundMask: LayerMask including Ground/Tilemap/etc.
                                  // - feetOffset: (0, negative) from boss center to "feet"
                                  // - groundRayLen: small distance below feet to detect impact
        while (!IsDead && diveTimeout > 0f)
        {
            diveTimeout -= Time.deltaTime;

            // Move down
            Vector2 next = (Vector2)transform.position + Vector2.down * (skyDiveSpeed * Time.deltaTime);
            rb.MovePosition(next);

            // Ground hit test (single ray)
            Vector2 origin = (Vector2)transform.position + feetOffset;
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, groundRayLen, groundMask);
            if (hit.collider)
            {
                // Impact: brief damage window
                StartSlamDamage();
                yield return new WaitForSeconds(0.10f);
                StopSlamDamage();
                break;
            }

            yield return null;
        }

        if (invulnerableDuringSky) EndIFrames(); // airborne i-frames OFF

        // Tell animator we’re done with the sky state (ensures exit even if clip is long)
        anim.SetTrigger("Trig_SkyDone");

        // Small recovery before resuming AI
        yield return new WaitForSeconds(0.30f);

        DisableAllHitboxes();
        busy = false;
    }


    void DisableAllHitboxes()
    {
        if (groundHitbox) groundHitbox.enabled = false;
        if (slamHitbox) slamHitbox.enabled = false;
    }

    // ---- Animation Event hooks (call these inside your clips) ----
    // Ground attack windows
    public void StartGroundDamage() { if (groundHitbox) groundHitbox.enabled = true; }
    public void StopGroundDamage() { if (groundHitbox) groundHitbox.enabled = false; }

    // Sky slam windows
    public void StartSlamDamage() { if (slamHitbox) slamHitbox.enabled = true; }
    public void StopSlamDamage() { if (slamHitbox) slamHitbox.enabled = false; }

    // Optional: small camera shake / dust VFX events
    public void OnSkyDiveStart() { /* play whoosh */ }
    public void OnSkyImpact() { /* dust ring, shake */ }

    // ---- Damage & death ----
    public void TakeDamage(int amount)
    {
        if (IsDead || IsInvulnerable) return;   // <— ignore while invulnerable

        currentHP = Mathf.Max(0, currentHP - Mathf.Abs(amount));
        if (currentHP == 0) { Die(); return; }

        if (!busy) // your existing flinch rule
        {
            anim.SetTrigger("Trig_Damage");
            rb.linearVelocity = Vector2.zero;
        }
    }


    void Die()
    {
        if (IsDead) return;
        IsDead = true;

        DisableAllHitboxes();
        rb.linearVelocity = Vector2.zero;

        anim.SetBool("IsDead", true);
        anim.SetTrigger("Trig_Death");

        if (deathVfx) Instantiate(deathVfx, transform.position, Quaternion.identity);

        // cleanup after animation finishes (use Animation Event, or a short delay)
        StartCoroutine(DespawnAfter(2.5f));
    }

    IEnumerator DespawnAfter(float s)
    {
        yield return new WaitForSeconds(s);
        Destroy(gameObject);
    }
}
