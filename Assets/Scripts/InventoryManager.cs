using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("Weapon Slots (Element 0 = slot 1 = level 1 weapon, Element 1 = slot 2, ...)")]
    [Tooltip("The weapon icon inside each slot (hidden until the weapon is picked up)")]
    [SerializeField] Image[] weaponSlots;

    [Header("Equipped Weapon Highlight")]
    [SerializeField] Color selectedSlotColor = Color.yellow;
    [SerializeField] Color normalSlotColor = Color.white;

    [Header("Reset")]
    [Tooltip("Build index of the main menu: loading it clears the inventory")]
    [SerializeField] int menuSceneIndex = 0;

    WeaponData[] weapons;
    Image[] slotFrames;
    int currentSlot = -1;
    Canvas canvas;

    public bool HasWeapon => currentSlot >= 0;
    public WeaponData CurrentWeapon => HasWeapon ? weapons[currentSlot] : null;

    public int WeaponCount
    {
        get
        {
            int count = 0;
            if (weapons != null)
                foreach (WeaponData weapon in weapons)
                    if (weapon != null) count++;
            return count;
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;

        canvas = GetComponent<Canvas>();
        AssignCamera();

        weapons = new WeaponData[weaponSlots.Length];
        slotFrames = new Image[weaponSlots.Length];
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            if (weaponSlots[i] == null) continue;
            weaponSlots[i].enabled = false;
            Transform frame = weaponSlots[i].transform.parent;
            if (frame != null) frame.TryGetComponent(out slotFrames[i]);
        }
        UpdateHighlight();
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.buildIndex == menuSceneIndex) Destroy(gameObject);
        else AssignCamera();
    }

    void AssignCamera()
    {
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera)
            canvas.worldCamera = Camera.main;
    }

    public bool HasWeaponInSlot(int slotNumber)
    {
        int i = slotNumber - 1;
        return weapons != null && i >= 0 && i < weapons.Length && weapons[i] != null;
    }

    public void AddWeapon(int slotNumber, Sprite icon, WeaponData weapon)
    {
        int i = slotNumber - 1;
        if (i < 0 || i >= weaponSlots.Length) return;
        weapons[i] = weapon;

        Image slot = weaponSlots[i];
        if (slot != null)
        {
            if (icon != null) slot.sprite = icon;
            
            slot.gameObject.SetActive(true);
            slot.transform.SetAsLastSibling();
            slot.color = Color.white;
            slot.enabled = true;
        }

        currentSlot = i;
        UpdateHighlight();
    }

    public void SelectSlot(int slotNumber)
    {
        if (!HasWeaponInSlot(slotNumber) || slotNumber - 1 == currentSlot) return;
        currentSlot = slotNumber - 1;
        UpdateHighlight();
    }

    void UpdateHighlight()
    {
        for (int i = 0; i < slotFrames.Length; i++)
            if (slotFrames[i] != null)
                slotFrames[i].color = i == currentSlot ? selectedSlotColor : normalSlotColor;
    }

    public void ResetForLevel(int levelBuildIndex)
    {
        for (int i = Mathf.Max(0, levelBuildIndex - 1); i < weapons.Length; i++)
        {
            weapons[i] = null;

            Image slot = weaponSlots[i];
            if (slot != null) slot.enabled = false;
        }
        
        currentSlot = -1;
        for (int i = weapons.Length - 1; i >= 0; i--)
            if (weapons[i] != null) { currentSlot = i; break; }

        UpdateHighlight();
    }
}