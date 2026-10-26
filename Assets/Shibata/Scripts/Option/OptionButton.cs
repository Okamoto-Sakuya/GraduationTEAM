using UnityEngine;

public class OptionButton : MonoBehaviour
{
    [SerializeField] 
    AspectRateSetter setter;

    //============================================================
    // アスペクト比変更用
    //============================================================

    public void AspectRate_A()
    {
        AspectRateCoontroller.instance.SetAspectRate(2560, 1440);
        Debug.Log("AspectRate_A");
    }

    public void AspectRate_B()
    {
        AspectRateCoontroller.instance.SetAspectRate(1920, 1080);
    }

    public void AspectRate_C()
    {
        AspectRateCoontroller.instance.SetAspectRate(1280, 720);
    }

    public void Setter_Next()
    {
        setter.ToggleAspRate(true);
    }

    public void Setter_Back()
    {
        setter.ToggleAspRate(false);
    }

    public void Enter()
    {
        AspectRateCoontroller.instance.Enter();
    }
}
