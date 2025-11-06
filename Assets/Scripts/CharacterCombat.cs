using UnityEngine;

[DisallowMultipleComponent]
public class CharacterCombat : MonoBehaviour
{
    [Header("Mount & Existing Weapon")]
    [SerializeField] Transform weaponMount;       // assign your hand socket
    [SerializeField] WeaponDriver weaponInstance; // drag the existing weapon (child of SpearRoot)
    [SerializeField] Transform spearRoot;         // drag SpearRoot (pivot under mount)
    [SerializeField] SpearAim spearFollower;      // usually already on SpearRoot

    void Awake()
    {
        // Try to auto-find common names, but DO NOT create anything
        if (!weaponMount)
            weaponMount = transform.Find("WeaponMount");

        if (!spearRoot && weaponMount)
            spearRoot = weaponMount.Find("SpearRoot");

        if (!weaponInstance && spearRoot)
            weaponInstance = spearRoot.GetComponentInChildren<WeaponDriver>(true);

        if (!spearFollower && spearRoot)
            spearFollower = spearRoot.GetComponent<SpearAim>();

        // Final validations (logs only)
        if (!weaponMount)
            Debug.LogWarning("[Combat] Missing WeaponMount reference.");
        if (!spearRoot)
            Debug.LogWarning("[Combat] Missing SpearRoot under WeaponMount.");
        if (!weaponInstance)
            Debug.LogWarning("[Combat] No WeaponDriver found under SpearRoot.");
        if (!spearFollower)
            Debug.LogWarning("[Combat] No SpearAim on SpearRoot (aim follow won’t run).");
    }

    void Update()
    {
        // Keep combat behavior (no spawning)
        if (weaponInstance && Input.GetMouseButtonDown(0))
            weaponInstance.PlayAttack();
    }
}
