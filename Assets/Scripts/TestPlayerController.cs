using UnityEngine;
using System.Collections;
using System;



[RequireComponent(typeof(Rigidbody2D))]
public class TestPlayerController : MonoBehaviour, IDamageable, IStunnable
{
    [Header("Move")]
    public float moveSpeed = 5f;

    [Header("Health")]
    public int maxHP = 100;
    [SerializeField] private int currentHP;
    public int CurrentHP => currentHP;

    [Header("Dash")]
    [SerializeField] float dashSpeed = 10f;
    [SerializeField] float dashDuration = 0.15f;
    [SerializeField] float dashCooldown = 0.6f;


    [Header("Attack")]
    [SerializeField] float attackDuration = 0.5f;
    [SerializeField] float attackCooldown = 1f;

    GameObject attackArea;

    [Header("Stun")]
    [SerializeField] float stunDamp = 20f;   // how fast we kill velocity while stunned

    public event Action<int, int> HealthChanged; // current, max
    public bool IsStunned { get; private set; }
    float stunUntil;


    Rigidbody2D rb;
    bool isDashing;
    bool isAttacking;
    float lastAttackTime = -999f;
    float lastDashTime = -999f;

    Vector2 moveDirection;
    Vector2 mouseWorld;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // Top-down defaults
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        currentHP = maxHP;

        // Initial fire (some UIs subscribe later; we re-fire in OnEnable)
        HealthChanged?.Invoke(currentHP, maxHP);
    }

    void OnEnable()
    {
        // Re-broadcast current health so late subscribers (UI) catch up
        HealthChanged?.Invoke(currentHP, maxHP);
        attackArea = transform.GetChild(0).gameObject;
    }

    public bool ApplyStun(float seconds)
    {
        if (seconds <= 0f) return false;

        IsStunned = true;
        stunUntil = Mathf.Max(stunUntil, Time.time + seconds);

        // Cancel dash immediately
        if (isDashing)
        {
            StopAllCoroutines();
            isDashing = false;
        }

        // Kill current motion
        rb.linearVelocity = Vector2.zero;
        return true;
    }

    void Update()
    {

        // --- Input ---
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");
        moveDirection = new Vector2(moveX, moveY).normalized;

        // Mouse aim (guard against null camera in editor)
        if (Camera.main != null)
            mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        // Dash trigger
        if (!isDashing && Time.time >= lastDashTime + dashCooldown && Input.GetKeyDown(KeyCode.LeftShift))
            StartCoroutine(Dash());
            

        // Clear stun when time passes

        if (IsStunned && Time.time >= stunUntil)
            IsStunned = false;

        // --- Input (blocked while stunned) ---
        if (!IsStunned)
        {
             moveX = Input.GetAxisRaw("Horizontal");
             moveY = Input.GetAxisRaw("Vertical");
            moveDirection = new Vector2(moveX, moveY).normalized;

            if (Camera.main != null)
                mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            // Dash trigger
            if (!isDashing && Time.time >= lastDashTime + dashCooldown && Input.GetKeyDown(KeyCode.LeftShift))
                StartCoroutine(Dash());
        }
        else
        {
            // While stunned, ignore input and slowly damp any drift
            moveDirection = Vector2.zero;
        }

        //if (!isAttacking && Time.time >= lastAttackTime + attackCooldown && Input.GetMouseButtonDown(0))
           // StartCoroutine(Attack());
 
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
            // Top-down movement
            rb.linearVelocity = moveDirection * moveSpeed;

            // Rotate to face the mouse
            Vector2 aimDir = mouseWorld - rb.position;
            float aimAngle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg - 90f;
            rb.rotation = aimAngle;
        }
        // else: Dash coroutine controls velocity
    }

    IEnumerator Dash()
    {
        isDashing = true;
        lastDashTime = Time.time;

        // Dash in move direction, or toward aim if idle
        Vector2 dashDir = moveDirection.sqrMagnitude > 0.001f
            ? moveDirection
            : (mouseWorld - rb.position).normalized;

        rb.linearVelocity = dashDir * dashSpeed;
        yield return new WaitForSeconds(dashDuration);

        isDashing = false;
    }

    // -------- Health / Damage --------
    public void TakeDamage(int amount)
    {
        int prev = currentHP;
        currentHP = Mathf.Max(0, currentHP - Mathf.Abs(amount));
        if (currentHP != prev)
            HealthChanged?.Invoke(currentHP, maxHP);

        if (currentHP == 0)
        {
            // TODO: death behavior (disable input, play anim, etc.)
        }
    }

    public void Heal(int amount)
    {
        int prev = currentHP;
        currentHP = Mathf.Min(maxHP, currentHP + Mathf.Abs(amount));
        if (currentHP != prev)
            HealthChanged?.Invoke(currentHP, maxHP);
    }


   /* IEnumerator Attack()
    {
        lastAttackTime = Time.time;
        isAttacking = true;
        attackArea.SetActive(isAttacking);

        yield return new WaitForSeconds(attackDuration);

        isAttacking = false;
        attackArea.SetActive(isAttacking);
    }
   */
}
