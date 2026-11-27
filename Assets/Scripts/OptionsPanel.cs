using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.EventSystems;
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

    static readonly (string label, string action, string part)[] KeyList =
    {
        ("Move Left", "Player/Move", "left"),
        ("Move Right", "Player/Move", "right"),
        ("Jump", "Player/Jump", ""),
        ("Shoot Up", "Player/ShootUp", ""),
        ("Shoot Down", "Player/ShootDown", ""),
        ("Shoot Left", "Player/ShootLeft", ""),
        ("Shoot Right", "Player/ShootRight", ""),
        ("Shoot Diagonal (hold)", "Player/ShootDiagonal", ""),
        ("Weapon 1", "Player/Weapon1", ""),
        ("Weapon 2", "Player/Weapon2", ""),
        ("Weapon 3", "Player/Weapon3", ""),
        ("Weapon 4", "Player/Weapon4", ""),
        ("Weapon 5", "Player/Weapon5", ""),
        ("Pause", "Player/Pause", ""),
    };

    class KeyRow
    {
        public InputAction action;
        public int bindingIndex;
        public TMP_Text keyLabel;
    }

    const string GammaKey = "Options_Gamma";
    const string BindingsKey = "Options_KeyBindings";

    [Header("Resolution")]
    [SerializeField] TMP_Dropdown resolutionDropdown;

    [Header("Gamma")]
    [SerializeField] Slider gammaSlider;

    [Header("Volumes")]
    [SerializeField] AudioMixer mixer;
    [SerializeField] VolumeSlider[] volumes;

    [Header("Input Keys Panel (under the Canvas, NOT inside OptionsPanel)")]
    [SerializeField] Button inputKeysButton;
    [SerializeField] GameObject inputKeysPanel;
    [SerializeField] Button inputKeysBackButton;
    [SerializeField] RectTransform keysContainer;
    [SerializeField] Button resetKeysButton;
    [SerializeField] Color keysTextColor = Color.white;

    readonly List<Vector2Int> resolutions = new List<Vector2Int>();
    readonly List<KeyRow> keyRows = new List<KeyRow>();
    InputActionAsset inputActions;
    InputActionRebindingExtensions.RebindingOperation rebind;
    static Volume gammaVolume;
    static LiftGammaGain gammaEffect;

    public void Init()
    {
        if (resolutionDropdown != null) SetupResolution();
        if (gammaSlider != null) SetupGamma();
        SetupVolumes();
        SetupInputKeysPanel();
        SetupKeys();
    }

    void OnDisable() => PlayerPrefs.Save();

    void SetupResolution()
    {
        var labels = new List<string>();
        int current = 0;
        foreach (Resolution r in Screen.resolutions)
        {
            var size = new Vector2Int(r.width, r.height);
            if (resolutions.Contains(size)) continue;
            if (size.x == Screen.width && size.y == Screen.height) current = resolutions.Count;
            resolutions.Add(size);
            labels.Add($"{size.x}x{size.y}");
        }

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(labels);
        resolutionDropdown.SetValueWithoutNotify(current);
        resolutionDropdown.RefreshShownValue();
        resolutionDropdown.interactable = resolutions.Count > 0;
        resolutionDropdown.onValueChanged.AddListener(i => Screen.SetResolution(resolutions[i].x, resolutions[i].y, Screen.fullScreenMode));
    }

    void SetupGamma()
    {
        gammaSlider.minValue = -0.5f;
        gammaSlider.maxValue = 0.5f;
        gammaSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(GammaKey, 0f));
        gammaSlider.onValueChanged.AddListener(SetGamma);
        SetGamma(gammaSlider.value);
    }

    void SetGamma(float value)
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

    void SetupVolumes()
    {
        if (mixer == null) return;

        foreach (VolumeSlider volume in volumes)
        {
            if (volume.slider == null || string.IsNullOrEmpty(volume.mixerParameter)) continue;

            string parameter = volume.mixerParameter;
            volume.slider.minValue = 0.0001f;
            volume.slider.maxValue = 1f;
            volume.slider.SetValueWithoutNotify(AudioManager.GetSavedVolume(parameter));

            volume.slider.onValueChanged.AddListener(value =>
            {
                AudioManager.SetMixerVolume(mixer, parameter, value);
                PlayerPrefs.SetFloat(AudioManager.VolumeKeyPrefix + parameter, value);
            });
            AudioManager.SetMixerVolume(mixer, parameter, volume.slider.value);
        }
    }

    void SetupInputKeysPanel()
    {
        if (inputKeysPanel == null) return;

        inputKeysPanel.SetActive(false);
        if (inputKeysButton != null)
            inputKeysButton.onClick.AddListener(() => { gameObject.SetActive(false); inputKeysPanel.SetActive(true); });
        if (inputKeysBackButton != null)
            inputKeysBackButton.onClick.AddListener(CloseInputKeys);
    }

    void CloseInputKeys()
    {
        rebind?.Cancel();
        PlayerPrefs.Save();
        inputKeysPanel.SetActive(false);
        gameObject.SetActive(true);
    }

    void SetupKeys()
    {
        inputActions = InputSystem.actions;
        if (inputActions == null || keysContainer == null) return;

        string saved = PlayerPrefs.GetString(BindingsKey, string.Empty);
        if (!string.IsNullOrEmpty(saved)) inputActions.LoadBindingOverridesFromJson(saved);

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
            if (action == null) continue;

            var row = new KeyRow { action = action, bindingIndex = FindKeyboardBinding(action, part) };
            row.keyLabel = CreateKeyRow(label, () => StartRebind(row));
            keyRows.Add(row);
        }

        if (resetKeysButton != null) resetKeysButton.onClick.AddListener(ResetKeys);
        RefreshKeyLabels();
    }

    public static int FindKeyboardBinding(InputAction action, string compositePart)
    {
        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];
            if (!binding.path.StartsWith("<Keyboard>") && !binding.path.StartsWith("<Mouse>")) continue;

            bool match = string.IsNullOrEmpty(compositePart)
                ? !binding.isPartOfComposite
                : binding.isPartOfComposite && string.Equals(binding.name, compositePart, System.StringComparison.OrdinalIgnoreCase);
            if (match) return i;
        }
        return -1;
    }

    void StartRebind(KeyRow row)
    {
        if (rebind != null || row.bindingIndex < 0) return;

        InputAction action = row.action;
        bool wasEnabled = action.enabled;
        action.Disable();
        row.keyLabel.text = "...";
        SetMenuNavigation(false);
        
        rebind = action.PerformInteractiveRebinding(row.bindingIndex)
            .WithControlsHavingToMatchPath("<Keyboard>")
            .WithControlsHavingToMatchPath("<Mouse>")
            .WithControlsExcluding("<Mouse>/position")
            .WithControlsExcluding("<Mouse>/delta")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(_ => FinishRebind(action, wasEnabled))
            .OnCancel(_ => FinishRebind(action, wasEnabled))
            .Start();
    }

    void FinishRebind(InputAction action, bool wasEnabled)
    {
        rebind.Dispose();
        rebind = null;
        if (wasEnabled) action.Enable();
        SetMenuNavigation(true);

        PlayerPrefs.SetString(BindingsKey, inputActions.SaveBindingOverridesAsJson());
        RefreshKeyLabels();
    }

    static void SetMenuNavigation(bool active)
    {
        if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = active;
    }

    void ResetKeys()
    {
        rebind?.Cancel();
        if (inputActions != null) inputActions.RemoveAllBindingOverrides();
        PlayerPrefs.DeleteKey(BindingsKey);
        RefreshKeyLabels();
    }

    void RefreshKeyLabels()
    {
        foreach (KeyRow row in keyRows)
            row.keyLabel.text = row.bindingIndex >= 0 ? row.action.GetBindingDisplayString(row.bindingIndex) : "?";
    }

    TMP_Text CreateKeyRow(string label, UnityAction onRebind)
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
        ButtonSounds.Register(button);
        CreateText(buttonObject.transform, "Rebind", 0f, 1f).color = Color.white;

        return keyLabel;
    }

    TMP_Text CreateText(Transform parent, string text, float xMin, float xMax)
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

    static void SetHorizontalAnchors(RectTransform rect, float xMin, float xMax)
    {
        rect.anchorMin = new Vector2(xMin, 0f);
        rect.anchorMax = new Vector2(xMax, 1f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}