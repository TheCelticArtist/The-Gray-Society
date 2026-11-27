using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIButtonSound : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    public static bool Muted;

    Button button;

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(() => AudioManager.Play(Sfx.Button));
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button.interactable && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (!Muted && button.interactable) AudioManager.Play(Sfx.Hover);
    }
}