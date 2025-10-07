using UnityEngine;
using System.Collections;
using System;


[RequireComponent(typeof(Rigidbody2D))]
public class TestPlayerController : MonoBehaviour, IDamageable
{ 
    [Header("Move")]
    public float moveSpeed = 5f;
    public bool IsStunned { get; private set; }
    float stunUntil;
    [Header("Health")]
    public int maxHP = 100;
    [SerializeField] private int currentHP; // shows in Inspector; private is fine
    public int CurrentHP => currentHP;

    [Header("Dash")]
    [SerializeField] float dashSpeed = 10f;
    [SerializeField] float dashDuration = 0.15f;
    [SerializeField] float dashCooldown = 0.6f;

    Rigidbody2D rb;
    bool isDashing;
    float lastDashTime = -999f;

    Vector2 moveDirection;
    Vector2 mouseWorld;
    public event Action<int, int> HealthChanged;//current, max
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        currentHP = maxHP;
        HealthChanged?.Invoke(currentHP, maxHP);
    }

    public bool ApplyStun(float seconds)
    {
        if (seconds <= 0f) return false;
        IsStunned = true;
        stunUntil = Time.time + seconds;
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
        if (IsStunned && Time.time >= stunUntil)
        {
            IsStunned = false;
        }
    }

    void FixedUpdate()
    {
        if (!isDashing)
        {
            // Top-down movement
            rb.linearVelocity = moveDirection * moveSpeed;

            // Rotate to face the mouse
            Vector2 aimDir = mouseWorld - rb.position;
            float aimAngle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg - 90f;
            rb.rotation = aimAngle;
        }
        // else: Dash coroutine is controlling rb.velocity
    }

    IEnumerator Dash()
    {
        isDashing = true;
        lastDashTime = Time.time;

        // Dash velocity in current move direction (fallback to facing if idle)
        Vector2 dashDir = moveDirection.sqrMagnitude > 0.001f ? moveDirection : (mouseWorld - rb.position).normalized;
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
            // TODO: death behavior
        }
    }

    // Optional healing
    public void Heal(int amount)
    {
        int prev = currentHP;
        currentHP = Mathf.Min(maxHP, currentHP + Mathf.Abs(amount));
        if (currentHP != prev)
            HealthChanged?.Invoke(currentHP, maxHP);
    }
}


