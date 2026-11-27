using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Tooltip("Health points of this enemy")]
    [SerializeField, Min(1)] int maxHealth = 3;
    [Tooltip("Sprite shown when the enemy is defeated")]
    [SerializeField] Sprite deadSprite;

    [HideInInspector] public bool isDamaged;

    int health;
    public bool IsDead { get; private set; }

    void Awake() => health = maxHealth;

    public void TakeDamage(int damage)
    {
        if (IsDead) return;
        health -= damage;
        isDamaged = true;
        if (health <= 0) Die();
    }

    void Die()
    {
        IsDead = true;

        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            if (t.CompareTag("Enemy")) t.tag = "Untagged";

        foreach (MonoBehaviour script in GetComponentsInChildren<MonoBehaviour>())
            if (script != this) script.enabled = false;

        Animator animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.enabled = false;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null && deadSprite != null) spriteRenderer.sprite = deadSprite;
    }
}