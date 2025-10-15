using UnityEngine;

public class CharacterCombat : MonoBehaviour
{
    [SerializeField] Transform weaponMount;        // assign a child transform (hand)
    [SerializeField] WeaponDriver weaponPrefab;    // drag your weapon prefab here
    WeaponDriver weapon;

    void Start()
    {
        if (!weaponMount) weaponMount = transform;
        // Instantiate if you dragged a prefab
        weapon = weaponPrefab && !weaponPrefab.gameObject.scene.IsValid()
            ? Instantiate(weaponPrefab, weaponMount)
            : weaponPrefab;

        if (weapon)
        {
            weapon.AttachTo(weaponMount, gameObject);
            Debug.Log("[Combat] Weapon attached.");
        }
        else Debug.LogWarning("[Combat] No weapon assigned.");
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
            weapon?.PlayAttack();
    }
}
