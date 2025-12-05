using UnityEngine;

[RequireComponent(typeof(SpriteRenderer), typeof(Collider2D))]
public class Pit : MonoBehaviour
{
    [Header("Sprites")]
    public Sprite openSprite;    // Default open sprite
    public Sprite closedSprite;  // Closed/trapped sprite

    [Header("Trap Settings")]
    public float holdTime = 2f;  // How long the player is held

    private SpriteRenderer sr;
    private bool triggered = false;
    private TestPlayerController trappedPlayer = null;
    private float trapTimer = 0f;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (openSprite)
            sr.sprite = openSprite;
    }

    void Update()
    {
        if (trappedPlayer != null)
        {
            trapTimer += Time.deltaTime;

            // Keep player locked at trap center
            trappedPlayer.transform.position = transform.position;

            // Stop all movement
            Rigidbody2D rb = trappedPlayer.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            // Prevent input/movement
            trappedPlayer.SetExternalSpeedMultiplier(0f);

            // Release player after holdTime
            if (trapTimer >= holdTime)
            {
                trappedPlayer.SetExternalSpeedMultiplier(1f);
                trappedPlayer = null;

                // Optional: reset sprite (can comment out if not needed)
                // if (openSprite) sr.sprite = openSprite;

                // Destroy trap after use
                Destroy(gameObject);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered) return;

        // Use CompareTag so we only trigger on the Player prefab instance
        if (!other.CompareTag("Player")) return;

        triggered = true;

        // Switch to closed sprite
        if (closedSprite)
            sr.sprite = closedSprite;

        // Get the TestPlayerController from parent if collider is child
        trappedPlayer = other.GetComponentInParent<TestPlayerController>();
        trapTimer = 0f;

        // Apply stun effect for visual feedback / internal logic
        if (trappedPlayer != null)
            trappedPlayer.ApplyStun(holdTime);
    }
}
