using UnityEngine;

public class BulletController : MonoBehaviour
{
    [SerializeField] float Speed;
    [SerializeField] float Lifetime;

    [Tooltip("0 if the bullet sprite is drawn facing RIGHT. Use -90 if it faces up, 180 if it faces left, 90 if it faces down")]
    [SerializeField] float spriteAngleOffset = 0f;

    public Vector2 Direction = Vector2.right;

    int damage = 1;
    PlatformEffect platformEffect = PlatformEffect.None;
    bool hasHit;                     // evita di colpire due cose nello stesso frame

    void Start()
    {
        // gira il proiettile verso la direzione del colpo (destra 0°, su 90°, sinistra 180°, giù -90°)
        float angle = Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg + spriteAngleOffset;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        Destroy(gameObject, Lifetime);   // sparisce da solo dopo "Lifetime" secondi
    }

    void Update()
    {
        // movimento in coordinate del mondo: la rotazione dello sprite non cambia la traiettoria
        transform.position += (Vector3)(Direction * Speed * Time.deltaTime);
    }

    // aspetto, danno ed effetto sulle piattaforme dell'arma che lo ha sparato
    public void SetWeapon(WeaponData weapon)
    {
        if (weapon == null) return;

        damage = weapon.damage;
        platformEffect = weapon.platformEffect;

        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr == null) return;

        if (weapon.bulletSprite != null)
        {
            // un Animator sul proiettile cambierebbe lo sprite ogni frame: lo spengo così resta quello dell'arma
            Animator anim = GetComponentInChildren<Animator>();
            if (anim != null)
                anim.enabled = false;

            sr.sprite = weapon.bulletSprite;
        }

        sr.color = weapon.bulletColor;
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