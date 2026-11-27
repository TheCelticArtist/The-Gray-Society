using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    [Header("Panels")]
    [Tooltip("The whole pause menu (dark background included), hidden at start")]
    [SerializeField] GameObject pauseRoot;
    [Tooltip("The panel with Resume / Options / Main Menu / Quit")]
    [SerializeField] GameObject pausePanel;
    [Tooltip("The Options panel (optional)")]
    [SerializeField] OptionsPanel optionsPanel;
    [Tooltip("The same Input Keys Panel linked to the OptionsPanel (optional)")]
    [SerializeField] GameObject inputKeysPanel;

    [Header("Buttons")]
    [SerializeField] Button resumeButton;
    [SerializeField] Button optionsButton;
    [Tooltip("The Back button inside the Options panel")]
    [SerializeField] Button optionsBackButton;
    [SerializeField] Button mainMenuButton;
    [SerializeField] Button quitButton;

    [Header("Main Menu")]
    [Tooltip("Build index of the main menu scene")]
    [SerializeField] int menuSceneIndex = 0;

    InputAction pauseAction;

    void Awake()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        pauseRoot.SetActive(false);

        Bind(resumeButton, Resume);
        Bind(optionsButton, OpenOptions);
        Bind(optionsBackButton, CloseOptions);
        Bind(mainMenuButton, GoToMainMenu);
        Bind(quitButton, QuitGame);
    }

    static void Bind(Button button, UnityAction action)
    {
        if (button != null) button.onClick.AddListener(action);
    }

    void Start()
    {
        if (optionsPanel != null)
        {
            optionsPanel.Init();
            optionsPanel.gameObject.SetActive(false);
        }

        if (InputSystem.actions != null) pauseAction = InputSystem.actions.FindAction("Player/Pause");
        pauseAction?.Enable();
    }

    void Update()
    {
        bool pressed = pauseAction != null
            ? pauseAction.WasPressedThisFrame()
            : Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        if (!pressed) return;

        if (!IsPaused) Pause();
        else if (inputKeysPanel != null && inputKeysPanel.activeSelf) return;
        else if (optionsPanel != null && optionsPanel.gameObject.activeSelf) CloseOptions();
        else Resume();
    }

    public void Pause()
    {
        SetPaused(true);
        TutorialManager.Notify(TutorialAction.Pause);

        pauseRoot.SetActive(true);
        pausePanel.SetActive(true);
        if (optionsPanel != null) optionsPanel.gameObject.SetActive(false);
        if (inputKeysPanel != null) inputKeysPanel.SetActive(false);
    }

    public void Resume()
    {
        SetPaused(false);
        pauseRoot.SetActive(false);
    }

    static void SetPaused(bool paused)
    {
        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
        if (paused) AudioManager.PauseMusic();
        else AudioManager.ResumeMusic();
    }

    void OpenOptions()
    {
        if (optionsPanel == null) return;
        pausePanel.SetActive(false);
        optionsPanel.gameObject.SetActive(true);
    }

    void CloseOptions()
    {
        optionsPanel.gameObject.SetActive(false);
        pausePanel.SetActive(true);
    }

    void GoToMainMenu()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(menuSceneIndex);
    }

    void QuitGame()
    {
        PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnDestroy()
    {
        if (IsPaused) SetPaused(false);
    }
}