using UnityEngine;

// cosa fa un proiettile quando colpisce una piattaforma
// (per aggiungere un effetto: nuovo nome qui + un nuovo "case" in Platform.OnHit)
public enum PlatformEffect
{
    None,
    Activate    // attiva il collider solido e cambia sprite
}

// proprietà di un'arma: le imposta il WeaponPickup, le conserva l'inventario, le usa il proiettile
[System.Serializable]
public class WeaponData
{
    public Sprite bulletSprite;
    public Color bulletColor = Color.white;
    public int damage = 1;
    public PlatformEffect platformEffect = PlatformEffect.None;
}