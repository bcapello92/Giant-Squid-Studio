using UnityEngine;
using System.Collections;

public class DamageUp : MonoBehaviour
{
    public AudioSource powerUpAudio;

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("[DamageUp] Trigger with " + other.name);

        // Ignore weapon hitboxes – only player body should pick this up
        if (other.GetComponent<AttackArea>() != null)
            return;

        // Find the player regardless of which child collider hit
        var player = other.GetComponentInParent<TestPlayerController>();
        if (player == null)
            return;

        var combat = player.GetComponent<CharacterCombat>();
        if (combat == null || combat.WeaponInstance == null)
        {
            Debug.LogWarning("[DamageUp] Player has no CharacterCombat or WeaponInstance.");
            return;
        }

        // Get the AttackArea from the weapon
        AttackArea damageMod = combat.WeaponInstance.attackArea;

        // Fallback: search in children if not wired
        if (damageMod == null)
            damageMod = player.GetComponentInChildren<AttackArea>();

        if (damageMod == null)
        {
            Debug.LogWarning("[DamageUp] No AttackArea found on player's weapon.");
            return;
        }

        // Apply the buff
        damageMod.damage++;
        Debug.Log("[DamageUp] Increased weapon damage to " + damageMod.damage);

        StartCoroutine(Pickup());
    }

    IEnumerator Pickup()
    {
        transform.localScale = Vector3.zero;

        if (powerUpAudio) powerUpAudio.Play();

        yield return new WaitForSeconds(1f);

        Destroy(gameObject);
    }
}
