using UnityEngine;

// Vita del nemico. Quando arriva a 0: si ferma, cambia sprite e diventa innocuo.
public class EnemyHealth : MonoBehaviour
{
    [Tooltip("Health points of this enemy")]
    [SerializeField, Min(1)] int maxHealth = 3;
    [Tooltip("Sprite shown when the enemy is defeated")]
    [SerializeField] Sprite deadSprite;

    // diventa true quando il nemico viene colpito (lo legge il DmgFilter, che lo rimette a false)
    [HideInInspector] public bool isDamaged;

    int health;
    bool isDead;

    public bool IsDead => isDead;

    void Awake()
    {
        health = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;

        health -= damage;
        isDamaged = true;

        if (health <= 0)
            Die();
    }

    void Die()
    {
        isDead = true;

        // innocuo: il player prende danno solo dagli oggetti con tag "Enemy"
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.CompareTag("Enemy"))
                t.tag = "Untagged";
        }

        // si ferma: spegne gli altri script del nemico (movimento, pattuglia, DmgFilter...)
        foreach (MonoBehaviour script in GetComponentsInChildren<MonoBehaviour>())
        {
            if (script != this)
                script.enabled = false;
        }

        // spegne l'animazione, altrimenti rimetterebbe subito lo sprite di prima
        Animator animator = GetComponentInChildren<Animator>();
        if (animator != null)
            animator.enabled = false;

        // blocca la fisica dove si trova
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        // cambia sprite
        SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null && deadSprite != null)
            spriteRenderer.sprite = deadSprite;
    }
}