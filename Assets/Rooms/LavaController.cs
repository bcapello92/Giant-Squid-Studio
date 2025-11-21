using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Collider2D))]
public class LavaHazard2D : MonoBehaviour
{
    [Header("Damage Settings")]
    public int damagePerTick = 5;
    public float tickInterval = 0.5f;
    public float verticalPushPerTick = 0.0f;
    public LayerMask targetLayers;
    public bool logHits = false;

    // Tracks all colliders currently inside the lava and their running coroutines
    private readonly Dictionary<Collider2D, Coroutine> ticking = new();

    void Awake()
    {
        // Ensure collider is trigger
        var col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
            col.isTrigger = true;

        if (targetLayers.value == 0)
        {
            // Default: everything
            targetLayers = ~0;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & targetLayers) == 0)
            return;

        // Start a tick loop only if not already running
        if (!ticking.ContainsKey(other) && damagePerTick > 0)
        {
            Coroutine co = StartCoroutine(DamageTickLoop(other));
            if (co != null)
                ticking.Add(other, co);

            if (logHits)
                Debug.Log($"[Lava] Start damage on {other.name}");
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // Safely stop and remove coroutine if it exists
        if (ticking.Remove(other, out var co))
        {
            if (co != null)
                StopCoroutine(co);

            if (logHits)
                Debug.Log($"[Lava] Stop damage on {other.name}");
        }
    }

    private IEnumerator DamageTickLoop(Collider2D target)
    {
        var rb = target.attachedRigidbody;

        while (true)
        {
            if (target == null)
                break;

            // If no longer overlapping, end loop
            if (!IsStillOverlapping(target))
                break;

            var dmg = GetDamageable(target);
            if (dmg != null)
            {
                dmg.TakeDamage(damagePerTick);
                if (logHits)
                    Debug.Log($"[Lava] Tick hit {target.name} for {damagePerTick}");
            }

            if (verticalPushPerTick != 0f && rb != null)
                rb.AddForce(Vector2.up * verticalPushPerTick, ForceMode2D.Impulse);

            yield return new WaitForSeconds(tickInterval);
        }

        // Clean up after coroutine exits
        if (ticking.ContainsKey(target))
            ticking.Remove(target);
    }

    private bool IsStillOverlapping(Collider2D target)
    {
        if (target == null)
            return false;

        var col = GetComponent<Collider2D>();
        if (col == null)
            return false;

        return col.IsTouching(target);
    }

    private IDamageable GetDamageable(Collider2D target)
    {
        return target.GetComponentInParent<IDamageable>() ??
               target.GetComponentInChildren<IDamageable>();
    }

    void OnDisable()
    {
        // Stop all running coroutines safely
        foreach (var kv in ticking)
        {
            if (kv.Value != null)
                StopCoroutine(kv.Value);
        }
        ticking.Clear();
    }

    void OnDestroy()
    {
        // Backup cleanup if object is destroyed
        foreach (var kv in ticking)
        {
            if (kv.Value != null)
                StopCoroutine(kv.Value);
        }
        ticking.Clear();
    }
}
