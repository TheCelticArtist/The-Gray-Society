using UnityEngine;
using UnityEngine.UI;

public class ButtonSounds : MonoBehaviour
{
    void Start()
    {
        foreach (Button button in GetComponentsInChildren<Button>(true))
            Register(button);
    }

    public static void Register(Button button)
    {
        if (button != null && !button.TryGetComponent(out UIButtonSound _))
            button.gameObject.AddComponent<UIButtonSound>();
    }
}