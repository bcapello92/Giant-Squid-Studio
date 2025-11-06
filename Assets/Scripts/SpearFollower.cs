using UnityEngine;

public class SpearAim : MonoBehaviour
{
    [Header("Refs")]
    public TestPlayerController player;   // drag your Player here
    public Transform handMount;           // drag HandMount here (usually Spear's parent)
    public SpriteRenderer sprite;         // optional, only if you want flip fix

    [Header("Tuning")]
    [Tooltip("Add this so the sprite points correctly. 0 if art points right, -90 if up, etc.")]
    public float aimOffsetDeg = 0f;
    public bool flipWhenLeft = false;     // your body already flips; keep false unless needed

    void Reset()
    {
        // Auto-wire common refs when you add the component
        if (!player) player = GetComponentInParent<TestPlayerController>();
        if (!handMount && transform.parent) handMount = transform.parent;
        if (!sprite) sprite = GetComponentInChildren<SpriteRenderer>();
    }

    void LateUpdate()
    {
        if (!player || !handMount) return;

        // 1) Glue spear to the hand (local space) — prevents any float
        if (transform.parent != handMount) transform.SetParent(handMount, true);
        transform.localPosition = Vector3.zero;

        // 2) Use the controller’s AimDir (single source of truth)
        Vector2 aim = (player.AimDir.sqrMagnitude > 0.0001f) ? player.AimDir : Vector2.right;
        float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg + aimOffsetDeg;

        // 3) Rotate around Z
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

        // 4) Optional sprite flip (usually NOT needed since your bodySR handles facing)
        if (sprite && flipWhenLeft)
            sprite.flipY = Mathf.Abs(Mathf.DeltaAngle(angle - aimOffsetDeg, 0f)) > 90f;
    }
}
