using UnityEngine;

public class WeaponPickup : MonoBehaviour
{
    [Tooltip("1 = first slot (level 1 weapon), 2 = second slot (level 2 weapon), ...")]
    [SerializeField, Min(1)] int slotNumber = 1;
    [Tooltip("Icon shown in the inventory. If empty, this object's sprite is used")]
    [SerializeField] Sprite inventoryIcon;

    [Header("Bullet")]
    [Tooltip("Static bullet sprite for this weapon. If empty, the Bullet prefab sprite is used")]
    [SerializeField] Sprite bulletSprite;
    [Tooltip("Bullet animation frames (e.g. the slices of a sprite sheet, in order). If set, they replace Bullet Sprite")]
    [SerializeField] Sprite[] bulletAnimation;
    [Tooltip("Animation speed in frames per second")]
    [SerializeField, Min(0.1f)] float bulletFrameRate = 12f;
    [Tooltip("Bullet tint. White = original sprite colors")]
    [SerializeField] Color bulletColor = Color.white;

    [Header("Weapon Effects")]
    [Tooltip("Damage dealt to enemies: 1 for weapon 1, 2 for weapon 2, ...")]
    [SerializeField, Min(1)] int damage = 1;
    [Tooltip("What the bullet does when it hits a platform")]
    [SerializeField] PlatformEffect platformEffect = PlatformEffect.None;

    bool pickedUp;

    void Start()
    {
        if (InventoryManager.Instance != null && InventoryManager.Instance.HasWeaponInSlot(slotNumber))
            Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other) => TryPickUp(other);
    void OnCollisionEnter2D(Collision2D collision) => TryPickUp(collision.collider);

    void TryPickUp(Collider2D other)
    {
        if (pickedUp || other.GetComponentInParent<PlayerController>() == null || InventoryManager.Instance == null) return;
        pickedUp = true;

        Sprite icon = inventoryIcon != null ? inventoryIcon : GetComponent<SpriteRenderer>().sprite;
        InventoryManager.Instance.AddWeapon(slotNumber, icon, new WeaponData
        {
            bulletSprite = bulletSprite,
            bulletFrames = bulletAnimation,
            bulletFrameRate = bulletFrameRate,
            bulletColor = bulletColor,
            damage = damage,
            platformEffect = platformEffect
        });

        AudioManager.Play(Sfx.Pickup);
        Destroy(gameObject);
    }
}