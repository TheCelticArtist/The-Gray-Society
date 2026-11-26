using UnityEngine;

public class WeaponPickup : MonoBehaviour
{
    [Tooltip("1 = first slot (level 1 weapon), 2 = second slot (level 2 weapon), ...")]
    [SerializeField, Min(1)] int slotNumber = 1;
    [Tooltip("Icon shown in the inventory. If empty, this object's sprite is used")]
    [SerializeField] Sprite inventoryIcon;

    [Header("Bullet")]
    [Tooltip("Bullet sprite for this weapon. If empty, the Bullet prefab sprite is used")]
    [SerializeField] Sprite bulletSprite;
    [Tooltip("Bullet tint. White = original sprite colors")]
    [SerializeField] Color bulletColor = Color.white;

    [Header("Weapon Effects")]
    [Tooltip("Damage dealt to enemies: 1 for weapon 1, 2 for weapon 2, ...")]
    [SerializeField, Min(1)] int damage = 1;
    [Tooltip("What the bullet does when it hits a platform")]
    [SerializeField] PlatformEffect platformEffect = PlatformEffect.None;

    bool pickedUp;                                 // evita che il suono parta due volte nello stesso frame

    void Start()
    {
        // se quest'arma è già stata raccolta (es. dopo una morte e il ricaricamento del livello) non ricompare
        if (InventoryManager.Instance != null && InventoryManager.Instance.HasWeaponInSlot(slotNumber))
            Destroy(gameObject);
    }

    // funziona sia con collider Trigger...
    void OnTriggerEnter2D(Collider2D other)
    {
        TryPickUp(other);
    }

    // ...sia con collider normale
    void OnCollisionEnter2D(Collision2D collision)
    {
        TryPickUp(collision.collider);
    }

    void TryPickUp(Collider2D other)
    {
        if (pickedUp) return;

        // riconosce il player dallo script, anche se il collider è su un oggetto figlio o manca il tag
        if (other.GetComponentInParent<PlayerController>() == null) return;

        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning("WeaponPickup: no InventoryManager in the scene!");
            return;
        }

        pickedUp = true;

        WeaponData weapon = new WeaponData
        {
            bulletSprite = bulletSprite,
            bulletColor = bulletColor,
            damage = damage,
            platformEffect = platformEffect
        };

        Sprite icon = inventoryIcon != null ? inventoryIcon : GetComponent<SpriteRenderer>().sprite;
        InventoryManager.Instance.AddWeapon(slotNumber, icon, weapon);
        AudioManager.Play(Sfx.Pickup);

        Destroy(gameObject);
    }
}