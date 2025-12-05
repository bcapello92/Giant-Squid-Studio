using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public class Monster6Base : RoomEnemy, IDamageable
{
    [Header("Health")]
    public int maxHP = 60;
    [SerializeField] int currentHP;

    [Header("Spawn")]
    public GameObject MonsterSix;

    [Header("Death")]
    public float deathDespawnDelay = 1.5f;
    public bool disablePhysicsOnDeath = true;
    public Behaviour[] componentsToDisableOnDeath;

    [Header("Audio")]
    public AudioSource deathNoise;

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
    RoomManager roomManager;
    LineRenderer lr;
    private GameObject SpawnedMonster;
    bool isDead;
    public event Action<int, int> OnHealthChanged; // (current, max)
    public event Action<Monster6Base> OnDied;
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        cols = GetComponentsInChildren<Collider2D>(true);
        lr = GetComponent<LineRenderer>();

        currentHP = Mathf.Max(1, maxHP);
        OnHealthChanged?.Invoke(currentHP, maxHP);
    }

    void OnEnable()
    {
        lr.positionCount = 2;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.widthMultiplier = 0.2f;
        SpawnedMonster = Instantiate(MonsterSix, gameObject.transform.position, Quaternion.identity);
    }

    void FixedUpdate()
    {
        if (!isDead)
        {
            lr.SetPosition(0, transform.position);
            lr.SetPosition(1, SpawnedMonster.transform.position);
        }
    }


    // ===== Health / Death =====
    public void TakeDamage(int amount)
    {
        if (isDead) return;

        currentHP = Mathf.Max(0, currentHP - Mathf.Abs(amount));
        OnHealthChanged?.Invoke(currentHP, maxHP);
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
        SpawnedMonster.GetComponent<Monster6Controller>().Die();
        lr.widthMultiplier = 0f;

        deathNoise.Play();

        // notify room ONCE
        DieInRoom();


        OnDied?.Invoke(this);
        
        if (rb)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            if (disablePhysicsOnDeath) rb.simulated = false;
        }

        if (componentsToDisableOnDeath != null)
            foreach (var b in componentsToDisableOnDeath) if (b) b.enabled = false;

        if (cols != null)
            foreach (var c in cols) if (c) c.enabled = false;

        gameObject.transform.localScale = new Vector3(0.5f, 0.5f, 1);
        if (anim) anim.SetTrigger(DeathTrig);
        StartCoroutine(DespawnAfterDelay());
    }

    public void OnDeathAnimationComplete() { Destroy(gameObject); }

    void Start()
    {
        // Register with healthbar manager if available
        if (EnemyHealthBarManager.Instance != null)
        {
            //EnemyHealthBarManager.Instance.RegisterBlob(this);
        }

        // also push initial health just in case
        OnHealthChanged?.Invoke(currentHP, maxHP);
    }

    IEnumerator DespawnAfterDelay()
    {
        yield return new WaitForSeconds(deathDespawnDelay);
        Destroy(gameObject);
    }
}
