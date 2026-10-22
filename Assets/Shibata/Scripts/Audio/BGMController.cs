using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using static AudioManager;

public class BGMController : MonoBehaviour
{
    public static BGMController Inst_BGMManager;

    [System.Serializable]
    public class BGMData
    {
        public BGMType type;
        public AudioClip clip;
    }

    public enum BGMType
    {
        Title,
        MainGame1,
        MainGame2,
        MainGame3,
        MainGame4,
        Clear,
        SkillTree,

        MainGame
    }

    [SerializeField]
    List<BGMData> bgmList = new();

    Dictionary<BGMType, AudioClip> bgmDictionary = new();

    AudioSource audioSource;

    void Awake()
    {
        DontDestroyOnLoad(this.gameObject);
        if (Inst_BGMManager != null)
        {
            Destroy(gameObject);
            return;
        }
        Inst_BGMManager = this;

        audioSource = GetComponent<AudioSource>();

        // InspectorÇ≈ê›íËÇµÇΩì‡óeÇDictionaryÇ÷ïœä∑
        foreach (BGMData data in bgmList)
        {
            if (data.clip != null)
            {
                bgmDictionary[data.type] = data.clip;
            }
        }
    }

    void Start()
    {
        bgmVolume = 0;
        foreach(BGMType type in bgmDictionary.Keys)
        {
            PlayBGM(type);

            if(type == BGMType.SkillTree)
            {
                bgmVolume = 1;
                break;
            }
        }

        PlayBGM(BGMType.Title);
    }

    void Update()
    {
        Inst_BGMManager.audioSource.volume = masterVolume * bgmVolume;
    }

    public static void PlayBGM(BGMType type)
    {
        if (Inst_BGMManager == null)
        {
            Debug.LogWarning("BGMManagerÇ™ë∂ç›ÇµÇ‹ÇπÇÒ");
            return;
        }

        if (Inst_BGMManager.bgmDictionary.TryGetValue(type, out AudioClip clip))
        {
            Inst_BGMManager.audioSource.Stop();

            Inst_BGMManager.audioSource.loop = true;

            Inst_BGMManager.audioSource.clip = clip;

            Inst_BGMManager.audioSource.Play();
        }
        else
        {
            Debug.LogWarning($"BGMñ¢ìoò^: {type}");
        }
    }
}
