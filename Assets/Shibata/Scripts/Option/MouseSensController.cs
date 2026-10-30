using UnityEngine;
using UnityEngine.UI;

public class MouseSensController : MonoBehaviour
{
    [SerializeField]
    Slider sensSlider;

    [SerializeField]
    float mouseSens = 1.0f;

    void Update()
    {
        mouseSens = sensSlider.value;

        float x = Input.GetAxis("Horizontal") * mouseSens;
        float y = Input.GetAxis("Vertical") * mouseSens;
    }
}
