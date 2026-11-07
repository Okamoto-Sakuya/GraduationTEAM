using UnityEngine;

public class CursorController : MonoBehaviour
{
    RectTransform myRect;

    [SerializeField] float sensitivity = 1.0f;
    [SerializeField] Vector2 areaTopLeft;

    void Start()
    {
        myRect = GetComponent<RectTransform>();

        //本来のカーソル透明化
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;
    }

    void Update()
    {
        float mouseX = Input.GetAxisRaw("Mouse X");
        float mouseY = Input.GetAxisRaw("Mouse Y");

        Vector2 mouseDelta = new Vector2(mouseX, mouseY);

        myRect.position += (Vector3)(mouseDelta * sensitivity);
    }
}