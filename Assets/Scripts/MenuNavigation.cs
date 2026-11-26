using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Mettilo su ogni pannello di menu (menu principale, pausa, opzioni, tasti).
// Quando il pannello si apre seleziona il primo elemento, così si naviga con frecce / D-pad / levetta
// e si conferma con Invio o con il tasto azione del gamepad (lo fa l'EventSystem con la mappa UI).
public class MenuNavigation : MonoBehaviour
{
    [Tooltip("The element selected when this menu opens. If empty, the first interactable element of the panel is used")]
    [SerializeField] Selectable firstSelected;

    GameObject lastSelected;

    void OnEnable()
    {
        StartCoroutine(SelectNextFrame());
    }

    void OnDisable()
    {
        // chiudendo il menu toglie la selezione: in gioco frecce e tasto azione non premono pulsanti nascosti
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem != null && eventSystem.currentSelectedGameObject != null &&
            eventSystem.currentSelectedGameObject.transform.IsChildOf(transform))
        {
            eventSystem.SetSelectedGameObject(null);
        }

        lastSelected = null;
    }

    IEnumerator SelectNextFrame()
    {
        yield return null;   // un frame di attesa: il clic che ha aperto il menu non deve rubare la selezione
        SelectFirst();
    }

    void Update()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return;

        GameObject current = eventSystem.currentSelectedGameObject;

        // la selezione si è persa (es. clic su uno spazio vuoto): torna al primo elemento
        if (current == null || !current.activeInHierarchy)
        {
            SelectFirst();
            return;
        }

        // se l'elemento selezionato è dentro una lista che scorre, la fa scorrere fino a lui
        if (current != lastSelected)
        {
            lastSelected = current;
            if (current.transform.IsChildOf(transform))
                KeepInView(current);
        }
    }

    void SelectFirst()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return;

        Selectable target = FindFirst();
        if (target == null) return;

        // selezione automatica: niente suono di hover
        UIButtonSound.Muted = true;
        eventSystem.SetSelectedGameObject(target.gameObject);
        UIButtonSound.Muted = false;
    }

    Selectable FindFirst()
    {
        if (firstSelected != null && firstSelected.IsActive() && firstSelected.IsInteractable())
            return firstSelected;

        // il primo elemento attivo e cliccabile, nell'ordine della Hierarchy
        foreach (Selectable selectable in GetComponentsInChildren<Selectable>())
        {
            if (selectable.IsActive() && selectable.IsInteractable() && selectable.navigation.mode != Navigation.Mode.None)
                return selectable;
        }

        return null;
    }

    // fa scorrere uno Scroll View in modo che l'elemento selezionato sia visibile
    static void KeepInView(GameObject selected)
    {
        ScrollRect scroll = selected.GetComponentInParent<ScrollRect>();
        if (scroll == null || scroll.content == null || !selected.transform.IsChildOf(scroll.content)) return;

        RectTransform viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
        Bounds item = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, selected.transform);
        Rect view = viewport.rect;

        float offset = 0f;
        if (item.max.y > view.yMax) offset = item.max.y - view.yMax;        // l'elemento è sopra la parte visibile
        else if (item.min.y < view.yMin) offset = item.min.y - view.yMin;   // l'elemento è sotto la parte visibile

        if (offset != 0f)
            scroll.content.anchoredPosition -= new Vector2(0f, offset);
    }
}