using UnityEngine;

public class Bullet2D : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 5f;
    public float lifeTime = 3f;

    [Header("Damage")]
    public int damage = 5;
    [Tooltip("Which layers can this bullet damage?")]
    public LayerMask hitLayers;

    [Header("Owner (optional)")]
    public GameObject owner;   // set by turret so we don't hit the shooter

    Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        // fire forward
        if (!rb) rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearVelocity = transform.right * speed; // assumes bullet faces +X

        // auto-destroy after some time so it doesn't live forever
        if (lifeTime > 0f)
            Destroy(gameObject, lifeTime);
    }

    // Use a trigger collider on the bullet for this to fire
    void OnTriggerEnter2D(Collider2D other)
    {
        // Ignore if hitting the owner
        if (owner != null && other.gameObject == owner)
            return;

        // Layer filter
        if (hitLayers.value != 0)
        {
            if ((hitLayers.value & (1 << other.gameObject.layer)) == 0)
                return;
        }

        // Try to find something damageable
        var damageable = other.GetComponentInParent<IDamageable>()
                      ?? other.GetComponentInChildren<IDamageable>();

        if (damageable != null)
        {
            damageable.TakeDamage(damage);
        }

        // Destroy bullet on ANY valid hit
        Destroy(gameObject);
    }

    // Optional: destroy when it leaves camera view (backup)
    void OnBecameInvisible()
    {
        // If it�s already scheduled for Destroy via lifeTime, this is safe anyway.
        Destroy(gameObject);
    }
}
