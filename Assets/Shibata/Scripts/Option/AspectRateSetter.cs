using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AspectRateSetter : MonoBehaviour
{
    enum Type
    {
        A,  //2560*1440
        B,  //1920*1080
        C,  //1280*720
        D,  //FullScreen
    }
    Type type;

    [System.Serializable]
    class Node
    {
        public TextMeshProUGUI text;
        public Image image;
    }

    void Awake()
    {
        
    }

    void Start()
    {
        type = Type.B;   
    }

    void Update()
    {
        
    }

    void Changer()
    {
        switch(type)
        {
            case Type.A:
                
                break;
            case Type.B:
                break;
            case Type.C:
                break;
            case Type.D:
                break;
        }
    }

    public void ToggleAspRate(bool b)
    {

    }
}
