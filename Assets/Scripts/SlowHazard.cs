using UnityEngine;

public class FeetHazardSlow : MonoBehaviour
{
    [SerializeField] private TestPlayerController player;

    [Header("Hazard Slow")]
    [SerializeField, Range(0f, 1f)]
    private float hazardSpeedMultiplier = 0.25f;  // 25% speed (75% slower)

    private int hazardsInside = 0;

    private void Reset()
    {
        // Auto-wire the player when you add this in the editor
        if (player == null)
            player = GetComponentInParent<TestPlayerController>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Only react to hazard tiles (set their Tag to "Hazard")
        if (!other.CompareTag("Hazard"))
            return;

        hazardsInside++;

        // First hazard we stepped onto
        if (hazardsInside == 1 && player != null)
        {
            player.SetExternalSpeedMultiplier(hazardSpeedMultiplier);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Hazard"))
            return;

        hazardsInside = Mathf.Max(0, hazardsInside - 1);

        // Left the last overlapping hazard
        if (hazardsInside == 0 && player != null)
        {
            player.SetExternalSpeedMultiplier(1f); // back to normal
        }
    }

    private void OnDisable()
    {
        // Safety: if feet object gets disabled, restore speed
        if (player != null)
            player.SetExternalSpeedMultiplier(1f);

        hazardsInside = 0;
    }
}
 