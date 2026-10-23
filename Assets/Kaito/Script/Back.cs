using UnityEngine;
using UnityEngine.SceneManagement;

public class Back : MonoBehaviour
{
    public void BackScene()
    {
        //シーン移行
        SceneManager.LoadScene("SeveBotton");
    }
}
