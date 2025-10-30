using UnityEngine;

[RequireComponent(typeof(Animator))]
public class WeaponDriver : MonoBehaviour
{
    [Header("Animator")]
    public string attackTrigger = "jab";   // must match Animator parameter
    public Animator animator;                 // auto-filled in Awake

    [Header("Hitbox (optional)")]
    public AttackArea attackArea;             // child trigger; can be null
    public float autoDisableHitboxDelay = 0f; // 0 means use animation events

    GameObject owner;

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (!animator) Debug.LogError("[Weapon] No Animator found!");
        if (attackArea) attackArea.gameObject.SetActive(false);
    }

    public void AttachTo(Transform mount, GameObject ownerGO)
    {
        owner = ownerGO;
        transform.SetParent(mount, worldPositionStays: false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        if (attackArea) attackArea.owner = ownerGO;
    }

   
        public void PlayAttack()
    {
        if (!animator) { Debug.LogError("[Weapon] No Animator."); return; }
        bool hasParam = false;
        foreach (var p in animator.parameters) if (p.name == attackTrigger && p.type == AnimatorControllerParameterType.Trigger) { hasParam = true; break; }
        if (!hasParam) { Debug.LogError($"[Weapon] Animator missing Trigger '{attackTrigger}'."); return; }

        animator.ResetTrigger(attackTrigger);
        animator.SetTrigger(attackTrigger);
        if (animator.speed == 0f) animator.speed = 1f;

        Debug.Log($"[Weapon] Triggered '{attackTrigger}' on {animator.runtimeAnimatorController?.name}");
    }



// ---- Animation Events (optional but recommended) ----
public void StartSwing()
    {
        if (!attackArea) return;
        attackArea.gameObject.SetActive(true);
        attackArea.StartSwing();
        if (autoDisableHitboxDelay > 0f) Invoke(nameof(EndSwing), autoDisableHitboxDelay);
        Debug.Log("[Weapon] StartSwing()");
    }

    public void EndSwing()
    {
        if (!attackArea) return;
        attackArea.EndSwing();
        attackArea.gameObject.SetActive(false);
        Debug.Log("[Weapon] EndSwing()");
    }

    // If you want a single-hit frame:
    public void DealDamageFrame()
    {
        if (!attackArea) return;
        attackArea.gameObject.SetActive(true);
        attackArea.StartSwing();
        attackArea.EndSwing();
        attackArea.gameObject.SetActive(false);
        Debug.Log("[Weapon] DealDamageFrame()");
    }
    public void OnAttackAnimationEnd()
    {
        if (!animator) return;
        animator.CrossFade("idle", .1f, 0, 0f);
    }
}
