using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public class Monster6Controller : RoomEnemy
{
    [Header("Health")]
    public int maxHP = 50;
    [SerializeField] int currentHP;

    [Header("Targeting")]
    public string playerTag = "Player";
    public LayerMask playerLayers;        // set to Player layer in Inspector
    public float detectionRange = 20f;     // start chasing if within this
    public float stopDistance = 1.2f;     // stop this far from player

    [Header("Shock Attack")]
    public float shockRadius = 1.4f;
    public int shockDamage = 10;
    public float shockCooldown = 1.0f;
    public float shockKnockback = 4f;

    [Header("Movement")]
    public float moveSpeed = 2.2f;
    public float acceleration = 12f;

    [Header("Death")]
    public float deathDespawnDelay = 1.5f;
    public bool disablePhysicsOnDeath = true;
    public Behaviour[] componentsToDisableOnDeath;

    [Header("Audio")]
    public AudioSource slimeNoise;

    // Animator hashes (adjust to your controller)
    static readonly int MoveSpeedHash = Animator.StringToHash("Speed");
    static readonly int AttackTrig = Animator.StringToHash("Attack");
    static readonly int HitTrig = Animator.StringToHash("Hit");
    static readonly int DeathTrig = Animator.StringToHash("Death");

    [Header("Debug / Attack Wiring")]
    public bool callShockInCodeIfNoEvent = true;   // call ShockNow() directly when the trigger fires
    public bool debugLogs = false;

    void Log(string msg) { if (debugLogs) Debug.Log($"[Blob] {msg}", this); }

    Rigidbody2D rb;
    Animator anim;
    Collider2D[] cols;
    Transform player;
    RoomManager roomManager;
    bool isDead;
    float lastShockTime = -999f;
    public event Action<int, int> OnHealthChanged; // (current, max)
    public event Action<Monster6Controller> OnDied;
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        cols = GetComponentsInChildren<Collider2D>(true);

        currentHP = Mathf.Max(1, maxHP);
        OnHealthChanged?.Invoke(currentHP, maxHP);
        // Best-effort: if you forgot to set playerLayers, include "Player" layer
        if (playerLayers.value == 0)
        {
            int lyr = LayerMask.NameToLayer("Player");
            if (lyr >= 0) playerLayers |= (1 << lyr);
        }
    }

    void OnEnable()
    {
        // Acquire target by tag if not assigned
        if (!player)
        {
            var go = GameObject.FindGameObjectWithTag(playerTag);
            if (go) player = go.transform;
        }

        // Debug sanity
        if (!player)
            Debug.LogWarning("[Blob] No player found with tag 'Player'. Assign tag or set player manually.");
    }

    void FixedUpdate()
    {
        if (isDead || !player) return;

        Vector2 toPlayer = (Vector2)(player.position - transform.position);
        float dist = toPlayer.magnitude;

        // --- Movement / Chase ---
        Vector2 desiredVel = Vector2.zero;
        if (dist <= detectionRange && dist > stopDistance)
        {
            desiredVel = toPlayer.normalized * moveSpeed;
        }

        Vector2 vel = rb.linearVelocity; 
        Vector2 step = Vector2.ClampMagnitude(desiredVel - vel, acceleration * Time.fixedDeltaTime);
        rb.linearVelocity = vel + step;

        if (anim) anim.SetFloat(MoveSpeedHash, rb.linearVelocity.magnitude);

        // --- Attack gating ---
        bool inRange = dist <= (stopDistance + 0.1f);
        bool offCooldown = Time.time >= lastShockTime + shockCooldown;

        if (inRange && offCooldown)
        {
            lastShockTime = Time.time;
            rb.linearVelocity = Vector2.zero;    // stop to actually attack

            Log($"Attack trigger: dist={dist:F2}, stop={stopDistance}");
            if (anim)
            {
                anim.SetTrigger(AttackTrig);   // Animator must have a Trigger named "Attack"
            }

            // If your Attack clip doesn't have an Animation Event yet, do it now:
            if (callShockInCodeIfNoEvent)
            {
                // small windup so it isn't instant; tune or remove
                StartCoroutine(_ShockAfter(0.05f));
            }
        }
    }

    IEnumerator _ShockAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        ShockNow();
    }


    public void ShockNow()
    {
        if (isDead) return;

        Vector2 center = transform.position;
        var hits = Physics2D.OverlapCircleAll(center, shockRadius, playerLayers);

        // Debug prints to confirm registration
        // Debug.Log($"[Blob] Shock overlap count: {hits.Length}");

        foreach (var h in hits)
        {
            if (!h) continue;

            var dmg = h.GetComponentInParent<IDamageable>() ?? h.GetComponentInChildren<IDamageable>();
            if (dmg != null)
            {
                dmg.TakeDamage(shockDamage);
                slimeNoise.Play();

                var prb = h.attachedRigidbody;
                if (prb) prb.AddForce((h.transform.position - transform.position).normalized * shockKnockback, ForceMode2D.Impulse);
            }
        }
    }

    public void Die()
    {
        if (isDead) return;
        isDead = true;
        Destroy(gameObject);
    }
}
