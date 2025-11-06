using UnityEngine;

public class DamageHitbox2D : MonoBehaviour
{
    public int damage = 10;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!enabled) return;

        var dmg = other.GetComponent<IDamageable>();
        if (dmg != null)
            dmg.TakeDamage(damage);
    }
}
