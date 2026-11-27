using UnityEngine;

public enum PlatformEffect { None, Activate, Destroy, ExtendHorizontal, Move }

[System.Serializable]
public class WeaponData
{
    public Sprite bulletSprite;
    public Sprite[] bulletFrames;
    public float bulletFrameRate = 12f;
    public Color bulletColor = Color.white;
    public int damage = 1;
    public PlatformEffect platformEffect = PlatformEffect.None;
}