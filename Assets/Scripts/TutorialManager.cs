using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// le azioni che fanno sparire un messaggio del tutorial
public enum TutorialAction { Move, Jump, Shoot, SwitchWeapon, Pause }

[System.Serializable]
public class TutorialStep
{
    [Tooltip("Text shown on screen. Use {ActionName} to show the current key, e.g. {Jump}, {Move:left}, {ShootUp}, {Weapon1}")]
    [TextArea(1, 3)] public string message;
    [Tooltip("The action that makes this message disappear")]
    public TutorialAction action;
}

// Mettilo sul testo TextMeshPro del tutorial (in alto al centro dell'HUD)
public class TutorialManager : MonoBehaviour
{
    const int MenuSceneIndex = 0;   // tornando al menu il tutorial riparte da capo

    [Tooltip("If empty, the TextMeshPro on this object is used")]
    [SerializeField] TextMeshProUGUI tutorialText;
    [SerializeField] TutorialStep[] steps;

    [Header("Timing")]
    [Tooltip("Fade in / fade out duration in seconds")]
    [SerializeField] float fadeTime = 0.3f;
    [Tooltip("Pause in seconds between one message and the next")]
    [SerializeField] float delayBetween = 0.5f;

    // {Jump}  {Move:left}  {Player/ShootUp}
    static readonly Regex KeyToken = new Regex(@"\{([\w/]+)(?::(\w+))?\}");

    // azioni già fatte: restano anche dopo una morte o un cambio livello
    static readonly HashSet<TutorialAction> completed = new HashSet<TutorialAction>();

    // da chiamare quando il giocatore compie un'azione: TutorialManager.Notify(TutorialAction.Jump);
    public static void Notify(TutorialAction action)
    {
        completed.Add(action);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetProgress()
    {
        completed.Clear();
        SceneManager.sceneLoaded -= ClearOnMenu;
        SceneManager.sceneLoaded += ClearOnMenu;
    }

    static void ClearOnMenu(Scene scene, LoadSceneMode mode)
    {
        if (scene.buildIndex == MenuSceneIndex)
            completed.Clear();
    }

    void Start()
    {
        if (tutorialText == null)
            tutorialText = GetComponent<TextMeshProUGUI>();

        if (tutorialText == null)
        {
            Debug.LogError("TutorialManager: the TextMeshPro text is missing!");
            return;
        }

        tutorialText.alpha = 0f;   // nascosto finché non c'è un messaggio da mostrare
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        while (true)
        {
            TutorialStep step = NextStep();

            if (step == null)
            {
                if (AllDone()) yield break;   // tutorial finito
                yield return null;            // aspetta che un passo diventi possibile (es. raccogliere l'arma)
                continue;
            }

            // mostra il messaggio
            tutorialText.text = FormatMessage(step.message);
            yield return Fade(1f);

            // aspetta che il giocatore compia l'azione
            while (!completed.Contains(step.action))
            {
                tutorialText.text = FormatMessage(step.message);   // si aggiorna se cambi i tasti dalle Opzioni
                yield return null;
            }

            // lo fa sparire
            yield return Fade(0f);
            yield return new WaitForSecondsRealtime(delayBetween);
        }
    }

    // il primo passo non ancora fatto e già possibile
    TutorialStep NextStep()
    {
        foreach (TutorialStep step in steps)
        {
            if (!completed.Contains(step.action) && IsAvailable(step.action))
                return step;
        }
        return null;
    }

    bool AllDone()
    {
        foreach (TutorialStep step in steps)
        {
            if (!completed.Contains(step.action))
                return false;
        }
        return true;
    }

    // alcune azioni si possono fare solo in certi momenti
    bool IsAvailable(TutorialAction action)
    {
        InventoryManager inventory = InventoryManager.Instance;

        switch (action)
        {
            case TutorialAction.Shoot:
                return inventory != null && inventory.HasWeapon;          // dopo aver raccolto un'arma

            case TutorialAction.SwitchWeapon:
                return inventory != null && CountWeapons(inventory) >= 2;  // con almeno due armi

            default:
                return true;
        }
    }

    int CountWeapons(InventoryManager inventory)
    {
        int count = 0;
        for (int slot = 1; slot <= 9; slot++)
        {
            if (inventory.HasWeaponInSlot(slot))
                count++;
        }
        return count;
    }

    // ---------------- Tasti nei messaggi ----------------

    // sostituisce {Jump}, {Move:left}, ... con il tasto attualmente impostato
    static string FormatMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return message;

        return KeyToken.Replace(message, match =>
        {
            string actionName = match.Groups[1].Value;
            string part = match.Groups[2].Success ? match.Groups[2].Value : "";
            return GetKeyName(actionName, part);
        });
    }

    static string GetKeyName(string actionName, string part)
    {
        InputActionAsset actions = InputSystem.actions;
        if (actions == null) return "?";

        InputAction action = actions.FindAction(actionName.Contains("/") ? actionName : "Player/" + actionName);
        if (action == null) return "?";

        // primo tasto di tastiera/mouse dell'azione (o della parte "left"/"right" per Move)
        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];
            // stesso criterio dell'OptionsPanel: il tasto originale, poi GetBindingDisplayString mostra quello rimappato
            bool keyboardOrMouse = binding.path.StartsWith("<Keyboard>") || binding.path.StartsWith("<Mouse>");
            if (!keyboardOrMouse) continue;

            bool match = string.IsNullOrEmpty(part)
                ? !binding.isPartOfComposite
                : binding.isPartOfComposite && string.Equals(binding.name, part, System.StringComparison.OrdinalIgnoreCase);

            if (match)
                return action.GetBindingDisplayString(i);
        }

        return "?";
    }

    IEnumerator Fade(float target)
    {
        if (fadeTime <= 0f)
        {
            tutorialText.alpha = target;
            yield break;
        }

        float start = tutorialText.alpha;
        float t = 0f;

        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;   // funziona anche con il gioco in pausa
            tutorialText.alpha = Mathf.Lerp(start, target, t / fadeTime);
            yield return null;
        }

        tutorialText.alpha = target;
    }
}