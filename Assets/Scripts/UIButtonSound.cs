using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Suono di click e di hover per un singolo pulsante.
// Non serve aggiungerlo a mano: lo mette ButtonSounds su tutti i pulsanti del Canvas.
[RequireComponent(typeof(Button))]
public class UIButtonSound : MonoBehaviour, IPointerEnterHandler
{
    Button button;

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(() => AudioManager.Play(Sfx.Button));
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button.interactable)              // niente suono sui pulsanti disattivati
            AudioManager.Play(Sfx.Hover);
    }
}