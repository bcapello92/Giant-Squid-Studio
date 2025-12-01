using UnityEngine;

[DisallowMultipleComponent]
public class WeaponDriver : MonoBehaviour
{
    [Header("Animator")]
    public string lightAttackTrigger = "jab";       // left-click / normal
    public string blockBool = "IsBlocking";         // MUST match Animator bool
    public Animator animator;                       // auto-filled in Awake

    [Header("Optional hitbox")]
    public AttackArea attackArea;
    public float autoDisableHitboxDelay = 0f;

    public bool IsBlocking { get; private set; }

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (!animator) animator = GetComponentInChildren<Animator>(true);
        if (!animator) Debug.LogError("[WeaponDriver] No Animator found on Weapon.");

        if (attackArea)
        {
            attackArea.gameObject.SetActive(false);

            // IMPORTANT: set owner so the hitbox doesn't damage the player
            if (attackArea.owner == null)
            {
                // assume player lives somewhere above this weapon
                var player = GetComponentInParent<TestPlayerController>();
                if (player != null)
                {
                    attackArea.owner = player.gameObject;
                }
                else
                {
                    // Fallback: root object as owner
                    attackArea.owner = transform.root.gameObject;
                }
            }
        }
    }

    // ---------------- ATTACK ----------------
    // old name kept if anything still calls it
    public void PlayAttack() => PlayLightAttack();

    public void PlayLightAttack()
    {
        PlayTrigger(lightAttackTrigger);
    }

    void PlayTrigger(string triggerName)
    {
        if (!animator)
        {
            Debug.LogError("[WeaponDriver] PlayTrigger called but no Animator.");
            return;
        }

        bool hasTrig = false;
        foreach (var p in animator.parameters)
        {
            if (p.name == triggerName && p.type == AnimatorControllerParameterType.Trigger)
            {
                hasTrig = true;
                break;
            }
        }

        if (!hasTrig)
        {
            Debug.LogError($"[WeaponDriver] Animator missing Trigger '{triggerName}'.");
            return;
        }

        animator.ResetTrigger(triggerName);
        animator.SetTrigger(triggerName);

        if (animator.speed == 0f) animator.speed = 1f;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

    // ---------------- BLOCK ----------------
    public void StartBlock()
    {
        IsBlocking = true;
        SetBlockAnimator(true);
    }

    public void StopBlock()
    {
        IsBlocking = false;
        SetBlockAnimator(false);
    }

    void SetBlockAnimator(bool value)
    {
        if (!animator || string.IsNullOrEmpty(blockBool)) return;

        // Make sure the parameter exists & is a bool (optional safety)
        bool hasBool = false;
        foreach (var p in animator.parameters)
        {
            if (p.name == blockBool && p.type == AnimatorControllerParameterType.Bool)
            {
                hasBool = true;
                break;
            }
        }
        if (!hasBool)
        {
            Debug.LogWarning($"[WeaponDriver] Animator missing Bool '{blockBool}' for blocking.");
            return;
        }

        animator.SetBool(blockBool, value);
    }

    // ---------------- HITBOX EVENTS (for light attack) ----------------
    public void StartSwing()
    {
        if (!attackArea) return;
        attackArea.gameObject.SetActive(true);
        attackArea.StartSwing();
        if (autoDisableHitboxDelay > 0f) Invoke(nameof(EndSwing), autoDisableHitboxDelay);
    }

    public void EndSwing()
    {
        if (!attackArea) return;
        attackArea.EndSwing();
        attackArea.gameObject.SetActive(false);
    }

    public void DealDamageFrame()
    {
        if (!attackArea) return;
        attackArea.gameObject.SetActive(true);
        attackArea.StartSwing();
        attackArea.EndSwing();
        attackArea.gameObject.SetActive(false);
    }
}
