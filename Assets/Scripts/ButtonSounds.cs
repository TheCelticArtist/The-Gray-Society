using UnityEngine;
using UnityEngine.UI;

// Mettilo sul Canvas del menu principale e su quello della pausa:
// aggiunge click e hover a tutti i pulsanti figli, anche a quelli dentro pannelli nascosti (es. Opzioni)
public class ButtonSounds : MonoBehaviour
{
    void Start()
    {
        foreach (Button button in GetComponentsInChildren<Button>(true))
            Register(button);
    }

    // da usare per i pulsanti creati da codice (es. i "Rebind" delle Opzioni)
    public static void Register(Button button)
    {
        if (button != null && !button.TryGetComponent(out UIButtonSound _))
            button.gameObject.AddComponent<UIButtonSound>();
    }
}