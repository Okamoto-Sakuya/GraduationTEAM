using UnityEngine;

public class OptionButton : MonoBehaviour
{
    public void Enter()
    {
        AspectRateCoontroller.instance.Enter();
    }

    //============================================================
    // オプション開閉
    //============================================================

    public void ToggleOption()
    {
        OptionController.instance.ToggleOptionMenu();
    }

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

    public void ASPSetter_Next()
    {
        AspectRateSetter.instance.ToggleAspRate(true);
    }

    public void ASPSetter_Back()
    {
        AspectRateSetter.instance.ToggleAspRate(false);
    }

    //============================================================
    // 使用言語変更用
    //============================================================

    public void LANGSetter_Next()
    {
        LanguageController.instance.ToggleLanguage(true);
    }

    public void LANGSetter_Back()
    {
        LanguageController.instance.ToggleLanguage(false);
    }

    //============================================================
    // メニューの表示切替
    //============================================================

    public void OpenMenu_Game()
    {
        OptionController.ToggleMenu(OptionController.Menu.GAME);
    }

    public void OpenMenu_Audio()
    {
        OptionController.ToggleMenu(OptionController.Menu.AUDIO);
    }
}
