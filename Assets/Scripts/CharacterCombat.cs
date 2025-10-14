using UnityEngine;
using UnityEngine.EventSystems;

public class CharacterCombat : MonoBehaviour
{
    [SerializeField] Transform weaponMount;     // drag your hand mount
    [SerializeField] WeaponDriver equippedWeapon; // drag an instance or prefab

    void Start()
    {
        if (!weaponMount) weaponMount = transform; // fallback

        if (equippedWeapon != null)
        {
            // If you dragged a prefab, instantiate it
            if (!equippedWeapon.gameObject.scene.IsValid())
            {
                equippedWeapon = Instantiate(equippedWeapon, weaponMount);
            }
            equippedWeapon.AttachTo(weaponMount, gameObject);
            Debug.Log("[Combat] Weapon attached.");
        }
        else
        {
            Debug.LogWarning("[Combat] No weapon assigned!");
        }
    }

    void Update()
    {
        // Optional: ignore clicks over UI
        if (EventSystem.current && EventSystem.current.IsPointerOverGameObject())
            return;

        if (Input.GetMouseButtonDown(0))
        {
            if (equippedWeapon != null)
            {
                Debug.Log("[Combat] Click → PlayAttack()");
                equippedWeapon.PlayAttack();
            }
            else Debug.LogWarning("[Combat] No weapon to attack with.");
        }
    }
}
