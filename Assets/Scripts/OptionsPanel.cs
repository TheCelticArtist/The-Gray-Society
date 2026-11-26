using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class OptionsPanel : MonoBehaviour
{
    [System.Serializable]
    public class VolumeSlider
    {
        public Slider slider;
        [Tooltip("Name of the parameter exposed in the Audio Mixer, e.g. MusicVolume")]
        public string mixerParameter;
    }

    // Tasti rimappabili: nome mostrato, azione in InputSystem_Actions, parte composita (left/right) o vuoto.
    // Sono gli stessi comandi letti dal PlayerController. Un'azione che non esiste viene saltata.
    private static readonly (string label, string action, string part)[] KeyList =
    {
        ("Move Left", "Player/Move", "left"),
        ("Move Right", "Player/Move", "right"),
        ("Jump", "Player/Jump", ""),
        ("Shoot Up", "Player/ShootUp", ""),
        ("Shoot Down", "Player/ShootDown", ""),
        ("Shoot Left", "Player/ShootLeft", ""),
        ("Shoot Right", "Player/ShootRight", ""),
        ("Weapon 1", "Player/Weapon1", ""),
        ("Weapon 2", "Player/Weapon2", ""),
        ("Weapon 3", "Player/Weapon3", ""),
    };

    private class KeyRow
    {
        public InputAction action;
        public int bindingIndex;
        public TMP_Text keyLabel;
    }

    private const string GammaKey = "Options_Gamma";
    private const string VolumeKeyPrefix = "Options_Volume_";
    private const string BindingsKey = "Options_KeyBindings";

    [Header("Resolution")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;

    [Header("Gamma")]
    [SerializeField] private Slider gammaSlider;

    [Header("Volumes")]
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private VolumeSlider[] volumes;

    [Header("Input Keys Panel (under the Canvas, NOT inside OptionsPanel)")]
    [SerializeField] private Button inputKeysButton;
    [SerializeField] private GameObject inputKeysPanel;
    [SerializeField] private Button inputKeysBackButton;
    [SerializeField] private RectTransform keysContainer;
    [SerializeField] private Button resetKeysButton;
    [SerializeField] private Color keysTextColor = Color.white;

    private readonly List<Vector2Int> resolutions = new List<Vector2Int>();
    private InputActionAsset inputActions;
    private readonly List<KeyRow> keyRows = new List<KeyRow>();
    private InputActionRebindingExtensions.RebindingOperation rebind;
    private static Volume gammaVolume;
    private static LiftGammaGain gammaEffect;

    public void Init()
    {
        if (resolutionDropdown != null) SetupResolution();
        if (gammaSlider != null) SetupGamma();
        SetupVolumes();
        SetupInputKeysPanel();
        SetupKeys();
    }

    private void OnDisable()
    {
        PlayerPrefs.Save();
    }

    private void SetupResolution()
    {
        var labels = new List<string>();
        int current = 0;
        foreach (Resolution r in Screen.resolutions)
        {
            var size = new Vector2Int(r.width, r.height);
            if (resolutions.Contains(size))
                continue;

            if (size.x == Screen.width && size.y == Screen.height)
                current = resolutions.Count;
            resolutions.Add(size);
            labels.Add($"{size.x}x{size.y}");
        }

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(labels);
        resolutionDropdown.SetValueWithoutNotify(current);
        resolutionDropdown.RefreshShownValue();
        resolutionDropdown.interactable = resolutions.Count > 0;
        resolutionDropdown.onValueChanged.AddListener(SetResolution);
    }

    private void SetResolution(int index)
    {
        Vector2Int size = resolutions[index];
        Screen.SetResolution(size.x, size.y, Screen.fullScreenMode);
    }

    private void SetupGamma()
    {
        gammaSlider.minValue = -0.5f;
        gammaSlider.maxValue = 0.5f;
        gammaSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(GammaKey, 0f));
        gammaSlider.onValueChanged.AddListener(SetGamma);
        SetGamma(gammaSlider.value);
    }

    private void SetGamma(float value)
    {
        if (gammaVolume == null)
        {
            var volumeObject = new GameObject("Gamma Volume");
            DontDestroyOnLoad(volumeObject);
            gammaVolume = volumeObject.AddComponent<Volume>();
            gammaVolume.isGlobal = true;
            gammaVolume.priority = 100f;
            gammaVolume.sharedProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            gammaEffect = gammaVolume.sharedProfile.Add<LiftGammaGain>();
            gammaEffect.gamma.overrideState = true;
        }

        gammaEffect.gamma.value = new Vector4(1f, 1f, 1f, value);
        PlayerPrefs.SetFloat(GammaKey, value);
    }

    private void SetupVolumes()
    {
        if (mixer == null)
        {
            Debug.LogWarning("OptionsPanel: the Mixer is missing, volumes won't work.");
            return;
        }

        for (int i = 0; i < volumes.Length; i++)
        {
            VolumeSlider volume = volumes[i];
            if (volume.slider == null || string.IsNullOrEmpty(volume.mixerParameter))
            {
                Debug.LogWarning($"OptionsPanel: Volumes element {i} is incomplete (it needs a Slider and a Mixer Parameter). Skipping it.");
                continue;
            }

            volume.slider.minValue = 0.0001f;
            volume.slider.maxValue = 1f;
            volume.slider.SetValueWithoutNotify(AudioManager.GetSavedVolume(volume.mixerParameter));   // salvato, o il default dell'AudioManager
            volume.slider.onValueChanged.AddListener(value => SetVolume(volume.mixerParameter, value));   // salva solo quando il giocatore muove lo slider
            ApplyVolume(volume.mixerParameter, volume.slider.value);                                      // all'avvio applica senza salvare
        }
    }

    private void SetVolume(string parameter, float value)
    {
        ApplyVolume(parameter, value);
        PlayerPrefs.SetFloat(VolumeKeyPrefix + parameter, value);
    }

    private void ApplyVolume(string parameter, float value)
    {
        if (!mixer.SetFloat(parameter, Mathf.Log10(Mathf.Max(value, 0.0001f)) * 20f))
            Debug.LogWarning($"OptionsPanel: the parameter '{parameter}' is not exposed in the Audio Mixer.");
    }

    // ---------------- Pannello Input Keys ----------------

    private void SetupInputKeysPanel()
    {
        if (inputKeysPanel == null)
        {
            Debug.LogWarning("OptionsPanel: Input Keys Panel is missing.");
            return;
        }

        if (inputKeysPanel.transform.IsChildOf(transform))
            Debug.LogWarning("OptionsPanel: move Input Keys Panel out of OptionsPanel (directly under the Canvas).");

        inputKeysPanel.SetActive(false);
        if (inputKeysButton != null)
            inputKeysButton.onClick.AddListener(OpenInputKeys);
        if (inputKeysBackButton != null)
            inputKeysBackButton.onClick.AddListener(CloseInputKeys);
    }

    private void OpenInputKeys()
    {
        gameObject.SetActive(false);
        inputKeysPanel.SetActive(true);
    }

    private void CloseInputKeys()
    {
        rebind?.Cancel();
        PlayerPrefs.Save();
        inputKeysPanel.SetActive(false);
        gameObject.SetActive(true);
    }


    private void SetupKeys()
    {
        inputActions = InputSystem.actions;
        if (inputActions == null || keysContainer == null)
        {
            Debug.LogWarning("OptionsPanel: Keys Container and the Project-wide Actions are required " +
                             "(Edit > Project Settings > Input System Package).");
            return;
        }

        string saved = PlayerPrefs.GetString(BindingsKey, string.Empty);
        if (!string.IsNullOrEmpty(saved))
            inputActions.LoadBindingOverridesFromJson(saved);

        if (!keysContainer.TryGetComponent(out VerticalLayoutGroup layout))
            layout = keysContainer.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        foreach (var (label, actionName, part) in KeyList)
        {
            InputAction action = inputActions.FindAction(actionName);
            if (action == null)
            {
                Debug.LogWarning($"OptionsPanel: the action '{actionName}' does not exist in InputSystem_Actions.");
                continue;
            }

            var row = new KeyRow { action = action, bindingIndex = FindKeyboardBinding(action, part) };
            row.keyLabel = CreateKeyRow(label, () => StartRebind(row));
            keyRows.Add(row);
        }

        if (resetKeysButton != null)
            resetKeysButton.onClick.AddListener(ResetKeys);
        RefreshKeyLabels();
    }

    private static int FindKeyboardBinding(InputAction action, string compositePart)
    {
        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];
            bool keyboardOrMouse = binding.path.StartsWith("<Keyboard>") || binding.path.StartsWith("<Mouse>");
            if (!keyboardOrMouse)
                continue;

            bool match = string.IsNullOrEmpty(compositePart)
                ? !binding.isPartOfComposite
                : binding.isPartOfComposite && string.Equals(binding.name, compositePart, System.StringComparison.OrdinalIgnoreCase);
            if (match)
                return i;
        }

        Debug.LogWarning($"OptionsPanel: no keyboard key found for '{action.name}' {compositePart}.");
        return -1;
    }

    private void StartRebind(KeyRow row)
    {
        if (rebind != null || row.bindingIndex < 0)
            return;

        InputAction action = row.action;
        bool wasEnabled = action.enabled;
        action.Disable();
        row.keyLabel.text = "...";

        rebind = action.PerformInteractiveRebinding(row.bindingIndex)
            .WithControlsExcluding("<Mouse>/position")
            .WithControlsExcluding("<Mouse>/delta")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(_ => FinishRebind(action, wasEnabled))
            .OnCancel(_ => FinishRebind(action, wasEnabled))
            .Start();
    }

    private void FinishRebind(InputAction action, bool wasEnabled)
    {
        rebind.Dispose();
        rebind = null;
        if (wasEnabled)
            action.Enable();

        PlayerPrefs.SetString(BindingsKey, inputActions.SaveBindingOverridesAsJson());
        RefreshKeyLabels();
    }

    private void ResetKeys()
    {
        rebind?.Cancel();
        if (inputActions != null)
            inputActions.RemoveAllBindingOverrides();
        PlayerPrefs.DeleteKey(BindingsKey);
        RefreshKeyLabels();
    }

    private void RefreshKeyLabels()
    {
        foreach (KeyRow row in keyRows)
        {
            row.keyLabel.text = row.bindingIndex >= 0
                ? row.action.GetBindingDisplayString(row.bindingIndex)
                : "?";
        }
    }

    private TMP_Text CreateKeyRow(string label, UnityAction onRebind)
    {
        var row = new GameObject(label, typeof(RectTransform), typeof(LayoutElement));
        row.transform.SetParent(keysContainer, false);
        row.GetComponent<LayoutElement>().preferredHeight = 50f;

        CreateText(row.transform, label, 0f, 0.45f);
        TMP_Text keyLabel = CreateText(row.transform, "-", 0.45f, 0.7f);

        var buttonObject = new GameObject("Rebind", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(row.transform, false);
        SetHorizontalAnchors((RectTransform)buttonObject.transform, 0.72f, 1f);
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onRebind);
        ButtonSounds.Register(button);   // suono di click e hover anche sui pulsanti creati da codice
        CreateText(buttonObject.transform, "Rebind", 0f, 1f).color = Color.white;

        return keyLabel;
    }

    private TMP_Text CreateText(Transform parent, string text, float xMin, float xMax)
    {
        var textObject = new GameObject(text, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        SetHorizontalAnchors((RectTransform)textObject.transform, xMin, xMax);

        TextMeshProUGUI tmp = textObject.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 28f;
        tmp.color = keysTextColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void SetHorizontalAnchors(RectTransform rect, float xMin, float xMax)
    {
        rect.anchorMin = new Vector2(xMin, 0f);
        rect.anchorMax = new Vector2(xMax, 1f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}