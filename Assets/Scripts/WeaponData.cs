using UnityEngine;

// cosa fa un proiettile quando colpisce una piattaforma
// (per aggiungere un effetto: nuovo nome in fondo qui + un nuovo "case" in Platform.OnHit)
public enum PlatformEffect
{
    None,
    Activate,           // attiva il collider solido e cambia sprite
    Destroy,            // distrugge la piattaforma
    ExtendHorizontal,   // allunga la piattaforma in orizzontale
    Move                // fa partire il movimento avanti e indietro
}

// proprietà di un'arma: le imposta il WeaponPickup, le conserva l'inventario, le usa il proiettile
[System.Serializable]
public class WeaponData
{
    public Sprite bulletSprite;              // sprite fisso del proiettile
    public Sprite[] bulletFrames;            // frame dell'animazione (se ci sono, sostituiscono bulletSprite)
    public float bulletFrameRate = 12f;      // frame al secondo dell'animazione
    public Color bulletColor = Color.white;
    public int damage = 1;
    public PlatformEffect platformEffect = PlatformEffect.None;
}