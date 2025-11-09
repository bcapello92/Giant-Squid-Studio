using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody2D))]
public class BossController2D : RoomEnemy, IDamageable
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
    [Header("Chase")]
    public float chaseAccel = 50f;  // smoothing for velocity changes

    [Header("Walk Animation Params (optional)")]
    public string isMovingParam = "IsMoving";
    public string moveXParam = "MoveX";
    public string moveYParam = "MoveY";

    // -------------------- SKY / VANISH --------------------
    [Header("Sky Attack Targeting")]
    public bool trackDuringHover = true;   // follow player's X before diving
    public float hoverTrackTime = 0.8f;    // how long to track before dive
    public float hoverMaxXSpeed = 10f;     // max horizontal speed while tracking
    public float hoverSnapEpsilon = 0.05f; // if close enough, stop moving X

    [Header("Sky Attack Tuning")]
    public float skyRiseHeight = 6f;
    public float skyRiseSpeed = 12f;
    public float skyDiveSpeed = 18f;
    public float alignSpeed = 10f;       // slide speed over target X
    public float telegraphDelay = 0.18f; // pause before diving
    public float playerTopAimXOffset = 0f;

    [Header("Vanish")]
    public bool invulnerableDuringVanish = true;
    public bool hideSpriteDuringVanish = true;
    public float vanishTeleportYMargin = 0.5f; // reappear just below ceiling
    public bool useOnVanishDoneEvent = true;   // wait for anim event to finish vanish
    public float vanishEventTimeout = 1.5f;    // failsafe if event missing

    [Header("Melee (Overlap)")]
    public int meleeDamage = 12;
    public float meleeRadius = 0.6f;
    public Vector2 meleeOffset = new Vector2(0.7f, 0.0f); // local, when facing right
    public LayerMask meleeTargets; // include Player
    public float meleeKnockback = 4f;


    // -------------------- I-FRAMES --------------------
    [Header("I-frames")]
    public bool invulnerableDuringAppear = true;
    public bool invulnerableDuringSky = true;

    // -------------------- ARENA LIMITS --------------------
    [Header("Arena Limits")]
    public Collider2D arenaCollider;     // Box/Composite/Polygon collider that bounds the room (always enabled)
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

    [Header("Animator State Names (match your controller)")]
    public string vanishStateName = "vanish";
    public string skyStateName = "attack from sky";
    public string impactStateName = "impact";

    // -------------------- STATE --------------------
    bool busy;               // true while in attacks/cinematics
    bool appeared;           // after appear finishes
    public bool IsInvulnerable { get; private set; }
    int iframeDepth = 0;     // nested i-frames safety
    Vector2 spawnPos;

    // vanish event flag
    bool vanishDoneFlag;

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

        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
        if (!spriteForHide) spriteForHide = GetComponentInChildren<SpriteRenderer>(true);
        if (!bodyCollider) bodyCollider = GetComponent<Collider2D>();

        // Guard: arenaCollider must not be the boss's own collider
        if (arenaCollider && bodyCollider && ReferenceEquals(arenaCollider, bodyCollider))
            Debug.LogWarning("[Boss] arenaCollider references bodyCollider. Use a separate, always-enabled room collider.");
    }

    void Start()
    {
        busy = true;
        if (invulnerableDuringAppear) BeginIFrames();
        StartCoroutine(AppearRoutine());
    }

    void FixedUpdate()
    {
        if (IsDead || !appeared) return;

        // don’t move while performing attacks/cinematics
        if (busy || !player)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 toPlayer = (Vector2)(player.position - transform.position);
        float dist = toPlayer.magnitude;

        Vector2 desired = (dist > groundAttackRange * 0.8f) ? toPlayer.normalized * chaseSpeed
                                                            : Vector2.zero;

        // smooth toward desired velocity (prevents jitter when starting/stopping)
        Vector2 v = rb.linearVelocity;
        Vector2 step = Vector2.ClampMagnitude(desired - v, chaseAccel * Time.fixedDeltaTime);
        rb.linearVelocity = v + step;

        // optional: drive walk params
        bool moving = rb.linearVelocity.sqrMagnitude > 0.001f;
        if (anim && !string.IsNullOrEmpty(isMovingParam)) anim.SetBool(isMovingParam, moving);
        if (anim)
        {
            Vector2 dir = moving ? rb.linearVelocity.normalized : (player ? (Vector2)(player.position - transform.position).normalized : Vector2.right);
            if (!string.IsNullOrEmpty(moveXParam)) anim.SetFloat(moveXParam, dir.x);
            if (!string.IsNullOrEmpty(moveYParam)) anim.SetFloat(moveYParam, dir.y);
        }
    }


    // -------------------- APPEAR --------------------
    IEnumerator AppearRoutine()
    {
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
            bool doSky = (Random.value < skyAttackChance);
            if (doSky && player)
                yield return StartCoroutine(VanishIntoSkyRoutine());
            else
                yield return StartCoroutine(GroundAttackRoutine());

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

        float safety = 2.5f;
        while (safety > 0f && !IsDead)
        {
            safety -= Time.deltaTime;
            yield return null;
        }

        DisableAllHitboxes();
        busy = false;
    }
    float FacingSign()
    {
        // if you flip sprites, prefer that; else use localScale.x
        var sr = spriteForHide;
        if (sr) return sr.flipX ? -1f : 1f;
        float sx = transform.localScale.x;
        return Mathf.Approximately(sx, 0f) ? 1f : Mathf.Sign(sx);
    }

    public void MeleeHitEvent() // call this from the peak frame of the attack animation
    {
        Vector2 pos = (Vector2)transform.position + new Vector2(meleeOffset.x * FacingSign(), meleeOffset.y);
        var hits = Physics2D.OverlapCircleAll(pos, meleeRadius, meleeTargets);
        foreach (var h in hits)
        {
            if (!h) continue;
            var dmg = h.GetComponentInParent<IDamageable>() ?? h.GetComponentInChildren<IDamageable>();
            if (dmg != null)
            {
                dmg.TakeDamage(meleeDamage);
                var prb = h.attachedRigidbody;
                if (prb) prb.AddForce(((Vector2)h.transform.position - (Vector2)transform.position).normalized * meleeKnockback, ForceMode2D.Impulse);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        // existing gizmo code...
        // add melee sphere viz:
        Gizmos.color = Color.red;
        float s = Application.isPlaying ? FacingSign() : Mathf.Sign(transform.localScale.x == 0 ? 1 : transform.localScale.x);
        Vector2 pos = (Vector2)transform.position + new Vector2(meleeOffset.x * s, meleeOffset.y);
        Gizmos.DrawWireSphere(pos, meleeRadius);
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
        OnVanishStart(); // apply invis / iframes immediately even if event fails

        // Wait for OnVanishDone() event or timeout
        vanishDoneFlag = false;
        float t = 0f;
        while (useOnVanishDoneEvent && !vanishDoneFlag && t < vanishEventTimeout)
        {
            t += Time.deltaTime;
            yield return null;
        }

        // Compute reappear position (near ceiling, clamped)
        float ceilingY = ArenaCeilingY();
        float reappearY = ClampArenaY(ceilingY - vanishTeleportYMargin);
        float targetX = player ? ClampArenaX(GetPlayerTopCenter().x + playerTopAimXOffset)
                       : ClampArenaX(transform.position.x);

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

    IEnumerator SkyAttackRoutine_ReenterFromAir()
    {
        // Tell Animator to go to sky state
        SafeSetTrigger(anim, P_TRIG_SKY);

        // Wait briefly for transition to consume trigger; force if blocked
        yield return WaitToEnterState(skyStateName, 0.6f, 0);
        if (!IsInState(skyStateName, 0))
        {
            anim.ResetTrigger(P_TRIG_SKY);
            anim.Play(skyStateName, 0, 0f); // force enter
            yield return null;
        }

        // Keep invuln from vanish and ensure invuln during sky
        if (invulnerableDuringSky && !IsInvulnerable) BeginIFrames();

        // Reappear visually before hover/telegraph
        if (hideSpriteDuringVanish && spriteForHide) spriteForHide.enabled = true;

        // --- HOVER & TRACK PLAYER.X (optional) ---
        float lockedTargetX = rb.position.x;
        float hoverTimer = hoverTrackTime;
        float hoverY = rb.position.y;

        while (!IsDead && trackDuringHover && hoverTimer > 0f)
        {
            hoverTimer -= Time.deltaTime;

            float targetX = rb.position.x;
           if (player)
            {
                var top = GetPlayerTopCenter();
                targetX = ClampArenaX(top.x + playerTopAimXOffset);
            }

            float nextX = Mathf.MoveTowards(rb.position.x, targetX, hoverMaxXSpeed * Time.deltaTime);
            if (Mathf.Abs(nextX - targetX) < hoverSnapEpsilon) nextX = targetX;

            rb.MovePosition(new Vector2(ClampArenaX(nextX), ClampArenaY(hoverY)));
            yield return null;
        }

        // Final lock before the dive
        lockedTargetX = ClampArenaX(rb.position.x);

        // Telegraph pause
        if (telegraphDelay > 0f) yield return new WaitForSeconds(telegraphDelay);

        // --- DIVE STRAIGHT DOWN (X locked) ---
        float diveTimeout = 2.0f;
        float arenaFloorY = ArenaFloorY();

        var oldConstraints = rb.constraints;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionX;

        while (!IsDead && diveTimeout > 0f)
        {
            diveTimeout -= Time.deltaTime;

            Vector2 next = new Vector2(
                lockedTargetX,
                Mathf.Max(transform.position.y - skyDiveSpeed * Time.deltaTime, arenaFloorY)
            );
            rb.MovePosition(new Vector2(ClampArenaX(next.x), next.y));

            // Ground check
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

        rb.constraints = oldConstraints;

        // End invulnerability windows
        if (invulnerableDuringSky) EndIFrames();
        if (invulnerableDuringVanish) EndIFrames();

        if (bodyCollider) bodyCollider.enabled = true;

        SafeSetTrigger(anim, P_TRIG_SKY_DONE);
        yield return new WaitForSeconds(0.25f);

        DisableAllHitboxes();
    }
    bool TryGetPlayerBounds(out Bounds b)
    {
        b = default;
        if (!player) return false;//check for player

        //aim at player collider
        var col = player.GetComponentInChildren<Collider2D>();
        if (col && col.enabled) { b = col.bounds; return true; }

        // Fallback to SpriteRenderer
        var sr = player.GetComponentInChildren<SpriteRenderer>();
        if (sr && sr.enabled && sr.sprite) { b = sr.bounds; return true; }

        // Last resort: approximate 1×1 around transform
        b = new Bounds(player.position, Vector3.one);
        return true;

    }

    Vector2 GetPlayerTopCenter()
    {
        if (TryGetPlayerBounds(out var bb))
            return new Vector2(bb.center.x, bb.max.y);
        return (Vector2)player.position;
    }
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

        StartCoroutine(DespawnAfter(2.5f));
    }

    IEnumerator DespawnAfter(float s)
    {
        yield return new WaitForSeconds(s);
        Destroy(gameObject);
        DieInRoom();
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

    // Animator helpers
    bool IsInState(string stateName, int layer = 0)
    {
        if (!anim || string.IsNullOrEmpty(stateName)) return false;
        var st = anim.GetCurrentAnimatorStateInfo(layer);
        return st.IsName(stateName);
    }
    IEnumerator WaitToEnterState(string stateName, float timeout, int layer)
    {
        float t = 0f;
        while (t < timeout)
        {
            if (IsInState(stateName, layer)) yield break;
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    // -------------------- UTIL: ARENA & CLAMP --------------------
    bool TryGetArena(out Bounds b)
    {
        b = default;
        if (!arenaCollider) return false;
        if (!arenaCollider.enabled) return false;
        var bb = arenaCollider.bounds;
        if (bb.size.x < 0.01f || bb.size.y < 0.01f) return false; // avoid zero/tiny
        b = bb; return true;
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
  
}
