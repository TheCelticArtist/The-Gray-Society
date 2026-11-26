using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    // lo legge il PlayerController per bloccare i comandi durante la pausa
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

    InputAction pauseAction;   // azione Player/Pause, rimappabile dalle Opzioni (se manca si usa Esc)

    void Awake()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        pauseRoot.SetActive(false);

        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (optionsButton != null) optionsButton.onClick.AddListener(OpenOptions);
        if (optionsBackButton != null) optionsBackButton.onClick.AddListener(CloseOptions);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(GoToMainMenu);
        if (quitButton != null) quitButton.onClick.AddListener(QuitGame);
    }

    void Start()
    {
        // prepara le Opzioni una volta sola e applica gamma, volumi e tasti salvati
        if (optionsPanel != null)
        {
            optionsPanel.Init();
            optionsPanel.gameObject.SetActive(false);
        }

        // tasto della pausa: l'azione Player/Pause (i tasti salvati li ha già caricati l'OptionsPanel qui sopra)
        if (InputSystem.actions != null)
            pauseAction = InputSystem.actions.FindAction("Player/Pause");

        if (pauseAction != null)
            pauseAction.Enable();
        else
            Debug.LogWarning("PauseMenu: action 'Player/Pause' not found in the Input Actions asset, Esc is used instead.");
    }

    bool PausePressed()
    {
        if (pauseAction != null)
            return pauseAction.WasPressedThisFrame();

        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
    }

    void Update()
    {
        if (!PausePressed()) return;

        if (!IsPaused)
            Pause();
        else if (inputKeysPanel != null && inputKeysPanel.activeSelf)
            return;                                   // nel pannello dei tasti la pausa non fa niente (ESC annulla il cambio tasto)
        else if (optionsPanel != null && optionsPanel.gameObject.activeSelf)
            CloseOptions();                           // tasto pausa nelle Opzioni = torna alla pausa
        else
            Resume();
    }

    public void Pause()
    {
        IsPaused = true;
        Time.timeScale = 0f;                          // ferma fisica, movimenti e animazioni
        AudioManager.PauseMusic();
        TutorialManager.Notify(TutorialAction.Pause);

        pauseRoot.SetActive(true);
        pausePanel.SetActive(true);
        if (optionsPanel != null) optionsPanel.gameObject.SetActive(false);
        if (inputKeysPanel != null) inputKeysPanel.SetActive(false);
    }

    public void Resume()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        AudioManager.ResumeMusic();                   // la musica riparte da dove si era fermata
        pauseRoot.SetActive(false);
    }

    void OpenOptions()
    {
        if (optionsPanel == null) return;

        pausePanel.SetActive(false);
        optionsPanel.gameObject.SetActive(true);
    }

    void CloseOptions()
    {
        optionsPanel.gameObject.SetActive(false);     // l'OptionsPanel salva le impostazioni quando si chiude
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
        PlayerPrefs.Save();                           // salva le impostazioni prima di uscire

#if UNITY_EDITOR
        // nell'Editor Application.Quit() viene ignorato: qui ferma il Play
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // nella build chiude il gioco
        Application.Quit();
#endif
    }

    void OnDestroy()
    {
        // se la scena cambia mentre il gioco è in pausa, il tempo riparte
        if (IsPaused)
        {
            IsPaused = false;
            Time.timeScale = 1f;
            AudioManager.ResumeMusic();
        }
    }
}