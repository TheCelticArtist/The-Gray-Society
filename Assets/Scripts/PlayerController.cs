using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerController : MonoBehaviour
{
    [Header("Movimento")]
    [SerializeField] float Speed;
    [SerializeField] float JumpForce;
    [SerializeField] float MaxJumpTime;

    [Header("Salute")]
    [SerializeField] int MaxHealth;
    [SerializeField] float InvincibilityTime;

    [Header("Sparo")]
    [SerializeField] GameObject Bullet;
    [SerializeField] float ShootCooldown = 5;
    [SerializeField] float SpawnOffset = 0.7f;

    [Header("Ground Check")]
    [SerializeField] Transform groundCheck; 
    [SerializeField] float groundCheckRadius = 0.2f; 
    [SerializeField] LayerMask groundLayer; 

    float nextShootTime;
    bool isJumping;
    float jumpTimer;
    int health;
    float Invincibility;
    Rigidbody2D rb;
    bool isGrounded;
    SpriteRenderer spriteRenderer;
    Animator animator;
    bool facingRight = true;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        health = MaxHealth;
    }

    void Update()
    {   
        // controlla se il giocatore è sopra una superficie
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        // Movimenti e Salti
        float h = 0;
        if (Input.GetKey(KeyCode.D)) h = 1;
        if (Input.GetKey(KeyCode.A)) h = -1;

        // Gestione flip dello sprite
        if (h > 0 && !facingRight)
        {
            spriteRenderer.flipX = false;
            facingRight = true;
        }
        else if (h < 0 && facingRight)
        {
            spriteRenderer.flipX = true;
            facingRight = false;
        }

        rb.linearVelocity = new Vector2(h * Speed, rb.linearVelocity.y);

        if (Input.GetKeyDown(KeyCode.W) && isGrounded)
        {
            isJumping = true;
            jumpTimer = 0f;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, JumpForce);
        }

        // più tempo tieni, più in alto salti
        if (isJumping && Input.GetKey(KeyCode.W))
        {
            if (jumpTimer < MaxJumpTime)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, JumpForce);
                jumpTimer += Time.deltaTime;
            }
            else
            {
                isJumping = false;
            }
        }

        // rilascio del tasto del salto
        if (Input.GetKeyUp(KeyCode.W))
            isJumping = false;

        // Sparo
        if (Time.time >= nextShootTime)
        {
            if (Input.GetKeyDown(KeyCode.RightArrow)) Shoot(Vector2.right);
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) Shoot(Vector2.left);
            else if (Input.GetKeyDown(KeyCode.UpArrow)) Shoot(Vector2.up);
            else if (Input.GetKeyDown(KeyCode.DownArrow)) Shoot(Vector2.down);
        }
        animator.SetFloat("Speed", Mathf.Abs(h));
        animator.SetBool("IsGrounded", isGrounded);
    }

    void Shoot(Vector2 dir)
    {
        nextShootTime = Time.time + ShootCooldown;

        Vector3 spawnPos = transform.position + (Vector3)(dir * SpawnOffset);
        GameObject bullet = Instantiate(Bullet, spawnPos, Quaternion.identity);
        bullet.GetComponent<BulletController>().Direction = dir;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
            Damaged(1);
    }

    void Damaged(int damage)
    {
        if (Time.time < Invincibility) return;   // ancora invulnerabile

        health -= damage;
        Invincibility = Time.time + InvincibilityTime;
        print("Danno Subito");
        print("HP: " + health);

        if (health <= 0)
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Opzionale: Per vedere il raggio del sensore nella Scene View mentre lavori
    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}