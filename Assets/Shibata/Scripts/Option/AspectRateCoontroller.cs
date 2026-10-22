using UnityEngine;

public class AspectRateCoontroller : MonoBehaviour
{
    public static AspectRateCoontroller instance;

    [SerializeField]
    Vector2 startAspectRate = new Vector2(1920,1080);

    int currentWidth;
    int currentHeight;
    bool isFullScreen = false;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        Screen.SetResolution((int)startAspectRate.x, (int)startAspectRate.y, FullScreenMode.Windowed);
    }

    //============================================================
    // ボタン用
    //============================================================

    public void SetAspectRate(int width, int height)
    {
        Screen.SetResolution(width, height, FullScreenMode.Windowed);
    }

    public void SetFullScreenMode(bool b)
    {
        isFullScreen = b;
    }

    public void Enter()
    {
        FullScreenMode currentMode;

        if (isFullScreen)
        {
            //フルスクリーン
            currentMode = Screen.fullScreenMode;
        }
        else
        {
            //ウィンドウ
            Screen.SetResolution(currentWidth, currentHeight, FullScreenMode.Windowed);
        }
    }
}
