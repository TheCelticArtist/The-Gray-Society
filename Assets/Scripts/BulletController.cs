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
    bool hasHit;                     // evita di colpire due cose nello stesso frame
    static bool scaleWarned;         // l'avviso sulla scala compare una volta sola

    // animazione a frame (sprite sheet dell'arma)
    SpriteRenderer spriteRenderer;
    Sprite[] frames;
    float frameDuration;
    float frameTimer;
    int frameIndex;

    void Start()
    {
        // gira il proiettile verso la direzione del colpo (destra 0°, su 90°, sinistra 180°, giù -90°)
        if (rotateWithDirection)
        {
            float angle = Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg + spriteAngleOffset;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        Destroy(gameObject, Lifetime);   // sparisce da solo dopo "Lifetime" secondi
    }

    void Update()
    {
        // movimento in coordinate del mondo: la rotazione dello sprite non cambia la traiettoria
        transform.position += (Vector3)(Direction * Speed * Time.deltaTime);

        // animazione: passa al frame successivo e ricomincia dal primo alla fine
        if (frames != null && frames.Length > 1)
        {
            frameTimer += Time.deltaTime;
            if (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                frameIndex = (frameIndex + 1) % frames.Length;

                if (frames[frameIndex] != null)
                    spriteRenderer.sprite = frames[frameIndex];
            }
        }
    }

    // aspetto, danno ed effetto sulle piattaforme dell'arma che lo ha sparato
    public void SetWeapon(WeaponData weapon)
    {
        if (weapon == null) return;

        damage = weapon.damage;
        platformEffect = weapon.platformEffect;

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null) return;

        // in modalità Sliced/Tiled il nuovo sprite verrebbe stirato alla misura di quello vecchio
        spriteRenderer.drawMode = SpriteDrawMode.Simple;

        // scala X diversa da Y = sprite stirato (si nota soprattutto quando il proiettile ruota)
        Vector3 scale = spriteRenderer.transform.lossyScale;
        if (!scaleWarned && Mathf.Abs(Mathf.Abs(scale.x) - Mathf.Abs(scale.y)) > 0.0001f)
        {
            Debug.LogWarning("Bullet: the bullet has a non-uniform scale (" + scale.x + ", " + scale.y +
                             "), so its sprite looks stretched. Set Scale X and Y to the same value in the Bullet prefab.");
            scaleWarned = true;
        }

        bool hasFrames = weapon.bulletFrames != null && weapon.bulletFrames.Length > 0 && weapon.bulletFrames[0] != null;

        if (hasFrames || weapon.bulletSprite != null)
        {
            // un Animator sul proiettile cambierebbe lo sprite ogni frame: lo spengo così resta quello dell'arma
            Animator anim = GetComponentInChildren<Animator>();
            if (anim != null)
                anim.enabled = false;
        }

        if (hasFrames)
        {
            // animazione: parte dal primo frame
            frames = weapon.bulletFrames;
            frameDuration = 1f / Mathf.Max(weapon.bulletFrameRate, 0.1f);
            frameTimer = 0f;
            frameIndex = 0;
            spriteRenderer.sprite = frames[0];
        }
        else if (weapon.bulletSprite != null)
        {
            spriteRenderer.sprite = weapon.bulletSprite;
        }

        spriteRenderer.color = weapon.bulletColor;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit) return;

        // piattaforma: applica l'effetto dell'arma
        Platform platform = other.GetComponentInParent<Platform>();
        if (platform != null)
        {
            platform.OnHit(platformEffect);
            Hit();
            return;
        }

        // nemico con la vita: gli toglie il danno dell'arma
        EnemyHealth enemy = other.GetComponentInParent<EnemyHealth>();
        if (enemy != null)
        {
            if (enemy.IsDead) return;    // i nemici sconfitti vengono attraversati

            enemy.TakeDamage(damage);
            Hit();
            return;
        }

        if (other.CompareTag("Enemy") || other.CompareTag("Ground"))
            Hit();
    }

    void Hit()
    {
        hasHit = true;
        Destroy(gameObject);
    }
}