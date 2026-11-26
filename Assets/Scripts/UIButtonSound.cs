using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Suono di click e di hover per un singolo pulsante.
// Non serve aggiungerlo a mano: lo mette ButtonSounds su tutti i pulsanti del Canvas.
[RequireComponent(typeof(Button))]
public class UIButtonSound : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    // true mentre un menu seleziona da solo il suo primo pulsante: in quel caso niente suono
    public static bool Muted;

    Button button;

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(() => AudioManager.Play(Sfx.Button));   // click col mouse, Invio o tasto azione del gamepad
    }

    // mouse sopra il pulsante = lo seleziona, così mouse, tastiera e gamepad evidenziano sempre lo stesso pulsante
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!button.interactable || EventSystem.current == null) return;

        EventSystem.current.SetSelectedGameObject(gameObject);
    }

    // suono di hover quando il pulsante viene selezionato (col mouse, con le frecce o con il gamepad)
    public void OnSelect(BaseEventData eventData)
    {
        if (Muted || !button.interactable) return;

        AudioManager.Play(Sfx.Hover);
    }
}