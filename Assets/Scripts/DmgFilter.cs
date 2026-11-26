using UnityEngine;

// Fa lampeggiare uno sprite quando il bersaglio viene colpito.
// Funziona sia con il Player (PlayerController.isDamaged) sia con i nemici (EnemyHealth.isDamaged).
// Mettilo sul player o sul nemico: se lasci vuoti Victim e DmgColor li trova da solo.
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
    [Tooltip("Color used while flashing")]
    [SerializeField] Color FlashColor = Color.red;

    PlayerController player;
    EnemyHealth enemy;
    Color originalColor;
    bool isFlashing;
    float flashTimer;      // tempo rimasto al lampeggio
    float blinkTimer;      // tempo rimasto al prossimo cambio di colore
    bool showingFlash;

    void Start()
    {
        if (Victim == null)
            Victim = gameObject;

        player = Victim.GetComponent<PlayerController>();
        enemy = Victim.GetComponent<EnemyHealth>();

        if (player == null && enemy == null)
            Debug.LogWarning("DmgFilter: the Victim '" + Victim.name + "' has neither a PlayerController nor an EnemyHealth.");

        if (DmgColor == null)
            DmgColor = Victim.GetComponentInChildren<SpriteRenderer>();

        if (DmgColor == null)
        {
            Debug.LogError("DmgFilter: no SpriteRenderer found on '" + Victim.name + "'.");
            enabled = false;
            return;
        }

        originalColor = DmgColor.color;   // il colore a cui tornare (bianco se lo sprite non è colorato)
    }

    void Update()
    {
        // un colpo nuovo fa partire il lampeggio
        if (!isFlashing && WasDamaged())
            StartFlash();

        if (!isFlashing) return;

        // un passo per frame: niente cicli while dentro Update
        flashTimer -= Time.deltaTime;
        if (flashTimer <= 0f)
        {
            StopFlash();
            return;
        }

        blinkTimer -= Time.deltaTime;
        if (blinkTimer <= 0f)
        {
            showingFlash = !showingFlash;
            DmgColor.color = showingFlash ? FlashColor : originalColor;
            blinkTimer = BlinkInterval;
        }
    }

    bool WasDamaged()
    {
        return (player != null && player.isDamaged) || (enemy != null && enemy.isDamaged);
    }

    void StartFlash()
    {
        isFlashing = true;
        flashTimer = DmgTime;
        blinkTimer = 0f;          // il primo cambio di colore è immediato
        showingFlash = false;
    }

    void StopFlash()
    {
        isFlashing = false;
        DmgColor.color = originalColor;

        // fine del lampeggio: il bersaglio torna "non colpito"
        if (player != null) player.isDamaged = false;
        if (enemy != null) enemy.isDamaged = false;
    }

    // se lo script viene spento (es. il nemico muore) lo sprite non resta rosso
    void OnDisable()
    {
        if (isFlashing && DmgColor != null)
            StopFlash();
    }
}