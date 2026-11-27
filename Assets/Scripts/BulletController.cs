using UnityEngine;

public class BulletController : MonoBehaviour
{
    [SerializeField] float Speed;
    [SerializeField] float Lifetime;
    [Tooltip("Turn the sprite toward the shot direction. Disable it for round bullets that must look the same in every direction")]
    [SerializeField] bool rotateWithDirection = true;
    [Tooltip("0 if the bullet sprite is drawn facing RIGHT. Use -90 if it faces up, 180 if it faces left, 90 if it faces down")]
    [SerializeField] float spriteAngleOffset = 0f;

    public Vector2 Direction = Vector2.right;

    int damage = 1;
    PlatformEffect platformEffect = PlatformEffect.None;
    bool hasHit;

    SpriteRenderer spriteRenderer;
    Sprite[] frames;
    float frameDuration, frameTimer;
    int frameIndex;

    void Start()
    {
        if (rotateWithDirection)
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg + spriteAngleOffset);

        Destroy(gameObject, Lifetime);
    }

    void Update()
    {
        transform.position += (Vector3)(Direction * Speed * Time.deltaTime);

        if (frames == null || frames.Length < 2) return;
        frameTimer += Time.deltaTime;
        if (frameTimer < frameDuration) return;
        frameTimer -= frameDuration;
        frameIndex = (frameIndex + 1) % frames.Length;
        if (frames[frameIndex] != null) spriteRenderer.sprite = frames[frameIndex];
    }

    public void SetWeapon(WeaponData weapon)
    {
        if (weapon == null) return;
        damage = weapon.damage;
        platformEffect = weapon.platformEffect;

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null) return;
        spriteRenderer.drawMode = SpriteDrawMode.Simple;

        bool hasFrames = weapon.bulletFrames != null && weapon.bulletFrames.Length > 0 && weapon.bulletFrames[0] != null;
        Sprite sprite = hasFrames ? weapon.bulletFrames[0] : weapon.bulletSprite;
        if (sprite != null)
        {
            Animator animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.enabled = false;
            spriteRenderer.sprite = sprite;
        }

        if (hasFrames)
        {
            frames = weapon.bulletFrames;
            frameDuration = 1f / Mathf.Max(weapon.bulletFrameRate, 0.1f);
        }

        spriteRenderer.color = weapon.bulletColor;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit) return;

        Platform platform = other.GetComponentInParent<Platform>();
        EnemyHealth enemy = other.GetComponentInParent<EnemyHealth>();

        if (platform != null) platform.OnHit(platformEffect);
        else if (enemy != null)
        {
            if (enemy.IsDead) return;
            enemy.TakeDamage(damage);
        }
        else if (!other.CompareTag("Enemy") && !other.CompareTag("Ground")) return;

        hasHit = true;
        Destroy(gameObject);
    }
}