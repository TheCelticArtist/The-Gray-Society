using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum TutorialAction { Move, Jump, Shoot, SwitchWeapon, Pause }

[System.Serializable]
public class TutorialStep
{
    [Tooltip("Text shown on screen. Use {ActionName} to show the current key, e.g. {Jump}, {Move:left}, {ShootUp}, {Weapon1}")]
    [TextArea(1, 3)] public string message;
    [Tooltip("The action that makes this message disappear")]
    public TutorialAction action;
}

public class TutorialManager : MonoBehaviour
{
    const int MenuSceneIndex = 0;

    [Tooltip("If empty, the TextMeshPro on this object is used")]
    [SerializeField] TextMeshProUGUI tutorialText;
    [Tooltip("Steps available from the start are shown in this order. A step that becomes available later (e.g. Shoot after picking up a weapon) is shown first, then the interrupted one comes back")]
    [SerializeField] TutorialStep[] steps;

    [Header("Timing")]
    [Tooltip("Fade in / fade out duration in seconds")]
    [SerializeField] float fadeTime = 0.3f;
    [Tooltip("Pause in seconds after a completed message, before the next one")]
    [SerializeField] float delayBetween = 0.5f;

    static readonly Regex KeyToken = new Regex(@"\{([\w/]+)(?::(\w+))?\}");
    
    static readonly HashSet<TutorialAction> completed = new HashSet<TutorialAction>();

    readonly List<TutorialStep> pending = new List<TutorialStep>();
    readonly HashSet<TutorialStep> unlocked = new HashSet<TutorialStep>();

    public static void Notify(TutorialAction action) => completed.Add(action);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetProgress()
    {
        completed.Clear();
        SceneManager.sceneLoaded -= ClearOnMenu;
        SceneManager.sceneLoaded += ClearOnMenu;
    }

    static void ClearOnMenu(Scene scene, LoadSceneMode mode)
    {
        if (scene.buildIndex == MenuSceneIndex) completed.Clear();
    }

    void Start()
    {
        if (tutorialText == null) tutorialText = GetComponent<TextMeshProUGUI>();
        if (tutorialText == null) return;

        tutorialText.alpha = 0f;
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
                if (shown != null)
                {
                    bool wasCompleted = completed.Contains(shown.action);
                    yield return Fade(0f);
                    if (wasCompleted) yield return new WaitForSecondsRealtime(delayBetween);
                }

                shown = next;
                if (shown != null)
                {
                    tutorialText.text = FormatMessage(shown.message);
                    yield return Fade(1f);
                }
                continue;
            }

            if (shown != null) tutorialText.text = FormatMessage(shown.message);
            else if (steps.All(step => completed.Contains(step.action))) yield break;

            yield return null;
        }
    }

    void UpdatePending(bool firstCheck)
    {
        pending.RemoveAll(step => completed.Contains(step.action));

        List<TutorialStep> newSteps = steps
            .Where(step => !unlocked.Contains(step) && !completed.Contains(step.action) && IsAvailable(step.action))
            .ToList();
        if (newSteps.Count == 0) return;

        unlocked.UnionWith(newSteps);
        if (firstCheck) pending.AddRange(newSteps);
        else pending.InsertRange(0, newSteps);
    }

    static bool IsAvailable(TutorialAction action)
    {
        InventoryManager inventory = InventoryManager.Instance;
        return action switch
        {
            TutorialAction.Shoot => inventory != null && inventory.HasWeapon,
            TutorialAction.SwitchWeapon => inventory != null && inventory.WeaponCount >= 2,
            _ => true
        };
    }

    static string FormatMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return message;
        return KeyToken.Replace(message, match => KeyName(match.Groups[1].Value, match.Groups[2].Value));
    }

    static string KeyName(string actionName, string part)
    {
        if (InputSystem.actions == null) return "?";
        InputAction action = InputSystem.actions.FindAction(actionName.Contains("/") ? actionName : "Player/" + actionName);
        if (action == null) return "?";

        int index = OptionsPanel.FindKeyboardBinding(action, part);
        return index >= 0 ? action.GetBindingDisplayString(index) : "?";
    }

    IEnumerator Fade(float target)
    {
        float start = tutorialText.alpha;
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            tutorialText.alpha = Mathf.Lerp(start, target, t / fadeTime);
            yield return null;
        }
        tutorialText.alpha = target;
    }
}