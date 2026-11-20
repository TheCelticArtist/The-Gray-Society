using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("Scena del gioco (deve essere in File > Build Profiles)")]
    [SerializeField] private string gameScene = "Game";

    [Header("Pannelli")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private OptionsPanel optionsPanel;
    [SerializeField] private GameObject creditsPanel;

    [Header("Bottoni del menu")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button quitButton;

    [Header("Bottoni Back")]
    [SerializeField] private Button optionsBackButton;
    [SerializeField] private Button creditsBackButton;

    private void Start()
    {
        Show(mainPanel);

        startButton.onClick.AddListener(StartGame);
        optionsButton.onClick.AddListener(() => Show(optionsPanel.gameObject));
        creditsButton.onClick.AddListener(() => Show(creditsPanel));
        quitButton.onClick.AddListener(Quit);
        optionsBackButton.onClick.AddListener(() => Show(mainPanel));
        creditsBackButton.onClick.AddListener(() => Show(mainPanel));

        // Applica le impostazioni salvate anche se il giocatore non apre Options.
        optionsPanel.Init();
    }

    private void Show(GameObject panel)
    {
        mainPanel.SetActive(panel == mainPanel);
        optionsPanel.gameObject.SetActive(panel == optionsPanel.gameObject);
        creditsPanel.SetActive(panel == creditsPanel);
    }

    private void StartGame()
    {
        SceneManager.LoadScene(gameScene);
    }

    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}