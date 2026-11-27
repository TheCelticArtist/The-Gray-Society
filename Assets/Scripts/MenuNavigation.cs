using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MenuNavigation : MonoBehaviour
{
    [Tooltip("The element selected when this menu opens. If empty, the first interactable element of the panel is used")]
    [SerializeField] Selectable firstSelected;

    GameObject lastSelected;

    void OnEnable() => StartCoroutine(SelectNextFrame());

    IEnumerator SelectNextFrame()
    {
        yield return null;
        SelectFirst();
    }

    void OnDisable()
    {
        EventSystem eventSystem = EventSystem.current;
        GameObject selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
        if (selected != null && selected.transform.IsChildOf(transform)) eventSystem.SetSelectedGameObject(null);
        lastSelected = null;
    }

    void Update()
    {
        if (EventSystem.current == null) return;
        GameObject current = EventSystem.current.currentSelectedGameObject;

        if (current == null || !current.activeInHierarchy) { SelectFirst(); return; }

        if (current == lastSelected) return;
        lastSelected = current;
        if (current.transform.IsChildOf(transform)) KeepInView(current);
    }

    void SelectFirst()
    {
        Selectable target = FindFirst();
        if (EventSystem.current == null || target == null) return;

        UIButtonSound.Muted = true;
        EventSystem.current.SetSelectedGameObject(target.gameObject);
        UIButtonSound.Muted = false;
    }

    Selectable FindFirst()
    {
        if (firstSelected != null && firstSelected.IsActive() && firstSelected.IsInteractable()) return firstSelected;

        foreach (Selectable selectable in GetComponentsInChildren<Selectable>())
            if (selectable.IsActive() && selectable.IsInteractable() && selectable.navigation.mode != Navigation.Mode.None)
                return selectable;
        return null;
    }

    static void KeepInView(GameObject selected)
    {
        ScrollRect scroll = selected.GetComponentInParent<ScrollRect>();
        if (scroll == null || scroll.content == null || !selected.transform.IsChildOf(scroll.content)) return;

        RectTransform viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
        Bounds item = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, selected.transform);
        Rect view = viewport.rect;

        float offset = 0f;
        if (item.max.y > view.yMax) offset = item.max.y - view.yMax;
        else if (item.min.y < view.yMin) offset = item.min.y - view.yMin;
        if (offset != 0f) scroll.content.anchoredPosition -= new Vector2(0f, offset);
    }
}