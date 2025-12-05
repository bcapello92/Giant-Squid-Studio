using UnityEngine;

public class WaterBullet : MonoBehaviour
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

        // 1) Check for player first (so we can use TakeBulletDamage)
        var player = other.GetComponentInParent<TestPlayerController>()
                  ?? other.GetComponentInChildren<TestPlayerController>();

        if (player != null)
        {
            // This will be blocked if the player is holding block,
            // and still respect dash i-frames.
            player.GetComponent<TestPlayerController>().ApplyStun(1);
            player.GetComponent<Rigidbody2D>().AddForce(rb.linearVelocity.normalized * 50, ForceMode2D.Impulse);
            Destroy(gameObject);
            return;
        }

        // 2) Fallback: anything else that implements IDamageable
        var damageable = other.GetComponentInParent<IDamageable>()
                      ?? other.GetComponentInChildren<IDamageable>();

        if (damageable != null)
        {
            // Non-player targets just use generic damage
            other.GetComponent<Rigidbody2D>().AddForce((other.transform.position - transform.position).normalized, ForceMode2D.Impulse);
        }

        // Destroy bullet on ANY valid hit
        Destroy(gameObject);
    }

    // Optional: destroy when it leaves camera view (backup)
    void OnBecameInvisible()
    {
        Destroy(gameObject);
    }
}
