using UnityEngine;
using UnityEngine.Tilemaps;


[RequireComponent(typeof(TilemapCollider2D))]
public class LavaDamageTileTrigger : MonoBehaviour
{
    public int damagePerSecond = 20;


    void Reset()
    {
        var col = GetComponent<TilemapCollider2D>();
        col.isTrigger = true;
    }


    void OnTriggerStay2D(Collider2D other)
    {
        var dmg = other.GetComponent<IDamageable>();
        if (dmg != null)
        {
            dmg.TakeDamage(Mathf.CeilToInt(damagePerSecond * Time.deltaTime));
        }
    }
}