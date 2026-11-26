using UnityEngine;

// Piattaforma che reagisce ai proiettili.
// Servono due collider: uno TRIGGER sempre attivo (lo colpiscono i proiettili)
// e uno SOLIDO disattivato all'inizio (quello su cui il player cammina una volta attivato).
public class Platform : MonoBehaviour
{
    [Tooltip("The solid collider. If empty, the BoxCollider2D without 'Is Trigger' is used")]
    [SerializeField] BoxCollider2D solidCollider;

    [Tooltip("Sprite of the platform once it is activated")]
    [SerializeField] Sprite activeSprite;

    SpriteRenderer spriteRenderer;
    bool isActive;

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
        {
            Debug.LogError("Platform '" + name + "': the solid BoxCollider2D (without Is Trigger) is missing!");
            return;
        }

        solidCollider.enabled = false;   // all'inizio la piattaforma non si può calpestare
    }

    // chiamata dal proiettile: ogni arma ha il suo effetto
    public void OnHit(PlatformEffect effect)
    {
        switch (effect)
        {
            case PlatformEffect.Activate:
                Activate();
                break;

            case PlatformEffect.None:
            default:
                break;
        }
    }

    void Activate()
    {
        if (isActive || solidCollider == null) return;

        isActive = true;
        solidCollider.enabled = true;

        if (activeSprite != null && spriteRenderer != null)
            spriteRenderer.sprite = activeSprite;
    }
}