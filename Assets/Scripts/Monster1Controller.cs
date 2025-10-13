using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BlobMonsterController : MonoBehaviour, IDamageable
{
    [Header("Health")]
    public int maxHP = 50;
    [SerializeField] int currentHP;

    [Header("Death")]
    public float deathDespawnDelay = 1.5f;     // fallback if you don't use an animation event
    public bool disablePhysicsOnDeath = true;  // turn off Rigidbody2D/colliders so it stops “bugging out”
    public Behaviour[] componentsToDisableOnDeath; // e.g., your AI/movement scripts

    // Animator hashes (match your Animator)
    static readonly int HitTrig = Animator.StringToHash("Hit");
    static readonly int DeathTrig = Animator.StringToHash("Death");

    Rigidbody2D rb;
    Animator anim;
    Collider2D[] cols;

    bool isDead;
    bool attackWindowOpen;             // if you were using an attack window
    readonly HashSet<Collider2D> hitThisSwing = new(); // if you were tracking hits per swing

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        cols = GetComponentsInChildren<Collider2D>(includeInactive: true);

        currentHP = Mathf.Max(1, maxHP);
    }

    // ========= IDamageable =========
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

    // ========= Death flow =========
    void Die()
    {
        if (isDead) return;
        isDead = true;

        // Stop any attack windows / per-swing tracking (if you used them)
        attackWindowOpen = false;
        hitThisSwing.Clear();

        // Stop movement/AI immediately
        if (rb)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            if (disablePhysicsOnDeath) rb.simulated = false; // remove from physics sim so it doesn't jitter
        }

        // Disable any behaviour scripts that might keep updating (AI, chaser, hazard emitters, etc.)
        if (componentsToDisableOnDeath != null)
        {
            for (int i = 0; i < componentsToDisableOnDeath.Length; i++)
            {
                if (componentsToDisableOnDeath[i]) componentsToDisableOnDeath[i].enabled = false;
            }
        }

        // Optionally disable colliders so the corpse doesn't block or receive hits
        if (cols != null)
        {
            foreach (var c in cols)
            {
                if (!c) continue;
                c.enabled = false;
            }
        }

        // Trigger death animation
        if (anim) anim.SetTrigger(DeathTrig);

        // EITHER: add an Animation Event on the last frame of the Death clip that calls OnDeathAnimationComplete()
        // OR: fall back to a timed despawn:
        StartCoroutine(DespawnAfterDelay());
    }

    IEnumerator DespawnAfterDelay()
    {
        yield return new WaitForSeconds(deathDespawnDelay);
        Destroy(gameObject);
    }

    // Call this from the Death animation via Animation Event at the end
    public void OnDeathAnimationComplete()
    {
        Destroy(gameObject);
    }

    // If you had attack events before, keep them safe-guarded:
    public void StartAttackWindow() { if (!isDead) { attackWindowOpen = true; hitThisSwing.Clear(); } }
    public void EndAttackWindow() { attackWindowOpen = false; hitThisSwing.Clear(); }
    public void DealDamageEvent() { if (!isDead) { /* do your overlap damage here */ } }
}
