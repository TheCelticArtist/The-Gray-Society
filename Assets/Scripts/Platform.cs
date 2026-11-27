using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Platform : MonoBehaviour
{
    public enum ExtendSide { Both, Left, Right }

    const float ExtendGap = 0.01f;

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
    [Tooltip("Width multiplier (2 = twice as wide). The platform stops earlier if another platform is in the way")]
    [SerializeField, Min(1f)] float extendMultiplier = 2f;
    [Tooltip("Which side grows: both, only left or only right")]
    [SerializeField] ExtendSide extendSide = ExtendSide.Both;
    [Tooltip("Seconds the extension takes (0 = instant)")]
    [SerializeField, Min(0f)] float extendDuration = 0.3f;
    [Tooltip("Besides other platforms, the extension also stops at solid colliders on these layers (e.g. level ground or walls). Nothing = only other platforms")]
    [SerializeField] LayerMask extraBlockingLayers;

    [Header("Move")]
    [Tooltip("Sprite shown after the Move effect (empty = keep the current sprite)")]
    [SerializeField] Sprite movingSprite;
    [Tooltip("How far the platform travels from its starting point, e.g. X 4 = 4 units to the right, Y 3 = 3 units up")]
    [SerializeField] Vector2 moveOffset = new Vector2(3f, 0f);
    [Tooltip("Seconds to go from the start point to the end point (then it comes back)")]
    [SerializeField, Min(0.1f)] float moveDuration = 2f;

    SpriteRenderer spriteRenderer;
    bool isActive, isExtended;

    BoxCollider2D[] boxes;
    Vector2[] baseBoxSizes, baseBoxOffsets;
    Vector3 baseScale;
    Vector2 baseSpriteSize;
    readonly List<RaycastHit2D> castHits = new List<RaycastHit2D>();

    // movimento
    Rigidbody2D body;
    Vector2 moveOrigin, lastPosition;
    float moveTimer;

    public bool IsMoving { get; private set; }
    public Vector2 Velocity { get; private set; }

    void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        boxes = GetComponents<BoxCollider2D>();
        baseBoxSizes = System.Array.ConvertAll(boxes, box => box.size);
        baseBoxOffsets = System.Array.ConvertAll(boxes, box => box.offset);
        baseScale = transform.localScale;
        if (spriteRenderer != null) baseSpriteSize = spriteRenderer.size;

        if (solidCollider == null) solidCollider = System.Array.Find(boxes, box => !box.isTrigger);
        if (solidCollider != null && !startSolid) solidCollider.enabled = false;
        isActive = startSolid;
    }

    public void OnHit(PlatformEffect effect)
    {
        switch (effect)
        {
            case PlatformEffect.Activate:
                if (canBeActivated && !isActive && solidCollider != null)
                {
                    isActive = true;
                    solidCollider.enabled = true;
                    SetSprite(activeSprite);
                }
                break;

            case PlatformEffect.Destroy:
                if (canBeDestroyed) Destroy(gameObject);
                break;

            case PlatformEffect.ExtendHorizontal:
                if (canBeExtended && !isExtended && !IsMoving) TryExtend();
                break;

            case PlatformEffect.Move:
                if (canBeMoved && !IsMoving && !isExtended)
                {
                    SetSprite(movingSprite);
                    StartMoving();
                }
                break;
        }
    }

    void SetSprite(Sprite sprite)
    {
        if (sprite != null && spriteRenderer != null) spriteRenderer.sprite = sprite;
    }

    void WantedGrowth(float width, out float left, out float right)
    {
        float extra = width * (extendMultiplier - 1f);
        left = extendSide == ExtendSide.Right ? 0f : extendSide == ExtendSide.Both ? extra * 0.5f : extra;
        right = extendSide == ExtendSide.Left ? 0f : extendSide == ExtendSide.Both ? extra * 0.5f : extra;
    }

    void TryExtend()
    {
        if (spriteRenderer == null) return;

        Bounds start = spriteRenderer.bounds;
        WantedGrowth(start.size.x, out float wantLeft, out float wantRight);

        float left = FreeSpace(start, Vector2.left, wantLeft);
        float right = FreeSpace(start, Vector2.right, wantRight);
        if (left + right < ExtendGap) return;

        isExtended = true;
        SetSprite(extendedSprite);
        StartCoroutine(Extend(start.min.x, start.size.x, left, right));
    }

    float FreeSpace(Bounds bounds, Vector2 direction, float wanted)
    {
        if (wanted <= 0f) return 0f;

        Vector2 size = new Vector2(bounds.size.x, bounds.size.y * 0.9f);
        Physics2D.BoxCast(bounds.center, size, 0f, direction, new ContactFilter2D().NoFilter(), castHits, wanted);

        float free = wanted;
        foreach (RaycastHit2D hit in castHits)
            if (IsBlocking(hit.collider)) free = Mathf.Min(free, Mathf.Max(0f, hit.distance - ExtendGap));
        return free;
    }

    bool IsBlocking(Collider2D other)
    {
        Platform platform = other.GetComponentInParent<Platform>();
        if (platform != null) return platform != this;

        return !other.isTrigger && (extraBlockingLayers.value & (1 << other.gameObject.layer)) != 0;
    }

    IEnumerator Extend(float startLeft, float startWidth, float left, float right)
    {
        float t = 0f;
        while (t < extendDuration)
        {
            t += Time.deltaTime;
            ApplyExtension(startLeft, startWidth, left, right, Mathf.Clamp01(t / extendDuration));
            yield return null;
        }
        ApplyExtension(startLeft, startWidth, left, right, 1f);
    }

    void ApplyExtension(float startLeft, float startWidth, float left, float right, float progress)
    {
        float factor = (startWidth + (left + right) * progress) / startWidth;

        if (spriteRenderer.drawMode != SpriteDrawMode.Simple)
        {
            spriteRenderer.size = new Vector2(baseSpriteSize.x * factor, baseSpriteSize.y);
            for (int i = 0; i < boxes.Length; i++)
            {
                if (boxes[i].autoTiling) continue;
                boxes[i].size = new Vector2(baseBoxSizes[i].x * factor, baseBoxSizes[i].y);
                boxes[i].offset = new Vector2(baseBoxOffsets[i].x * factor, baseBoxOffsets[i].y);
            }
        }
        else
        {
            transform.localScale = new Vector3(baseScale.x * factor, baseScale.y, baseScale.z);
        }

        float shift = (startLeft - left * progress) - spriteRenderer.bounds.min.x;
        if (shift != 0f) transform.position += new Vector3(shift, 0f, 0f);
    }

    void StartMoving()
    {
        if (!TryGetComponent(out body)) body = gameObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        moveOrigin = lastPosition = body.position;
        moveTimer = 0f;
        IsMoving = true;
    }

    void FixedUpdate()
    {
        if (!IsMoving) return;

        moveTimer += Time.fixedDeltaTime;
        float t = Mathf.SmoothStep(0f, 1f, Mathf.PingPong(moveTimer / moveDuration, 1f));
        Vector2 target = moveOrigin + moveOffset * t;

        body.MovePosition(target);
        Velocity = (target - lastPosition) / Time.fixedDeltaTime;
        lastPosition = target;
    }

    void OnDrawGizmosSelected()
    {
        SpriteRenderer sr = spriteRenderer != null ? spriteRenderer : GetComponentInChildren<SpriteRenderer>();
        if (canBeExtended && !isExtended && sr != null && extendMultiplier > 1f)
        {
            Bounds b = sr.bounds;
            WantedGrowth(b.size.x, out float left, out float right);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(new Vector3(b.center.x + (right - left) * 0.5f, b.center.y, b.center.z),
                                new Vector3(b.size.x + left + right, b.size.y, 0f));
        }

        if (canBeMoved && moveOffset != Vector2.zero)
        {
            Vector3 start = IsMoving ? (Vector3)moveOrigin : transform.position;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(start, start + (Vector3)moveOffset);
            Gizmos.DrawWireSphere(start + (Vector3)moveOffset, 0.2f);
        }
    }
}