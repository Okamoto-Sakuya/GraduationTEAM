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

        currentWidth = (int)startAspectRate.x;
        currentHeight = (int)startAspectRate.y;

        Screen.SetResolution(currentWidth, currentHeight, FullScreenMode.Windowed);
    }

    void Update()
    {
        if(Input.GetKeyDown(KeyCode.A))
        {
            Debug.Log("W" + currentWidth + "*H" + currentHeight);
        }
    }

    //============================================================
    // ボタン用
    //============================================================

    public void SetAspectRate(int width, int height)
    {
        //Screen.SetResolution(width, height, FullScreenMode.Windowed);

        currentWidth = width;
        currentHeight = height;
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
