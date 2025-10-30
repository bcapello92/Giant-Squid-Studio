using UnityEngine;

public class PowerupPickup : MonoBehaviour
{
    public string buffId = "atk_up";
    public int healAmount = 10;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (RunManager.I != null)
        {
            // example: heal + add buff
            RunManager.I.Heal(healAmount);
            RunManager.I.AddBuff(buffId);
        }
        Destroy(gameObject);
    }
}
