using UnityEngine;
using System;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[DisallowMultipleComponent]
public class TestPlayerController : MonoBehaviour, IDamageable
{
    [Header("Move")]
    public float moveSpeed = 5f;

    [Header("Dash")]
    public float dashSpeed = 10f;
    public float dashDuration = 0.15f;
    public float dashCooldown = 0.6f;
   
    [Header("Dash i-frames")]
    [Tooltip("Layers the player should NOT collide with while dashing (e.g., Enemy, EnemyAttack).")]
    public string[] dashIgnoreLayers = new[] { "Enemy", "EnemyAttack" };

    [Tooltip("Also ignore damage while dashing.")]
    public bool dashGrantsInvulnerability = true;

    public bool IsInvulnerable { get; private set; }

    int playerLayer;
    int[] ignoreLayerIds;
    int iframeDepth = 0; // protects against double-enable/disable

    [Header("Health")]
    public int maxHP = 100;
    [SerializeField] private int currentHP;
    public int CurrentHP => currentHP;
    public event Action<int, int> HealthChanged; // (current, max)

    [Header("Stun")]
    public float stunDamp = 20f; // how quickly velocity damps while stunned
    public bool IsStunned { get; private set; }
    float stunUntil;

    [Header("Aiming")]
    public Camera aimCamera;                 // assign MainCamera or leave empty to auto-find
    public bool rotateBodyToAim = false;     // true = rotate the Rigidbody2D toward mouse

    //aim direction
    public Vector2 AimDir { get; private set; } = Vector2.right;
    [SerializeField] bool faceByAimAlways = true;
    [Header("Animator (optional)")]
    public Animator anim;                    
    public string moveXParam = "MoveX";
    public string moveYParam = "MoveY";
    public string lastXParam = "LastX";
    public string lastYParam = "LastY";
    public string speedParam = "Speed";
    public SpriteRenderer bodySR;//for swapping side to side sprite
    public string DamageTrigParam = "TakeDamage";

    [Header("Audio")]
    public AudioSource hurtAudio;
    public AudioSource dashAudio;

    Rigidbody2D rb;
    Vector2 moveInput;
    Vector2 lastNonZeroDir = Vector2.right;  // used for facing when idle

    bool isDashing;
    float lastDashTime = -999f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (!aimCamera) aimCamera = Camera.main;
        if (!anim) anim = GetComponentInChildren<Animator>();

        currentHP = maxHP;
        HealthChanged?.Invoke(currentHP, maxHP);

        playerLayer = gameObject.layer;
        ignoreLayerIds = new int[dashIgnoreLayers.Length];
        for (int i = 0; i < dashIgnoreLayers.Length; i++)
        {
            ignoreLayerIds[i] = LayerMask.NameToLayer(dashIgnoreLayers[i]);
            if (ignoreLayerIds[i] < 0)
                Debug.LogWarning($"[Player] Layer '{dashIgnoreLayers[i]}' not found. Check Project Settings > Tags & Layers.");
        }

    }

    void OnEnable()
    {
        // Resend current health for late UI subscribers
        HealthChanged?.Invoke(currentHP, maxHP);
    }

    void Update()
    {
        // ----- Clear stun when time passes -----
        if (IsStunned && Time.time >= stunUntil)
            IsStunned = false;

        // ----- Input (block while stunned) -----
        if (!IsStunned)
        {
            float mx = Input.GetAxisRaw("Horizontal");
            float my = Input.GetAxisRaw("Vertical");
            moveInput = new Vector2(mx, my).normalized;

            if (moveInput.sqrMagnitude > 0.0001f)
                lastNonZeroDir = moveInput;

            // Dash
            if (!isDashing && Time.time >= lastDashTime + dashCooldown && Input.GetKeyDown(KeyCode.LeftShift))
                StartCoroutine(Dash());
        }
        else
        {
            moveInput = Vector2.zero;
        }

        // ----- Mouse aim -----
        UpdateAim();

        // Optional body rotation to aim
        if (rotateBodyToAim)
        {
            float z = Mathf.Atan2(AimDir.y, AimDir.x) * Mathf.Rad2Deg - 90f;
            rb.rotation = z;
        }

        // ----- Animator parameters (optional) -----
        if (anim)
        {
            float speedMag = rb ? rb.linearVelocity.magnitude : 0f;
            if (!string.IsNullOrEmpty(speedParam))
                SafeSetFloat(anim, speedParam, speedMag);

            // Movement vector you actually apply
            SafeSetFloat(anim, moveXParam, moveInput.x);
            SafeSetFloat(anim, moveYParam, moveInput.y);

            // Choose which direction to FACE
            Vector2 faceDir;
            if (faceByAimAlways)
            {
                //faces the mouse direction
                faceDir = AimDir;
            }
            else
            {
                // Face movement while moving; face mouse when idle
                faceDir = (moveInput.sqrMagnitude > 0.0001f) ? moveInput : AimDir;
            }

            SafeSetFloat(anim, lastXParam, faceDir.x);
            SafeSetFloat(anim, lastYParam, faceDir.y);
        }
        if (bodySR)
        {
            // Face vector you already computed for the Animator (mouse aim)
            Vector2 face = new Vector2(
                anim ? anim.GetFloat(lastXParam) : AimDir.x,
                anim ? anim.GetFloat(lastYParam) : AimDir.y
            );

            bool horizontal = Mathf.Abs(face.x) >= Mathf.Abs(face.y);
            if (horizontal)
                bodySR.flipX = (face.x < 0f);  // reuse RIGHT-facing clip; flip for left
            else
                bodySR.flipX = false;          // for up/down, don�t flip
        }
    }

    void FixedUpdate()
    {
        if (IsStunned)
        {
            // Strong damping while stunned
            rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, stunDamp * Time.fixedDeltaTime);
            return;
        }

        if (!isDashing)
        {
            rb.linearVelocity = moveInput * moveSpeed;
        }
        // else: Dash coroutine controls velocity
    }

    IEnumerator Dash()
    {
        isDashing = true;
        lastDashTime = Time.time;

        Vector2 dir = (moveInput.sqrMagnitude > 0.0001f) ? moveInput : AimDir;
        if (dir.sqrMagnitude < 0.0001f) dir = lastNonZeroDir;

        rb.linearVelocity = dir.normalized * dashSpeed;

        dashAudio.Play();

        BeginDashIFrames();

        float t = 0f;
        while (t < dashDuration)
        {
            t += Time.deltaTime;
            yield return null;
        }

        isDashing = false;
        EndDashIFrames();
    }
    // --- Helpers ---
    void BeginDashIFrames()
    {
        iframeDepth++;
        if (iframeDepth != 1) return;

        if (dashGrantsInvulnerability) IsInvulnerable = true;

        // Ignore collisions vs. enemy/boss layers ONLY (walls still collide)
        for (int i = 0; i < ignoreLayerIds.Length; i++)
        {
            int other = ignoreLayerIds[i];
            if (other >= 0)
                Physics2D.IgnoreLayerCollision(playerLayer, other, true);
        }
    }

    void EndDashIFrames()
    {
        if (iframeDepth <= 0) return;
        iframeDepth--;
        if (iframeDepth != 0) return;

        for (int i = 0; i < ignoreLayerIds.Length; i++)
        {
            int other = ignoreLayerIds[i];
            if (other >= 0)
                Physics2D.IgnoreLayerCollision(playerLayer, other, false);
        }
        IsInvulnerable = false;
    }
    void UpdateAim()
    {
        if (!aimCamera) { AimDir = lastNonZeroDir; return; }

        Vector3 mouseWorld = aimCamera.ScreenToWorldPoint(Input.mousePosition);
        Vector2 v = (Vector2)(mouseWorld - transform.position);
        if (v.sqrMagnitude > 0.000001f)
            AimDir = v.normalized;
        else
            AimDir = lastNonZeroDir;
    }

    // -------- Health / Damage --------
    public void TakeDamage(int amount)
    {
        if (IsInvulnerable) return; // i-frames: ignore damage

        int prev = currentHP;
        currentHP = Mathf.Max(0, currentHP - Mathf.Abs(amount));
        if (currentHP != prev)
        {
            HealthChanged?.Invoke(currentHP, maxHP);
            if (amount > 0 && anim && !string.IsNullOrEmpty(DamageTrigParam))
                anim.SetTrigger(DamageTrigParam);
        }

        if (currentHP == 0)
        {
            RunManager.I?.OnPlayerDied();
        }

        hurtAudio.Play();
    }


    public void Heal(int amount)
    {
        int prev = currentHP;
        currentHP = Mathf.Min(maxHP, currentHP + Mathf.Abs(amount));
        if (currentHP != prev)
            HealthChanged?.Invoke(currentHP, maxHP);
    }

    // -------- Stun --------
    public void ApplyStun(float seconds)
    {
        if (seconds <= 0f) return;

        IsStunned = true;
        stunUntil = Mathf.Max(stunUntil, Time.time + seconds);

        if (isDashing)
        {
            StopAllCoroutines();
            isDashing = false;
        }

        rb.linearVelocity = Vector2.zero;
    }

    // ----- Animator safe helpers -----
    bool HasParam(Animator a, string name, AnimatorControllerParameterType type)
    {
        if (!a || string.IsNullOrEmpty(name)) return false;
        var ps = a.parameters;
        for (int i = 0; i < ps.Length; i++)
            if (ps[i].name == name && ps[i].type == type) return true;
        return false;
    }

    void SafeSetFloat(Animator a, string name, float v)
    {
        if (HasParam(a, name, AnimatorControllerParameterType.Float))
            a.SetFloat(name, v);
    }
}
