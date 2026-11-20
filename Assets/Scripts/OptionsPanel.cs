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
        [Tooltip("Nome del parametro esposto nell'Audio Mixer, es. MusicVolume")]
        public string mixerParameter;
    }

    // Tasti rimappabili: nome mostrato, azione in InputSystem_Actions, parte composita (left/right) o vuoto.
    private static readonly (string label, string action, string part)[] KeyList =
    {
        ("Move Left", "Player/Move", "left"),
        ("Move Right", "Player/Move", "right"),
        ("Jump", "Player/Jump", ""),
        ("Interact", "Player/Interact", ""),
        ("Attack", "Player/Attack", ""),
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

    [Header("Risoluzione")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;

    [Header("Gamma")]
    [SerializeField] private Slider gammaSlider;

    [Header("Volumi")]
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private VolumeSlider[] volumes;

    [Header("Pannello Input Keys (sotto il Canvas, NON dentro OptionsPanel)")]
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
            Debug.LogWarning("OptionsPanel: manca il Mixer, i volumi non funzionano.");
            return;
        }

        for (int i = 0; i < volumes.Length; i++)
        {
            VolumeSlider volume = volumes[i];
            if (volume.slider == null || string.IsNullOrEmpty(volume.mixerParameter))
            {
                Debug.LogWarning($"OptionsPanel: Volumes, elemento {i} incompleto (servono Slider e Mixer Parameter). Lo salto.");
                continue;
            }

            volume.slider.minValue = 0.0001f;
            volume.slider.maxValue = 1f;
            volume.slider.SetValueWithoutNotify(PlayerPrefs.GetFloat(VolumeKeyPrefix + volume.mixerParameter, 1f));
            volume.slider.onValueChanged.AddListener(value => SetVolume(volume.mixerParameter, value));
            SetVolume(volume.mixerParameter, volume.slider.value);
        }
    }

    private void SetVolume(string parameter, float value)
    {
        if (!mixer.SetFloat(parameter, Mathf.Log10(Mathf.Max(value, 0.0001f)) * 20f))
            Debug.LogWarning($"OptionsPanel: il parametro '{parameter}' non è esposto nell'Audio Mixer.");
        PlayerPrefs.SetFloat(VolumeKeyPrefix + parameter, value);
    }

    // ---------------- Pannello Input Keys ----------------

    private void SetupInputKeysPanel()
    {
        if (inputKeysPanel == null)
        {
            Debug.LogWarning("OptionsPanel: manca Input Keys Panel.");
            return;
        }

        if (inputKeysPanel.transform.IsChildOf(transform))
            Debug.LogWarning("OptionsPanel: sposta Input Keys Panel fuori da OptionsPanel (direttamente sotto il Canvas).");

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
            Debug.LogWarning("OptionsPanel: servono Keys Container e le Project-wide Actions " +
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
                Debug.LogWarning($"OptionsPanel: l'azione '{actionName}' non esiste in InputSystem_Actions.");
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

        Debug.LogWarning($"OptionsPanel: nessun tasto trovato per '{action.name}' {compositePart}.");
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