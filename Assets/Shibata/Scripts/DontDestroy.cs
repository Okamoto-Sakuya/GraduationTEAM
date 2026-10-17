using UnityEngine;

public class DontDestroy : MonoBehaviour
{
    private void Awake()
    {
        DontDestroyOnLoad(this.gameObject);
        //if(OptionManager.instance != null)
        //{
        //    Destroy(gameObject);
        //    return;
        //}
        //OptionManager.instance = GameObject.Find("Manager").GetComponent<OptionManager>();
    }
}
