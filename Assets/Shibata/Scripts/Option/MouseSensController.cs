using UnityEngine;
using UnityEngine.UI;

public class MouseSensController : MonoBehaviour
{
    [SerializeField]
    Slider sensSlider;
    [SerializeField]
    RectTransform cursor;

    [SerializeField]
    float mouseSens = 1.0f;

    void Update()
    {
        //Cursor.visible = false;

        mouseSens = sensSlider.value;

        Vector2 mousePosition = Input.mousePosition;

        cursor.position = Vector2.Lerp(
            cursor.position,
            mousePosition,
            mouseSens * Time.deltaTime
        );
    }
}
