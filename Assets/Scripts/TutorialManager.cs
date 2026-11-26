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
    [Tooltip("Steps available from the start are shown in this order. A step that becomes available later (e.g. Shoot after picking up a weapon) is shown first, then the interrupted one comes back")]
    [SerializeField] TutorialStep[] steps;

    [Header("Timing")]
    [Tooltip("Fade in / fade out duration in seconds")]
    [SerializeField] float fadeTime = 0.3f;
    [Tooltip("Pause in seconds after a completed message, before the next one")]
    [SerializeField] float delayBetween = 0.5f;

    // {Jump}  {Move:left}  {Player/ShootUp}
    static readonly Regex KeyToken = new Regex(@"\{([\w/]+)(?::(\w+))?\}");

    // azioni già fatte: restano anche dopo una morte o un cambio livello
    static readonly HashSet<TutorialAction> completed = new HashSet<TutorialAction>();

    // messaggi in attesa: il primo è quello mostrato (una pila, i nuovi passano davanti)
    readonly List<TutorialStep> pending = new List<TutorialStep>();
    // passi già entrati nella pila almeno una volta
    readonly HashSet<TutorialStep> unlocked = new HashSet<TutorialStep>();

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
        bool firstCheck = true;
        TutorialStep shown = null;

        while (true)
        {
            UpdatePending(firstCheck);
            firstCheck = false;

            TutorialStep next = pending.Count > 0 ? pending[0] : null;

            if (next != shown)
            {
                // cambia messaggio: quello vecchio sparisce (completato o messo da parte), arriva il nuovo
                if (shown != null)
                {
                    bool wasCompleted = completed.Contains(shown.action);
                    yield return Fade(0f);

                    if (wasCompleted)
                        yield return new WaitForSecondsRealtime(delayBetween);
                }

                shown = next;

                if (shown != null)
                {
                    tutorialText.text = FormatMessage(shown.message);
                    yield return Fade(1f);
                }

                continue;   // ricontrolla subito: durante la dissolvenza può essere cambiato qualcosa
            }

            if (shown != null)
                tutorialText.text = FormatMessage(shown.message);   // si aggiorna se cambi i tasti dalle Opzioni
            else if (AllDone())
                yield break;                                         // tutorial finito

            yield return null;
        }
    }

    // aggiorna la pila dei messaggi
    void UpdatePending(bool firstCheck)
    {
        // toglie i messaggi delle azioni già fatte
        pending.RemoveAll(step => completed.Contains(step.action));

        // aggiunge quelli appena diventati possibili
        List<TutorialStep> newlyAvailable = new List<TutorialStep>();
        foreach (TutorialStep step in steps)
        {
            if (unlocked.Contains(step) || completed.Contains(step.action)) continue;
            if (!IsAvailable(step.action)) continue;

            unlocked.Add(step);
            newlyAvailable.Add(step);
        }

        if (newlyAvailable.Count == 0) return;

        if (firstCheck)
            pending.AddRange(newlyAvailable);          // all'avvio: nell'ordine della lista
        else
            pending.InsertRange(0, newlyAvailable);    // dopo: passano davanti a quello mostrato
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