using UnityEngine;

[DisallowMultipleComponent]
public class CharacterCombat : MonoBehaviour
{
    [Header("Mount & Existing Weapon")]
    [SerializeField] Transform weaponMount;
    [SerializeField] WeaponDriver weaponInstance;
    [SerializeField] Transform spearRoot;
    [SerializeField] SpearAim spearFollower;

    public WeaponDriver WeaponInstance => weaponInstance;
    public bool IsBlocking => weaponInstance && weaponInstance.IsBlocking;
    void Awake()
    {
        if (!weaponMount)
            weaponMount = transform.Find("WeaponMount");

        if (!spearRoot && weaponMount)
            spearRoot = weaponMount.Find("SpearRoot");

        if (!weaponInstance && spearRoot)
            weaponInstance = spearRoot.GetComponentInChildren<WeaponDriver>(true);

        if (!spearFollower && spearRoot)
            spearFollower = spearRoot.GetComponent<SpearAim>();

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
        if (!weaponInstance) return;

        // Left click: light attack (disabled while blocking if you want)
        if (!IsBlocking && Input.GetMouseButtonDown(0))
        {
            weaponInstance.PlayLightAttack();
        }

        // Right click: hold to block
        if (Input.GetMouseButtonDown(1))
        {
            weaponInstance.StartBlock();
        }
        if (Input.GetMouseButtonUp(1))
        {
            weaponInstance.StopBlock();
        }
    }
}
