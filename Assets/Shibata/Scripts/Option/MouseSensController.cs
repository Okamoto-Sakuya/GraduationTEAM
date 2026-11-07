using UnityEngine;
using UnityEngine.UI;

public class MouseSensController : MonoBehaviour
{
    [SerializeField]
    Slider sensSlider;
    [SerializeField]
    RectTransform cursor;

    void Update()
    {
        OptionManager.mouseSens = sensSlider.value;
    }
}
