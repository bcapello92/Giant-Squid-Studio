using System.Collections;
using UnityEngine;

/// Explosive crate that flashes when armed, then switches to a separate explosion visual which is scaled to match gameplay radius.
/// Flashing visuals are never scaled.
[RequireComponent(typeof(Collider2D))]
public class ExplosiveCrate2D : MonoBehaviour, IDamageable 
{
    [Header("Explosion (gameplay)")]
    public float explosionRadius = 2.5f;   // damage radius in world units
    public int damage = 25;
    public float stunSeconds = 1.25f;
    public float knockback = 6f;
    public LayerMask targetLayers;

    [Header("Timing")]
    public bool armOnStart = false;
    public float armDuration = 3f;         // fuse before boom

    [Header("Flash visuals (Animator A)")]
    [SerializeField] Transform flashRoot;              // child with flashing sprites
    [SerializeField] Animator flashAnimator;           // Animator with Idle/Armed states
    public string armedBoolParam = "Armed";            // Bool on Animator A

    [Header("Explosion visuals (Animator B)")]
    [SerializeField] Transform explosionRoot;          // child with explosion sprites
    [SerializeField] Animator explosionAnimator;       // Animator with one-shot Explosion state
    public string explodeTriggerParam = "Explode";     // Trigger on Animator B
    [Tooltip("Visual radius (world units) your Explosion animation represents at scale=1.")]
    public float authoredExplosionRadius = 1f;

    [Header("Debug")]
    public bool debugLogs = false;

    // runtime
    bool armed, exploded;
    int armedHash, explodeHash;

    void OnValidate()
    {
        if (!flashAnimator && flashRoot) flashAnimator = flashRoot.GetComponent<Animator>();
        if (!explosionAnimator && explosionRoot) explosionAnimator = explosionRoot.GetComponent<Animator>();
    }

    void Awake()
    {
        // Reasonable auto-finds if fields unassigned
        if (!flashAnimator) flashAnimator = GetComponentInChildren<Animator>(true);
        if (!flashRoot && flashAnimator) flashRoot = flashAnimator.transform;

        // Try to find a different child animator for explosion if both point to same
        if (!explosionAnimator || explosionAnimator == flashAnimator)
        {
            foreach (var a in GetComponentsInChildren<Animator>(true))
                if (a != flashAnimator) { explosionAnimator = a; break; }
            if (!explosionRoot && explosionAnimator) explosionRoot = explosionAnimator.transform;
        }

        armedHash = Animator.StringToHash(armedBoolParam);
        explodeHash = Animator.StringToHash(explodeTriggerParam);

        if (targetLayers.value == 0) targetLayers = ~0; // Everything for easy testing

        // Ensure states: Flash visible, Explosion hidden at start
        if (explosionRoot) explosionRoot.gameObject.SetActive(false);
        if (flashRoot) flashRoot.gameObject.SetActive(true);
    }

    void Start()
    {
        if (armOnStart) ArmAndExplodeAfter(armDuration);
    }

    public void ArmAndExplodeAfter(float delay)
    {
        if (armed || exploded) return;
        StartCoroutine(ArmRoutine(delay));
    }

    IEnumerator ArmRoutine(float delay)
    {
        armed = true;

        // Start flashing (Animator A)
        if (flashAnimator)
        {
            flashAnimator.SetBool(armedHash, true);
        }
        if (debugLogs) Debug.Log("[Crate] Armed → flashing (Animator A).");

        yield return new WaitForSeconds(delay);
        Explode();
    }

    public void Explode()
    {
        if (exploded) return;
        exploded = true;

        if (debugLogs) Debug.Log("[Crate] BOOM!");

        // 1) Stop flashing immediately
        if (flashAnimator) flashAnimator.SetBool(armedHash, false);

        // 2) Activate explosion visuals and scale ONLY that root
        if (explosionRoot)
        {
            explosionRoot.gameObject.SetActive(true);
            ApplyExplosionVisualScale(explosionRoot);
        }

        // 3) Optionally hide the flash visuals now (prevents seeing last flash frame)
        if (flashRoot) flashRoot.gameObject.SetActive(false);

        // 4) Trigger explosion animation (Animator B)
        if (explosionAnimator)
        {
            explosionAnimator.ResetTrigger(explodeHash);
            explosionAnimator.SetTrigger(explodeHash);
        }

        // 5) Gameplay effect instantly (or call from an Animation Event if you want precise timing)
        DoExplosionHit();

        // 6) Disable collider so it no longer blocks anything after detonation
        var col = GetComponent<Collider2D>(); if (col) col.enabled = false;

        // 7) Destroy when explosion anim finishes:
        //    Add an Animation Event at the END of the Explosion clip (Animator B) that calls DestroySelf().
        //    Or uncomment the timed fallback below (match to clip length):
        // Destroy(gameObject, 0.6f);
    }

    void ApplyExplosionVisualScale(Transform toScale)
    {
        if (!toScale) return;
        float baseR = Mathf.Max(0.0001f, authoredExplosionRadius);
        float scale = explosionRadius / baseR;
        toScale.localScale = new Vector3(scale, scale, 1f);
        if (debugLogs) Debug.Log($"[Crate] Explosion visual scaled x{scale:0.##} (authoredR={baseR}, targetR={explosionRadius}).");
    }

    void DoExplosionHit()
    {
        var hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius, targetLayers);
        if (debugLogs) Debug.Log($"[Crate] explode @ {transform.position}, r={explosionRadius}, hits={hits.Length}");

        foreach (var h in hits)
        {
            if (!h) continue;

            // Damage
            var dmg = h.GetComponentInParent<IDamageable>() ?? h.GetComponentInChildren<IDamageable>();
            if (dmg != null) dmg.TakeDamage(damage);

            // Stun (optional)
            var stun = h.GetComponentInParent<IStunnable>() ?? h.GetComponentInChildren<IStunnable>();
            if (stun != null) stun.ApplyStun(stunSeconds);

            // Knockback outward
            var rb = h.attachedRigidbody;
            if (rb)
            {
                Vector2 dir = ((Vector2)h.transform.position - (Vector2)transform.position).normalized;
                rb.AddForce(dir * knockback, ForceMode2D.Impulse);
            }
        }
    }
   public void TakeDamage(int damage)
    {
        if (exploded) return;
        if (armed) return; // already counting down

        if (debugLogs)
            Debug.Log($"[Crate] Took {damage} damage → arm fuse for {armDuration} seconds");

        ArmAndExplodeAfter(armDuration);
    }
    // Call this from an Animation Event on the last frame of the Explosion clip (Animator B)
    public void DestroySelf()
    {
        if (debugLogs) Debug.Log("[Crate] DestroySelf()");
        Destroy(gameObject);
    }
}
