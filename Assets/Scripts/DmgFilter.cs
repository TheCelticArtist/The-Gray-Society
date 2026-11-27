using UnityEngine;

public class DmgFilter : MonoBehaviour
{
    [Tooltip("The object that gets damaged (Player or Enemy). If empty, this object is used")]
    [SerializeField] GameObject Victim;
    [Tooltip("The sprite that flashes. If empty, the Victim's SpriteRenderer is used")]
    [SerializeField] SpriteRenderer DmgColor;
    [Tooltip("How long the flashing lasts, in seconds")]
    [SerializeField] float DmgTime = 1.5f;
    [Tooltip("Seconds between one color change and the next (0 = every frame)")]
    [SerializeField] float BlinkInterval = 0.1f;
    [SerializeField] Color FlashColor = Color.red;

    PlayerController player;
    EnemyHealth enemy;
    Color originalColor;
    bool isFlashing, showingFlash;
    float flashTimer, blinkTimer;

    bool VictimDamaged => (player != null && player.isDamaged) || (enemy != null && enemy.isDamaged);

    void Start()
    {
        if (Victim == null) Victim = gameObject;
        player = Victim.GetComponent<PlayerController>();
        enemy = Victim.GetComponent<EnemyHealth>();

        if (DmgColor == null) DmgColor = Victim.GetComponentInChildren<SpriteRenderer>();
        if (DmgColor == null) { enabled = false; return; }
        originalColor = DmgColor.color;
    }

    void Update()
    {
        if (!isFlashing && VictimDamaged)
        {
            isFlashing = true;
            flashTimer = DmgTime;
            blinkTimer = 0f;
            showingFlash = false;
        }
        if (!isFlashing) return;

        flashTimer -= Time.deltaTime;
        if (flashTimer <= 0f) { StopFlash(); return; }

        blinkTimer -= Time.deltaTime;
        if (blinkTimer <= 0f)
        {
            showingFlash = !showingFlash;
            DmgColor.color = showingFlash ? FlashColor : originalColor;
            blinkTimer = BlinkInterval;
        }
    }

    void StopFlash()
    {
        isFlashing = false;
        DmgColor.color = originalColor;
        if (player != null) player.isDamaged = false;
        if (enemy != null) enemy.isDamaged = false;
    }

    void OnDisable()
    {
        if (isFlashing && DmgColor != null) StopFlash();
    }
}