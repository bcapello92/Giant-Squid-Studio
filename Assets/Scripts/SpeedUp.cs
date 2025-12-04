using UnityEngine;
using System.Collections;

public class SpeedUp : MonoBehaviour
{
    [Header("Buff Amounts")]
    [Tooltip("How much to add to the player's moveSpeed.")]
    public float moveSpeedIncrease = 1.5f;

    [Tooltip("How much to add to the player's dashSpeed.")]
    public float dashSpeedIncrease = 2f;

    [Header("FX")]
    public AudioSource powerUpAudio;

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("[SpeedUp] Trigger with " + other.name);

        // Ignore weapon hitboxes – only player body should pick this up
        if (other.GetComponent<AttackArea>() != null)
            return;

        // Find the player regardless of which child collider hit
        var player = other.GetComponentInParent<TestPlayerController>();
        if (player == null)
            return;

        // Apply the buff to movement / dash
        float oldMove = player.moveSpeed;
        float oldDash = player.dashSpeed;

        player.moveSpeed += moveSpeedIncrease;
        player.dashSpeed += dashSpeedIncrease;

        Debug.Log($"[SpeedUp] Increased moveSpeed {oldMove} -> {player.moveSpeed}, " +
                  $"dashSpeed {oldDash} -> {player.dashSpeed}");
        RunManager.I?.AddBuff("SpeedUp");
        StartCoroutine(Pickup());
    }

    IEnumerator Pickup()
    {
        // Hide the pickup visually
        transform.localScale = Vector3.zero;

        // Play audio if assigned
        if (powerUpAudio) powerUpAudio.Play();

        // Give audio time to play, then destroy
        yield return new WaitForSeconds(1f);

        Destroy(gameObject);
    }
}
