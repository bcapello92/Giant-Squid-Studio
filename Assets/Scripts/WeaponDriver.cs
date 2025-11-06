using UnityEngine;

[DisallowMultipleComponent]
public class WeaponDriver : MonoBehaviour
{
    [Header("Animator")]
    public string attackTrigger = "jab";  // must match Animator parameter
    public Animator animator;             // auto-filled in Awake

    [Header("Optional hitbox")]
    public AttackArea attackArea;         // can be null
    public float autoDisableHitboxDelay = 0f; // 0 = use animation events

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (!animator) animator = GetComponentInChildren<Animator>(true);
        if (!animator) Debug.LogError("[WeaponDriver] No Animator found on Weapon_Harpoon 1.");
        if (attackArea) attackArea.gameObject.SetActive(false);
    }

    public void PlayAttack()
    {
        if (!animator)
        {
            Debug.LogError("[WeaponDriver] PlayAttack called but no Animator.");
            return;
        }

        // Ensure the trigger exists and is a Trigger
        bool hasTrig = false;
        foreach (var p in animator.parameters)
            if (p.name == attackTrigger && p.type == AnimatorControllerParameterType.Trigger)
            { hasTrig = true; break; }

        if (!hasTrig)
        {
            Debug.LogError($"[WeaponDriver] Animator missing Trigger '{attackTrigger}'.");
            return;
        }

        animator.ResetTrigger(attackTrigger);
        animator.SetTrigger(attackTrigger);
        if (animator.speed == 0f) animator.speed = 1f;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

    // Animation Events (optional)
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
