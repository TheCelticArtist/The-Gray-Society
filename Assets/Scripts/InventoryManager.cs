using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class InventoryManager : MonoBehaviour
{
    // Accessibile da qualsiasi script: InventoryManager.Instance
    public static InventoryManager Instance { get; private set; }

    [Header("Weapon Slots (Element 0 = slot 1 = level 1 weapon, Element 1 = slot 2, ...)")]
    [Tooltip("The weapon icon inside each slot (hidden until the weapon is picked up)")]
    [SerializeField] Image[] weaponSlots;

    [Header("Equipped Weapon Highlight")]
    [Tooltip("Frame color of the slot in use")]
    [SerializeField] Color selectedSlotColor = Color.yellow;
    [Tooltip("Frame color of the other slots")]
    [SerializeField] Color normalSlotColor = Color.white;

    [Header("Reset")]
    [Tooltip("Build index of the main menu: loading it clears the inventory")]
    [SerializeField] int menuSceneIndex = 0;

    bool[] collected;                           // quali armi sono state raccolte
    WeaponData[] weapons;                       // proprietà di ogni arma raccolta (proiettile, danno, effetto)
    Image[] slotFrames;                         // cornici degli slot (i genitori delle icone)
    int currentSlot = -1;                       // arma equipaggiata (-1 = nessuna)
    Canvas canvas;

    // true se hai un'arma equipaggiata (lo usa il PlayerController per sparare)
    public bool HasWeapon => currentSlot >= 0;

    // proprietà dell'arma equipaggiata (null se non ne hai)
    public WeaponData CurrentWeapon => currentSlot >= 0 ? weapons[currentSlot] : null;

    void Awake()
    {
        // se esiste già un inventario (portato dal livello precedente), questo doppione si elimina
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);           // il canvas sopravvive ai cambi di scena
        SceneManager.sceneLoaded += OnSceneLoaded;

        canvas = GetComponent<Canvas>();
        AssignCamera();

        collected = new bool[weaponSlots.Length];
        weapons = new WeaponData[weaponSlots.Length];
        slotFrames = new Image[weaponSlots.Length];

        // tutti gli slot sono già pronti, ma le icone restano nascoste finché non raccogli l'arma
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            if (weaponSlots[i] == null)
            {
                Debug.LogError("InventoryManager: Weapon Slots element " + i + " is empty!");
                continue;
            }

            weaponSlots[i].enabled = false;

            // la cornice è l'oggetto genitore dell'icona (Slot1, Slot2, ...)
            Transform parent = weaponSlots[i].transform.parent;
            if (parent != null && parent.TryGetComponent(out Image frame))
                slotFrames[i] = frame;
        }

        UpdateHighlight();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // tornando al menu principale l'inventario sparisce, così una nuova partita riparte da zero
        if (scene.buildIndex == menuSceneIndex)
        {
            Destroy(gameObject);
            return;
        }

        // ogni livello ha la sua Main Camera: il canvas si ricollega a quella nuova
        AssignCamera();
    }

    void AssignCamera()
    {
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera)
            canvas.worldCamera = Camera.main;
    }

    // slotNumber parte da 1: 1 = primo slot, 2 = secondo slot, ...
    public bool HasWeaponInSlot(int slotNumber)
    {
        int i = slotNumber - 1;
        return collected != null && i >= 0 && i < collected.Length && collected[i];
    }

    public void AddWeapon(int slotNumber, Sprite icon, WeaponData weapon)
    {
        int i = slotNumber - 1;

        if (i < 0 || i >= weaponSlots.Length)
        {
            Debug.LogError("InventoryManager: slot " + slotNumber + " does not exist! Available slots: " + weaponSlots.Length);
            return;
        }

        collected[i] = true;
        weapons[i] = weapon;

        Image slot = weaponSlots[i];
        if (slot != null)
        {
            if (icon != null)
                slot.sprite = icon;

            slot.gameObject.SetActive(true);        // nel caso l'oggetto icona fosse disattivato nella Hierarchy
            slot.transform.SetAsLastSibling();      // disegnata sopra la cornice dello slot
            slot.color = Color.white;               // nel caso l'alpha fosse a 0
            slot.enabled = true;
        }

        // l'arma appena raccolta viene equipaggiata subito
        currentSlot = i;
        UpdateHighlight();
    }

    // equipaggia l'arma di uno slot (solo se è già stata raccolta)
    public void SelectSlot(int slotNumber)
    {
        int i = slotNumber - 1;

        if (!HasWeaponInSlot(slotNumber) || i == currentSlot) return;

        currentSlot = i;
        UpdateHighlight();
    }

    void UpdateHighlight()
    {
        for (int i = 0; i < slotFrames.Length; i++)
        {
            if (slotFrames[i] != null)
                slotFrames[i].color = i == currentSlot ? selectedSlotColor : normalSlotColor;
        }
    }
}