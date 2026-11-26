using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.SocialPlatforms.Impl;
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
    [Tooltip("Sprite of a full heart")]
    [SerializeField] Sprite FullHeartSprite;
    [Tooltip("Sprite of a lost heart")]
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
    [SerializeField] GameObject Exit;

    [SerializeField] GameObject HealthIcon;

    // azioni dell'Input System (le stesse che si rimappano dalle Opzioni)
    InputAction moveAction;
    InputAction jumpAction;
    InputAction shootUpAction;
    InputAction shootDownAction;
    InputAction shootLeftAction;
    InputAction shootRightAction;
    InputAction shootDiagonalUpAction;
    InputAction shootDiagonalDownAction;
    InputAction shootDiagonalModifierAction;   // tenuto premuto: su/giù sparano in diagonale
    readonly List<InputAction> weaponActions = new List<InputAction>();   // Weapon1, Weapon2, ... (null se mancano)

    float nextShootTime;
    bool isJumping;
    float jumpTimer;
    int health;
    float Invincibility;
    Rigidbody2D rb;
    bool isGrounded;
    public bool isDamaged;
    SpriteRenderer spriteRenderer;
    Animator animator;
    bool facingRight = true;
    Image[] healthIcons;
    ContactFilter2D groundFilter;
    readonly List<Collider2D> groundHits = new List<Collider2D>();

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        health = MaxHealth;
        CreateHealthIcons();
        UpdateHealthUI();

        // il ground check considera solo i collider solidi: i trigger (es. quello delle piattaforme spente) non contano
        groundFilter.SetLayerMask(groundLayer);
        groundFilter.useTriggers = false;

        SetupInput();
    }

    void SetupInput()
    {
        InputActionAsset actions = InputSystem.actions;
        if (actions == null)
        {
            Debug.LogError("PlayerController: Project-wide Input Actions not found (Edit > Project Settings > Input System Package).");
            return;
        }

        moveAction = FindAction(actions, "Player/Move");
        jumpAction = FindAction(actions, "Player/Jump");
        shootUpAction = FindAction(actions, "Player/ShootUp");
        shootDownAction = FindAction(actions, "Player/ShootDown");
        shootLeftAction = FindAction(actions, "Player/ShootLeft");
        shootRightAction = FindAction(actions, "Player/ShootRight");
        shootDiagonalModifierAction = FindAction(actions, "Player/ShootDiagonal");

        // tasti diagonali dedicati: facoltativi, se non ci sono nessun avviso
        shootDiagonalUpAction = actions.FindAction("Player/ShootDiagonalUp");
        shootDiagonalDownAction = actions.FindAction("Player/ShootDiagonalDown");

        // Weapon1 ... Weapon9: quelle che non esistono restano null e vengono ignorate
        weaponActions.Clear();
        for (int n = 1; n <= 9; n++)
            weaponActions.Add(actions.FindAction("Player/Weapon" + n));

        actions.FindActionMap("Player")?.Enable();
    }

    static InputAction FindAction(InputActionAsset actions, string actionName)
    {
        InputAction action = actions.FindAction(actionName);
        if (action == null)
            Debug.LogWarning("PlayerController: action '" + actionName + "' not found in the Input Actions asset.");
        return action;
    }

    static bool Pressed(InputAction action) => action != null && action.WasPressedThisFrame();
    static bool Held(InputAction action) => action != null && action.IsPressed();
    static bool Released(InputAction action) => action != null && action.WasReleasedThisFrame();

    void Update()
    {
        // in pausa nessun comando (niente salti, spari o cambi arma)
        if (PauseMenu.IsPaused) return;

        // controlla se il giocatore è sopra una superficie solida
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundFilter, groundHits) > 0;

        // Movimento: solo l'asse orizzontale dell'azione Move, sempre a velocità piena
        float h = 0;
        if (moveAction != null)
        {
            float x = moveAction.ReadValue<Vector2>().x;
            if (Mathf.Abs(x) > 0.2f)
                h = Mathf.Sign(x);
        }

        if (h != 0)
            TutorialManager.Notify(TutorialAction.Move);

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

        // piattaforma mobile sotto i piedi: il player si sposta insieme a lei
        Vector2 platformVelocity = Vector2.zero;
        if (isGrounded)
        {
            foreach (Collider2D hit in groundHits)
            {
                Platform platform = hit.GetComponentInParent<Platform>();
                if (platform != null && platform.IsMoving)
                {
                    platformVelocity = platform.Velocity;
                    break;
                }
            }
        }

        rb.linearVelocity = new Vector2(h * Speed + platformVelocity.x, rb.linearVelocity.y);

        // se la piattaforma scende, il player resta appoggiato invece di "galleggiare"
        if (platformVelocity.y < 0f && !isJumping && rb.linearVelocity.y > platformVelocity.y)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, platformVelocity.y);

        if (Pressed(jumpAction) && isGrounded)
        {
            isJumping = true;
            jumpTimer = 0f;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, JumpForce);
            AudioManager.Play(Sfx.Jump);
            TutorialManager.Notify(TutorialAction.Jump);
        }

        // più tempo tieni, più in alto salti
        if (isJumping && Held(jumpAction))
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
        if (Released(jumpAction))
            isJumping = false;

        // Cambio arma con le azioni Weapon1, Weapon2, ... (solo tra quelle già raccolte)
        if (InventoryManager.Instance != null)
        {
            for (int i = 0; i < weaponActions.Count; i++)
            {
                if (Pressed(weaponActions[i]))
                {
                    WeaponData before = InventoryManager.Instance.CurrentWeapon;
                    InventoryManager.Instance.SelectSlot(i + 1);

                    // conta per il tutorial solo se l'arma è cambiata davvero
                    if (InventoryManager.Instance.CurrentWeapon != before)
                        TutorialManager.Notify(TutorialAction.SwitchWeapon);
                }
            }
        }

        // Sparo (solo se l'arma è stata raccolta)
        // tenendo premuto continua a sparare, un colpo ogni ShootCooldown secondi
        bool hasWeapon = InventoryManager.Instance != null && InventoryManager.Instance.HasWeapon;

        if (hasWeapon && Time.time >= nextShootTime)
        {
            float forward = facingRight ? 1f : -1f;   // le diagonali sparano verso dove è girato il player
            bool diagonalModifier = Held(shootDiagonalModifierAction);

            // modificatore (es. Ctrl) + su / giù = diagonale in avanti
            if (diagonalModifier && Held(shootUpAction)) Shoot(new Vector2(forward, 1f).normalized);
            else if (diagonalModifier && Held(shootDownAction)) Shoot(new Vector2(forward, -1f).normalized);
            else if (Held(shootRightAction)) Shoot(Vector2.right);
            else if (Held(shootLeftAction)) Shoot(Vector2.left);
            else if (Held(shootUpAction)) Shoot(Vector2.up);
            else if (Held(shootDownAction)) Shoot(Vector2.down);
            else if (Held(shootDiagonalUpAction)) Shoot(new Vector2(forward, 1f).normalized);
            else if (Held(shootDiagonalDownAction)) Shoot(new Vector2(forward, -1f).normalized);
        }
        animator.SetFloat("Speed", Mathf.Abs(h));
        animator.SetBool("IsGrounded", isGrounded);
    }

    void Shoot(Vector2 dir)
    {
        nextShootTime = Time.time + ShootCooldown;

        Vector3 spawnPos = transform.position + (Vector3)(dir * SpawnOffset);
        GameObject bullet = Instantiate(Bullet, spawnPos, Quaternion.identity);

        BulletController bulletController = bullet.GetComponent<BulletController>();
        bulletController.Direction = dir;

        // aspetto, danno ed effetto sulle piattaforme dell'arma equipaggiata
        bulletController.SetWeapon(InventoryManager.Instance.CurrentWeapon);

        AudioManager.Play(Sfx.Shoot);
        TutorialManager.Notify(TutorialAction.Shoot);
    }

    public void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            Damaged(2, false, Sfx.EnemyHit);   // nemici: rispettano l'invincibilità
        }

        else if (other.CompareTag("Spike"))
        {
            Damaged(1, true, Sfx.SpikeHit);    // spike: danno immediato, nessuna attesa
        }

        else if (other.CompareTag("Exit"))
        {
            SceneManager.LoadScene(2);
        }
    }

    public void Damaged(int damage, bool ignoreInvincibility, Sfx hitSound)
    {
        if (!ignoreInvincibility)
        {
            if (Time.time < Invincibility) return;   // ancora invulnerabile ai nemici
            Invincibility = Time.time + InvincibilityTime;
        }

        // dopo il controllo dell'invincibilità: lampeggia solo se il danno arriva davvero
        isDamaged = true;

        health -= damage;

        UpdateHealthUI();   // aggiorna le icone nello stesso istante del danno
        AudioManager.Play(hitSound);
        print("Damage taken");
        print("HP: " + health);

        if (health <= 0)
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // crea un'icona per ogni vita, duplicando la prima
    void CreateHealthIcons()
    {
        if (HealthIconTemplate == null)
        {
            Debug.LogError("PlayerController: the Health Icon Template field is empty!");
            return;
        }

        if (MaxHealth <= 0) return;

        healthIcons = new Image[MaxHealth];
        healthIcons[0] = HealthIconTemplate;

        for (int i = 1; i < MaxHealth; i++)
            healthIcons[i] = Instantiate(HealthIconTemplate, HealthIconTemplate.transform.parent);
    }

    // le prime "health" icone sono piene, le altre vuote
    void UpdateHealthUI()
    {
        if (healthIcons == null) return;

        for (int i = 0; i < healthIcons.Length; i++)
            healthIcons[i].sprite = i < health ? FullHeartSprite : EmptyHeartSprite;
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