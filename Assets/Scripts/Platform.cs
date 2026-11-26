using System.Collections;
using UnityEngine;

// Piattaforma che reagisce ai proiettili: ogni arma ha il suo effetto (vedi PlatformEffect in WeaponData.cs).
// Servono due collider: uno TRIGGER sempre attivo (lo colpiscono i proiettili)
// e uno SOLIDO disattivato all'inizio (quello su cui il player cammina una volta attivato).
// Gli effetti si sommano, tranne Allunga e Movimento che si escludono: vale il primo che arriva.
// Lo sprite è sempre quello dell'ultimo effetto ricevuto.
public class Platform : MonoBehaviour
{
    public enum ExtendSide { Both, Left, Right }

    [Tooltip("The solid collider. If empty, the BoxCollider2D without 'Is Trigger' is used")]
    [SerializeField] BoxCollider2D solidCollider;
    [Tooltip("If enabled, the platform starts solid (useful for platforms that only move, extend or get destroyed)")]
    [SerializeField] bool startSolid = false;

    [Header("Reacts To")]
    [SerializeField] bool canBeActivated = true;
    [SerializeField] bool canBeDestroyed = true;
    [Tooltip("Extend and Move can't both happen: the first one that hits the platform wins")]
    [SerializeField] bool canBeExtended = true;
    [Tooltip("Extend and Move can't both happen: the first one that hits the platform wins")]
    [SerializeField] bool canBeMoved = true;

    [Header("Activate")]
    [Tooltip("Sprite shown after the Activate effect (empty = keep the current sprite)")]
    [SerializeField] Sprite activeSprite;

    [Header("Extend Horizontal")]
    [Tooltip("Sprite shown after the Extend effect (empty = keep the current sprite)")]
    [SerializeField] Sprite extendedSprite;
    [Tooltip("Width multiplier (2 = twice as wide)")]
    [SerializeField, Min(1f)] float extendMultiplier = 2f;
    [Tooltip("Which side grows: both, only left or only right")]
    [SerializeField] ExtendSide extendSide = ExtendSide.Both;
    [Tooltip("Seconds the extension takes (0 = instant)")]
    [SerializeField, Min(0f)] float extendDuration = 0.3f;

    [Header("Move")]
    [Tooltip("Sprite shown after the Move effect (empty = keep the current sprite)")]
    [SerializeField] Sprite movingSprite;
    [Tooltip("How far the platform travels from its starting point, e.g. X 4 = 4 units to the right, Y 3 = 3 units up")]
    [SerializeField] Vector2 moveOffset = new Vector2(3f, 0f);
    [Tooltip("Seconds to go from the start point to the end point (then it comes back)")]
    [SerializeField, Min(0.1f)] float moveDuration = 2f;

    SpriteRenderer spriteRenderer;
    bool isActive;
    bool isExtended;

    // valori iniziali per l'allungamento
    Vector3 baseScale;
    Vector2 baseSpriteSize;
    BoxCollider2D[] boxes;
    Vector2[] baseBoxSizes;
    Vector2[] baseBoxOffsets;

    // movimento
    Rigidbody2D body;
    Vector2 moveOrigin;
    Vector2 lastPosition;
    float moveTimer;

    public bool IsMoving { get; private set; }
    public Vector2 Velocity { get; private set; }   // la legge il PlayerController per farsi trasportare

    void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        // cerca da solo il collider solido (quello senza Is Trigger)
        if (solidCollider == null)
        {
            foreach (BoxCollider2D box in GetComponents<BoxCollider2D>())
            {
                if (!box.isTrigger)
                {
                    solidCollider = box;
                    break;
                }
            }
        }

        if (solidCollider == null)
            Debug.LogError("Platform '" + name + "': the solid BoxCollider2D (without Is Trigger) is missing!");
        else if (!startSolid)
            solidCollider.enabled = false;   // all'inizio la piattaforma non si può calpestare
        else
            isActive = true;

        // misure iniziali, servono per l'allungamento
        baseScale = transform.localScale;
        if (spriteRenderer != null)
            baseSpriteSize = spriteRenderer.size;

        boxes = GetComponents<BoxCollider2D>();
        baseBoxSizes = new Vector2[boxes.Length];
        baseBoxOffsets = new Vector2[boxes.Length];
        for (int i = 0; i < boxes.Length; i++)
        {
            baseBoxSizes[i] = boxes[i].size;
            baseBoxOffsets[i] = boxes[i].offset;
        }
    }

    // chiamata dal proiettile: ogni arma ha il suo effetto
    public void OnHit(PlatformEffect effect)
    {
        switch (effect)
        {
            case PlatformEffect.Activate:
                if (canBeActivated) Activate();
                break;

            case PlatformEffect.Destroy:
                if (canBeDestroyed) Destroy(gameObject);
                break;

            case PlatformEffect.ExtendHorizontal:
                // non si allunga se è già allungata o se si sta già muovendo
                if (canBeExtended && !isExtended && !IsMoving)
                {
                    SetSprite(extendedSprite);
                    StartCoroutine(Extend());
                }
                break;

            case PlatformEffect.Move:
                // non si muove se è già in movimento o se è stata allungata
                if (canBeMoved && !IsMoving && !isExtended)
                {
                    SetSprite(movingSprite);
                    StartMoving();
                }
                break;

            case PlatformEffect.None:
            default:
                break;
        }
    }

    // lo sprite è quello dell'ultimo effetto ricevuto; se l'effetto non ne ha uno, resta quello di prima
    void SetSprite(Sprite sprite)
    {
        if (sprite != null && spriteRenderer != null)
            spriteRenderer.sprite = sprite;
    }

    // ---------------- Attiva ----------------

    void Activate()
    {
        if (isActive || solidCollider == null) return;

        isActive = true;
        solidCollider.enabled = true;
        SetSprite(activeSprite);
    }

    // ---------------- Allunga in orizzontale ----------------

    IEnumerator Extend()
    {
        isExtended = true;

        float t = 0f;
        while (t < extendDuration)
        {
            t += Time.deltaTime;
            SetWidthFactor(Mathf.Lerp(1f, extendMultiplier, t / extendDuration));
            yield return null;
        }

        SetWidthFactor(extendMultiplier);
    }

    void SetWidthFactor(float factor)
    {
        if (spriteRenderer == null) return;

        float leftEdge = spriteRenderer.bounds.min.x;
        float rightEdge = spriteRenderer.bounds.max.x;

        if (spriteRenderer.drawMode != SpriteDrawMode.Simple)
        {
            // sprite in modalità Tiled/Sliced: si allunga ripetendo lo sprite, senza stirare i pixel
            spriteRenderer.size = new Vector2(baseSpriteSize.x * factor, baseSpriteSize.y);

            for (int i = 0; i < boxes.Length; i++)
            {
                if (boxes[i].autoTiling) continue;   // con Auto Tiling il collider segue da solo lo sprite
                boxes[i].size = new Vector2(baseBoxSizes[i].x * factor, baseBoxSizes[i].y);
                boxes[i].offset = new Vector2(baseBoxOffsets[i].x * factor, baseBoxOffsets[i].y);
            }
        }
        else
        {
            // sprite normale: si allunga la scala (i collider la seguono da soli)
            transform.localScale = new Vector3(baseScale.x * factor, baseScale.y, baseScale.z);
        }

        // tiene fermo il bordo opposto al lato che si allunga
        Bounds bounds = spriteRenderer.bounds;
        float shift = 0f;
        if (extendSide == ExtendSide.Right) shift = leftEdge - bounds.min.x;
        else if (extendSide == ExtendSide.Left) shift = rightEdge - bounds.max.x;

        if (shift != 0f)
            transform.position += new Vector3(shift, 0f, 0f);
    }

    // ---------------- Movimento ----------------

    void StartMoving()
    {
        // un Rigidbody2D Kinematic serve per spostare bene la piattaforma e trasportare il player
        if (!TryGetComponent(out body))
            body = gameObject.AddComponent<Rigidbody2D>();

        body.bodyType = RigidbodyType2D.Kinematic;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        moveOrigin = body.position;
        lastPosition = body.position;
        moveTimer = 0f;
        IsMoving = true;
    }

    void FixedUpdate()
    {
        if (!IsMoving) return;

        // avanti e indietro tra il punto di partenza e quello di arrivo, rallentando alle estremità
        moveTimer += Time.fixedDeltaTime;
        float t = Mathf.SmoothStep(0f, 1f, Mathf.PingPong(moveTimer / moveDuration, 1f));
        Vector2 target = moveOrigin + moveOffset * t;

        body.MovePosition(target);
        Velocity = (target - lastPosition) / Time.fixedDeltaTime;
        lastPosition = target;
    }

    // Scene view: linea gialla dal punto di partenza a quello di arrivo del movimento
    void OnDrawGizmosSelected()
    {
        if (!canBeMoved || moveOffset == Vector2.zero) return;

        Vector3 start = Application.isPlaying && IsMoving ? (Vector3)moveOrigin : transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(start, start + (Vector3)moveOffset);
        Gizmos.DrawWireSphere(start + (Vector3)moveOffset, 0.2f);
    }
}