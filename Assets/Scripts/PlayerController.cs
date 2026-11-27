using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float Speed;
    [SerializeField] float JumpForce;
    [SerializeField] float MaxJumpTime;

    [Header("Health")]
    [SerializeField] int MaxHealth;
    [SerializeField] float InvincibilityTime;

    [Header("Health UI")]
    [Tooltip("The first heart icon inside the container: it gets duplicated for every health point")]
    [SerializeField] Image HealthIconTemplate;
    [SerializeField] Sprite FullHeartSprite;
    [SerializeField] Sprite EmptyHeartSprite;

    [Header("Shooting")]
    [SerializeField] GameObject Bullet;
    [Tooltip("Seconds between one shot and the next (also while holding the shoot key)")]
    [SerializeField] float ShootCooldown = 5;
    [Tooltip("Distance from the player where the bullet spawns")]
    [SerializeField] float SpawnOffset = 0.7f;

    [Header("Ground Check")]
    [SerializeField] Transform groundCheck;
    [SerializeField] float groundCheckRadius = 0.2f;
    [SerializeField] LayerMask groundLayer;

    public bool isDamaged;

    InputAction move, jump, shootUp, shootDown, shootLeft, shootRight, shootDiagonal, shootDiagonalUp, shootDiagonalDown;
    readonly InputAction[] weaponKeys = new InputAction[9];

    Rigidbody2D rb;
    SpriteRenderer spriteRenderer;
    Animator animator;
    Image[] healthIcons;
    ContactFilter2D groundFilter;
    readonly List<Collider2D> groundHits = new List<Collider2D>();

    int health;
    float invincibleUntil, nextShootTime, jumpTimer;
    bool isJumping, isGrounded, facingRight = true;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        health = MaxHealth;
        CreateHealthIcons();
        UpdateHealthUI();

        groundFilter.SetLayerMask(groundLayer);
        groundFilter.useTriggers = false;

        InputActionAsset actions = InputSystem.actions;
        if (actions == null) return;

        InputAction Find(string name) => actions.FindAction("Player/" + name);
        move = Find("Move");
        jump = Find("Jump");
        shootUp = Find("ShootUp");
        shootDown = Find("ShootDown");
        shootLeft = Find("ShootLeft");
        shootRight = Find("ShootRight");
        shootDiagonal = Find("ShootDiagonal");
        shootDiagonalUp = Find("ShootDiagonalUp");
        shootDiagonalDown = Find("ShootDiagonalDown");
        for (int i = 0; i < weaponKeys.Length; i++)
            weaponKeys[i] = Find("Weapon" + (i + 1));

        actions.FindActionMap("Player")?.Enable();
    }

    void Update()
    {
        if (PauseMenu.IsPaused) return;

        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundFilter, groundHits) > 0;

        float x = move != null ? move.ReadValue<Vector2>().x : 0f;
        float h = Mathf.Abs(x) > 0.2f ? Mathf.Sign(x) : 0f;
        if (h != 0)
        {
            facingRight = h > 0;
            spriteRenderer.flipX = !facingRight;
            TutorialManager.Notify(TutorialAction.Move);
        }

        Vector2 platformVelocity = GroundPlatformVelocity();
        rb.linearVelocity = new Vector2(h * Speed + platformVelocity.x, rb.linearVelocity.y);
        if (platformVelocity.y < 0f && !isJumping && rb.linearVelocity.y > platformVelocity.y)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, platformVelocity.y);

        if (Pressed(jump) && isGrounded)
        {
            isJumping = true;
            jumpTimer = 0f;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, JumpForce);
            AudioManager.Play(Sfx.Jump);
            TutorialManager.Notify(TutorialAction.Jump);
        }
        if (isJumping && Held(jump))
        {
            if (jumpTimer < MaxJumpTime)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, JumpForce);
                jumpTimer += Time.deltaTime;
            }
            else isJumping = false;
        }
        if (Released(jump)) isJumping = false;

        InventoryManager inventory = InventoryManager.Instance;
        if (inventory != null)
        {
            for (int i = 0; i < weaponKeys.Length; i++)
            {
                if (!Pressed(weaponKeys[i])) continue;
                WeaponData before = inventory.CurrentWeapon;
                inventory.SelectSlot(i + 1);
                if (inventory.CurrentWeapon != before) TutorialManager.Notify(TutorialAction.SwitchWeapon);
            }

            if (inventory.HasWeapon && Time.time >= nextShootTime)
            {
                Vector2 direction = ShootDirection();
                if (direction != Vector2.zero) Shoot(direction, inventory.CurrentWeapon);
            }
        }

        animator.SetFloat("Speed", Mathf.Abs(h));
        animator.SetBool("IsGrounded", isGrounded);
    }

    Vector2 GroundPlatformVelocity()
    {
        if (!isGrounded) return Vector2.zero;
        foreach (Collider2D hit in groundHits)
        {
            Platform platform = hit.GetComponentInParent<Platform>();
            if (platform != null && platform.IsMoving) return platform.Velocity;
        }
        return Vector2.zero;
    }

    Vector2 ShootDirection()
    {
        float forward = facingRight ? 1f : -1f;
        Vector2 diagonalUp = new Vector2(forward, 1f).normalized;
        Vector2 diagonalDown = new Vector2(forward, -1f).normalized;
        bool modifier = Held(shootDiagonal);   // es. Ctrl + su / giù

        if (modifier && Held(shootUp)) return diagonalUp;
        if (modifier && Held(shootDown)) return diagonalDown;
        if (Held(shootRight)) return Vector2.right;
        if (Held(shootLeft)) return Vector2.left;
        if (Held(shootUp)) return Vector2.up;
        if (Held(shootDown)) return Vector2.down;
        if (Held(shootDiagonalUp)) return diagonalUp;
        if (Held(shootDiagonalDown)) return diagonalDown;
        return Vector2.zero;
    }

    void Shoot(Vector2 direction, WeaponData weapon)
    {
        nextShootTime = Time.time + ShootCooldown;

        GameObject bullet = Instantiate(Bullet, transform.position + (Vector3)(direction * SpawnOffset), Quaternion.identity);
        BulletController bulletController = bullet.GetComponent<BulletController>();
        bulletController.Direction = direction;
        bulletController.SetWeapon(weapon);

        AudioManager.Play(Sfx.Shoot);
        TutorialManager.Notify(TutorialAction.Shoot);
    }

    public void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy")) Damaged(2, false, Sfx.EnemyHit);
        else if (other.CompareTag("Spike")) Damaged(1, true, Sfx.SpikeHit);
        else if (other.CompareTag("Exit")) SceneManager.LoadScene(2);
    }

    public void Damaged(int damage, bool ignoreInvincibility, Sfx hitSound)
    {
        if (!ignoreInvincibility)
        {
            if (Time.time < invincibleUntil) return;
            invincibleUntil = Time.time + InvincibilityTime;
        }

        isDamaged = true;
        health -= damage;
        UpdateHealthUI();
        AudioManager.Play(hitSound);

        if (health <= 0)
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void CreateHealthIcons()
    {
        if (HealthIconTemplate == null || MaxHealth <= 0) return;

        healthIcons = new Image[MaxHealth];
        healthIcons[0] = HealthIconTemplate;
        for (int i = 1; i < MaxHealth; i++)
            healthIcons[i] = Instantiate(HealthIconTemplate, HealthIconTemplate.transform.parent);
    }

    void UpdateHealthUI()
    {
        if (healthIcons == null) return;
        for (int i = 0; i < healthIcons.Length; i++)
            healthIcons[i].sprite = i < health ? FullHeartSprite : EmptyHeartSprite;
    }

    static bool Pressed(InputAction action) => action != null && action.WasPressedThisFrame();
    static bool Held(InputAction action) => action != null && action.IsPressed();
    static bool Released(InputAction action) => action != null && action.WasReleasedThisFrame();

    void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}