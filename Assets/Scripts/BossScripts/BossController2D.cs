using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody2D))]
public class BossController2D : MonoBehaviour, IDamageable // implement IDamageableEx if you want rich logs
{
    // -------------------- REFS --------------------
    [Header("Core Refs")]
    public Animator anim;
    public Rigidbody2D rb;
    public Transform player;                     // auto-found by tag if left empty
    [Tooltip("Main body collider (hurtbox) to disable during vanish, optional.")]
    public Collider2D bodyCollider;
    [Tooltip("Sprite that gets hidden during vanish, optional.")]
    public SpriteRenderer spriteForHide;

    [Header("Hitboxes")]
    public DamageHitbox2D groundHitbox;          // enable during ground swing
    public DamageHitbox2D slamHitbox;            // enable on sky impact

    // -------------------- HEALTH --------------------
    [Header("Health")]
    public int maxHP = 300;
    [SerializeField] int currentHP;
    public bool IsDead { get; private set; }

    // -------------------- AI / MOVEMENT --------------------
    [Header("AI")]
    public float idleTimeBetweenAttacks = 0.8f;
    [Range(0f, 1f)] public float skyAttackChance = 0.35f;
    public float groundAttackRange = 3.5f;
    public float chaseSpeed = 2.0f;

    [Header("Walk Animation Params (optional)")]
    public string isMovingParam = "IsMoving";
    public string moveXParam = "MoveX";
    public string moveYParam = "MoveY";

    // -------------------- SKY / VANISH --------------------
    [Header("Sky Attack Tuning")]
    public float skyRiseHeight = 6f;
    public float skyRiseSpeed = 12f;
    public float skyDiveSpeed = 18f;
    public float alignSpeed = 10f;       // slide speed over target X
    public float telegraphDelay = 0.18f;    // pause before diving

    [Header("Vanish")]
    public bool invulnerableDuringVanish = true;
    public bool hideSpriteDuringVanish = true;
    public float vanishTeleportYMargin = 0.5f; // reappear just below ceiling

    // -------------------- I-FRAMES --------------------
    [Header("I-frames")]
    public bool invulnerableDuringAppear = true;
    public bool invulnerableDuringSky = true;

    // -------------------- ARENA LIMITS --------------------
    [Header("Arena Limits")]
    public Collider2D arenaCollider;     // Box/Composite/Polygon collider that bounds the room
    public float wallMargin = 0.5f;
    public float ceilingMargin = 0.5f;
    public float floorMargin = 0.1f;

    // -------------------- GROUND CHECK --------------------
    [Header("Ground Check (Ray)")]
    public LayerMask groundMask;                     // include Tilemap/Ground layers
    public Vector2 feetOffset = new Vector2(0f, -0.5f);
    public float groundRayLen = 0.8f;

    // -------------------- ANIM PARAM NAMES --------------------
    static readonly string P_TRIG_APPEAR_DONE = "Trig_AppearDone";
    static readonly string P_TRIG_ATTACK = "Trig_Attack";
    static readonly string P_TRIG_SKY = "Trig_Sky";
    static readonly string P_TRIG_SKY_DONE = "Trig_SkyDone";
    static readonly string P_TRIG_VANISH = "Trig_Vanish";
    static readonly string P_TRIG_VANISH_DONE = "Trig_VanishDone";
    static readonly string P_TRIG_DAMAGE = "Trig_Damage";
    static readonly string P_BOOL_DEAD = "IsDead";

    // -------------------- STATE --------------------
    bool busy;               // true while in attacks/cinematics
    bool appeared;           // after appear finishes
    public bool IsInvulnerable { get; private set; }
    int iframeDepth = 0;     // nested i-frames safety
    Vector2 spawnPos;

    // -------------------- UNITY --------------------
    void Reset()
    {
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        spriteForHide = GetComponentInChildren<SpriteRenderer>();
        bodyCollider = GetComponent<Collider2D>();
    }

    void Awake()
    {
        if (!anim) anim = GetComponent<Animator>();
        if (!rb) rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;

        currentHP = maxHP;
        spawnPos = transform.position;

        DisableAllHitboxes();

        // Try auto-find player by tag
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
        if (!spriteForHide) spriteForHide = GetComponentInChildren<SpriteRenderer>(true);
        if (!bodyCollider) bodyCollider = GetComponent<Collider2D>();
    }

    void Start()
    {
        // Start Appear cinematic (Animator Entry → Appear)
        busy = true;
        if (invulnerableDuringAppear) BeginIFrames();
        StartCoroutine(AppearRoutine());
    }

    void Update()
    {
        if (IsDead || !appeared) return;

        // Simple chase if not in an attack
        Vector2 vel = Vector2.zero;
        if (!busy && player)
        {
            Vector2 toPlayer = (player.position - transform.position);
            float dist = toPlayer.magnitude;
            vel = (dist > groundAttackRange * 0.8f) ? toPlayer.normalized * chaseSpeed : Vector2.zero;
        }

        rb.linearVelocity = vel;

        // Drive walk animation (optional)
        bool moving = vel.sqrMagnitude > 0.0001f;
        if (anim && !string.IsNullOrEmpty(isMovingParam)) anim.SetBool(isMovingParam, moving);
        if (anim)
        {
            Vector2 dir = moving ? vel.normalized : (player ? (Vector2)(player.position - transform.position).normalized : Vector2.right);
            if (!string.IsNullOrEmpty(moveXParam)) anim.SetFloat(moveXParam, dir.x);
            if (!string.IsNullOrEmpty(moveYParam)) anim.SetFloat(moveYParam, dir.y);
        }
    }

    // -------------------- APPEAR --------------------
    IEnumerator AppearRoutine()
    {
        // If the animation event never fires, safety-finish in 2s
        float safety = 2f;
        while (!appeared && safety > 0f)
        {
            safety -= Time.deltaTime;
            yield return null;
        }
        if (!appeared) OnAppearDone();
    }

    // Animation Event on last frame of Appear
    public void OnAppearDone()
    {
        if (appeared) return;
        SafeSetTrigger(anim, P_TRIG_APPEAR_DONE);
        appeared = true;
        busy = false;
        if (invulnerableDuringAppear) EndIFrames();

        StartCoroutine(AILoop());
    }

    // -------------------- AI LOOP --------------------
    IEnumerator AILoop()
    {
        while (!IsDead)
        {
            // pick attack
            bool doSky = (Random.value < skyAttackChance);
            if (doSky && player)
                yield return StartCoroutine(VanishIntoSkyRoutine());
            else
                yield return StartCoroutine(GroundAttackRoutine());

            // small idle
            float t = 0f;
            while (t < idleTimeBetweenAttacks && !IsDead)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }
    }

    // -------------------- GROUND ATTACK --------------------
    IEnumerator GroundAttackRoutine()
    {
        busy = true;
        rb.linearVelocity = Vector2.zero;
        DisableAllHitboxes();

        SafeSetTrigger(anim, P_TRIG_ATTACK);

        // Animation events should call StartGroundDamage/StopGroundDamage.
        // Safety timeout so we never hang.
        float safety = 2.5f;
        while (safety > 0f && !IsDead)
        {
            safety -= Time.deltaTime;
            yield return null;
        }

        DisableAllHitboxes();
        busy = false;
    }

    // Called by animation events in ground swing
    public void StartGroundDamage() { if (groundHitbox) groundHitbox.enabled = true; }
    public void StopGroundDamage() { if (groundHitbox) groundHitbox.enabled = false; }

    // -------------------- VANISH → SKY ATTACK --------------------
    IEnumerator VanishIntoSkyRoutine()
    {
        busy = true;
        rb.linearVelocity = Vector2.zero;
        DisableAllHitboxes();

        // Enter Vanish state
        SafeSetTrigger(anim, P_TRIG_VANISH);
        OnVanishStart(); // in case events are missing, apply immediately

        // wait a short beat so the vanish pose shows
        yield return new WaitForSeconds(0.25f);

        // Compute reappear position (near ceiling, clamped)
        float ceilingY = ArenaCeilingY();
        float reappearY = ClampArenaY(ceilingY - vanishTeleportYMargin);
        float targetX = player ? ClampArenaX(player.position.x) : ClampArenaX(transform.position.x);

        // Teleport invisible boss to the air start
        rb.position = new Vector2(targetX, reappearY);

        // Hand off to aerial flow
        yield return StartCoroutine(SkyAttackRoutine_ReenterFromAir());

        busy = false;
    }

    // Events (optional) to place in Vanish clip
    public void OnVanishStart()
    {
        if (invulnerableDuringVanish) BeginIFrames();
        if (hideSpriteDuringVanish && spriteForHide) spriteForHide.enabled = false;
        if (bodyCollider) bodyCollider.enabled = false;
        DisableAllHitboxes();
    }
    public void OnVanishEnd() { /* if your clip re-materializes visually, you can re-enable sprite here */ }
    public void OnVanishDone() { SafeSetTrigger(anim, P_TRIG_VANISH_DONE); }

    // Assumes we already teleported near the ceiling; reappear, telegraph, dive
    IEnumerator SkyAttackRoutine_ReenterFromAir()
    {
        SafeSetTrigger(anim, P_TRIG_SKY);

        // Keep invuln from vanish and ensure invuln during sky
        if (invulnerableDuringSky && !IsInvulnerable) BeginIFrames();

        // Reappear visually before telegraph
        if (hideSpriteDuringVanish && spriteForHide) spriteForHide.enabled = true;

        // Lock X at reappearance and pause (no continuous tracking)
        float lockedTargetX = rb.position.x;
        if (telegraphDelay > 0f) yield return new WaitForSeconds(telegraphDelay);

        // Dive straight down, clamped to arena floor
        float diveTimeout = 2.0f;
        float arenaFloorY = ArenaFloorY();

        while (!IsDead && diveTimeout > 0f)
        {
            diveTimeout -= Time.deltaTime;

            Vector2 next = new Vector2(
                ClampArenaX(lockedTargetX),
                Mathf.Max(transform.position.y - skyDiveSpeed * Time.deltaTime, arenaFloorY)
            );
            rb.MovePosition(next);

            // Ground check: ray from feet
            Vector2 origin = (Vector2)transform.position + feetOffset;
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, groundRayLen, groundMask);
            bool hitFloor = (transform.position.y <= arenaFloorY + 0.02f);

            if (hit.collider || hitFloor)
            {
                StartSlamDamage();
                yield return new WaitForSeconds(0.10f);
                StopSlamDamage();
                break;
            }
            yield return null;
        }

        // End invulnerability windows
        if (invulnerableDuringSky) EndIFrames();
        if (invulnerableDuringVanish) EndIFrames();

        // Restore collision if we hid it
        if (bodyCollider) bodyCollider.enabled = true;

        SafeSetTrigger(anim, P_TRIG_SKY_DONE);
        yield return new WaitForSeconds(0.25f);

        DisableAllHitboxes();
    }

    // Called on impact window during sky slam
    public void StartSlamDamage() { if (slamHitbox) slamHitbox.enabled = true; }
    public void StopSlamDamage() { if (slamHitbox) slamHitbox.enabled = false; }

    // -------------------- DAMAGE / DEATH --------------------
    public void TakeDamage(int amount)
    {
        if (IsDead || IsInvulnerable) return;

        currentHP = Mathf.Max(0, currentHP - Mathf.Abs(amount));
        if (currentHP == 0) { Die(); return; }

        if (!busy)
        {
            SafeSetTrigger(anim, P_TRIG_DAMAGE);
            rb.linearVelocity = Vector2.zero;
        }
    }

    void Die()
    {
        if (IsDead) return;
        IsDead = true;

        DisableAllHitboxes();
        rb.linearVelocity = Vector2.zero;
        anim.SetBool(P_BOOL_DEAD, true);
        SafeSetTrigger(anim, "Trig_Death");

        // destroy after short delay (or use animation event)
        StartCoroutine(DespawnAfter(2.5f));
    }

    IEnumerator DespawnAfter(float s)
    {
        yield return new WaitForSeconds(s);
        Destroy(gameObject);
    }

    // -------------------- UTIL: HITBOX / I-FRAME / ANIM --------------------
    void DisableAllHitboxes()
    {
        if (groundHitbox) groundHitbox.enabled = false;
        if (slamHitbox) slamHitbox.enabled = false;
    }

    void BeginIFrames()
    {
        iframeDepth++;
        if (iframeDepth == 1) IsInvulnerable = true;
    }

    void EndIFrames()
    {
        if (iframeDepth <= 0) return;
        iframeDepth--;
        if (iframeDepth == 0) IsInvulnerable = false;
    }

    static bool HasParam(Animator a, string name, AnimatorControllerParameterType type)
    {
        if (!a || string.IsNullOrEmpty(name)) return false;
        var ps = a.parameters;
        for (int i = 0; i < ps.Length; i++)
            if (ps[i].name == name && ps[i].type == type) return true;
        return false;
    }
    static void SafeSetTrigger(Animator a, string name)
    {
        if (HasParam(a, name, AnimatorControllerParameterType.Trigger))
            a.SetTrigger(name);
    }

    // -------------------- UTIL: ARENA & CLAMP --------------------
    bool TryGetArena(out Bounds b)
    {
        if (arenaCollider) { b = arenaCollider.bounds; return true; }
        b = default; return false;
    }
    float ClampArenaX(float x)
    {
        if (TryGetArena(out var b)) return Mathf.Clamp(x, b.min.x + wallMargin, b.max.x - wallMargin);
        return x;
    }
    float ClampArenaY(float y)
    {
        if (TryGetArena(out var b)) return Mathf.Clamp(y, b.min.y + floorMargin, b.max.y - ceilingMargin);
        return y;
    }
    float ArenaCeilingY() => TryGetArena(out var b) ? b.max.y - ceilingMargin : transform.position.y + skyRiseHeight;
    float ArenaFloorY() => TryGetArena(out var b) ? b.min.y + floorMargin : transform.position.y - 100f;

    // -------------------- GIZMOS (debug ground ray) --------------------
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector2 ro = (Vector2)transform.position + feetOffset;
        Gizmos.DrawLine(ro, ro + Vector2.down * groundRayLen);
    }
}
