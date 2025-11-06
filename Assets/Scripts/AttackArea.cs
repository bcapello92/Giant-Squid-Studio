using System.Collections.Generic;
using UnityEngine;

public class AttackArea : MonoBehaviour
{
    [Header("Damage")]
    public int damage = 10;
    public float knockback = 0f;              // optional
    public LayerMask targetLayers;            // set to Enemy (and Player if needed)

    [Header("Behavior")]
    public bool oneHitPerSwing = true;        // prevents double-hitting the same collider
    public bool continuousOnStay = false;     // set true if you want DOT while overlapping
    public float stayTickInterval = 0.2f;     // used only if continuousOnStay

    // (Optional) set by the wielder so we don't hit ourselves/friends
    [Tooltip("GameObject that owns this hitbox (e.g., the attacker root).")]
    public GameObject owner;

    // runtime
    readonly HashSet<Collider2D> hitThisSwing = new HashSet<Collider2D>();
    float lastStayTickTime;

    /// Call this from your attack animation at the start of the active frames
    public void StartSwing()
    {
        hitThisSwing.Clear();
        lastStayTickTime = -999f;
        enabled = true; // if you disable this component between swings
    }

    /// Call this at the end of the active frames
    public void EndSwing()
    {
        hitThisSwing.Clear();
        enabled = false; // optional
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryHit(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (!continuousOnStay) return;
        if (Time.time < lastStayTickTime + stayTickInterval) return;
        if (TryHit(other)) lastStayTickTime = Time.time;
    }

    bool TryHit(Collider2D other)
    {
        if (!other || other.isTrigger) return false;

        // Layer filter (if set)
        if (targetLayers.value != 0)
        {
            if (((1 << other.gameObject.layer) & targetLayers.value) == 0)
                return false;
        }

        // Ignore self/owner
        if (owner && other.transform.IsChildOf(owner.transform))
            return false;

        // One-hit-per-swing guard
        if (oneHitPerSwing && hitThisSwing.Contains(other))
            return false;

        // Find anything damageable on that hierarchy
        var dmg = other.GetComponentInParent<IDamageable>() ?? other.GetComponentInChildren<IDamageable>();
        if (dmg == null) return false;

        // Apply damage
        dmg.TakeDamage(damage);
        Debug.Log("take " + damage + " damage");

        // Optional knockback
        if (knockback > 0f && other.attachedRigidbody != null)
        {
            Vector2 dir = ((Vector2)other.transform.position - (Vector2)transform.position).normalized;
            other.attachedRigidbody.AddForce(dir * knockback, ForceMode2D.Impulse);
        }

        if (oneHitPerSwing) hitThisSwing.Add(other);
        // Debug.Log($"[AttackArea] Hit {other.name} for {damage}");
        return true;
    }
}
