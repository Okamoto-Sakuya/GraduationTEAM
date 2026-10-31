using System.Collections.Generic;
using System.Diagnostics.Contracts;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using static AudioManager;
using static SEController;

public class VoiceController : MonoBehaviour
{
    public static VoiceController instance;

    [System.Serializable]
    public class VoiceData
    {
        public VoiceType type;

        [Header("AddressableのVoice")]
        public AssetReferenceT<AudioClip> clip;
    }

    public enum VoiceType
    {
        A,
        B,
        C
    }

    //============================================================
    // SE管理
    //============================================================

    [SerializeField]
    List<VoiceData> voiceList = new();

    Dictionary<VoiceType, AudioClip> voiceDic = new();

    Dictionary<VoiceType, AsyncOperationHandle<AudioClip>> voiceHandles = new();

    HashSet<VoiceType> loadingVoice = new();

    Dictionary<VoiceType, int> pendingPlayCounts = new();

    //============================================================
    // SEごとの音量設定
    //============================================================

    [SerializeField]
    ReadCSV_Audio readCSVAudio;

    //============================================================
    // ミュートするSE
    //============================================================

    [SerializeField]
    VoiceType[] muteVoice;

    bool isMuteVoice = false;

    //============================================================
    // 複数再生可能なSE
    //============================================================

    [SerializeField]
    VoiceType[] consecutiveVoice;

    //============================================================
    // AudioSource
    //============================================================

    const int maxVoiceCount = 10;

    AudioSource[] audioSources;

    Queue<AudioSource> playOrder = new();

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        
        audioSources = new AudioSource[maxVoiceCount];

        for(int i = 0; i < maxVoiceCount;  i++)
        {
            audioSources[i] = gameObject.AddComponent<AudioSource>();

            audioSources[i].playOnAwake = false;
            audioSources[i].loop = false;
        }

        LoadAllVoice();
    }

    //============================================================
    // Addressablesロード
    //============================================================

    /// <summary>
    /// Inspectorで設定したSEをすべて非同期ロードする
    /// </summary>
    private void LoadAllVoice()
    {
        HashSet<VoiceType> registeredTypes = new();

        foreach (VoiceData data in voiceList)
        {
            if (data == null || data.clip == null)
                continue;

            if (!registeredTypes.Add(data.type))
            {
                Debug.LogWarning(
                    $"SETypeが重複しています: {data.type}");
                continue;
            }

            VoiceType type = data.type;

            loadingVoice.Add(type);

            AsyncOperationHandle<AudioClip> handle =
                Addressables.LoadAssetAsync<AudioClip>(data.clip);

            voiceHandles[type] = handle;

            handle.Completed += operation =>
            {
                if (this == null)
                    return;

                loadingVoice.Remove(type);

                if (operation.Status == AsyncOperationStatus.Succeeded)
                {
                    voiceDic[type] = operation.Result;

                    Debug.Log($"SEロード成功: {type}");

                    // ロード中に要求された再生を実行
                    if (pendingPlayCounts.TryGetValue(
                        type, out int count))
                    {
                        pendingPlayCounts.Remove(type);

                        for (int i = 0; i < count; i++)
                        {
                            //PlayVoice(type);
                        }
                    }
                }
                else
                {
                    Debug.LogError(
                        $"SEロード失敗: {type}");

                    pendingPlayCounts.Remove(type);
                }
            };
        }
    }

    void Update()
    {
        if (audioSources == null)
            return;

        foreach (AudioSource source in audioSources)
        {
            source.volume = masterVolume * voiceVolume;
        }

        RemoveFinishedSources();
    }

    private void RemoveFinishedSources()
    {
        if (playOrder.Count == 0)
            return;

        Queue<AudioSource> newQueue = new();

        foreach (AudioSource source in playOrder)
        {
            if (source.isPlaying)
            {
                newQueue.Enqueue(source);
            }
            else
            {
                source.clip = null;
            }
        }

        playOrder = newQueue;
    }

    public static void PlayVoice(VoiceType type)
    {
        if(instance = null)
        {
            Debug.LogWarning("VoiceControllerが存在しません");
            return;
        }

        //if (IsMuteVoice(type))
        //    return;

        VoiceController controller = instance;

        if (!controller.voiceDic.TryGetValue
            (type, out AudioClip clip))
        {
            if (controller.loadingVoice.Contains(type))
            {
                if (IsConsecutiveVoice(type))
                {
                    controller.pendingPlayCounts.TryGetValue(type, out int count);
                    controller.pendingPlayCounts[type] = count + 1;
                }
                else
                {
                    controller.pendingPlayCounts[type] = 1;
                }

                return;
            }


            Debug.LogWarning($"Voice未登録またはロード失敗: {type}");
            return;
        }

        if (!IsConsecutiveVoice(type))
        {
            foreach (AudioSource source in controller.audioSources)
            {
                if (source.isPlaying && source.clip == clip)
                    return;
            }
        }

        foreach(AudioSource source in controller.audioSources)
        {
            if (!source.isPlaying)
            {
                PlayOnSource(source, clip, type);
                return;
            }
        }

        if(controller.playOrder.Count > 0)
        {
            AudioSource oldestSource = controller.playOrder.Dequeue();

            oldestSource.Stop();
            oldestSource.clip = null;

            PlayOnSource(oldestSource, clip, type);
        }
    }

    private static void PlayOnSource(
        AudioSource source,
        AudioClip clip,
        VoiceType type)
    {
        VoiceController controller = instance;

        controller.RemoveSourceFromQueue(source);

        // SETypeとClipVolumes.nameが一致する音量を取得
        float clipVolume = GetClipVolume(type);

        // AudioSource.volumeは全体音量にする
        source.volume = masterVolume * voiceVolume;

        // 再生中のSEを管理するためclipを設定
        source.clip = clip;

        // SEごとの音量はvolumeScaleで適用
        source.PlayOneShot(clip, clipVolume);

        controller.playOrder.Enqueue(source);
    }

    //============================================================
    // Queue管理
    //============================================================

    void RemoveSourceFromQueue(AudioSource target)
    {
        Queue<AudioSource> newQueue = new();

        foreach (AudioSource source in playOrder)
        {
            if (source != target)
            {
                newQueue.Enqueue(source);
            }
        }

        playOrder = newQueue;
    }

    static float GetClipVolume(VoiceType type)
    {
        VoiceController controller = instance;

        if (controller == null || controller.readCSVAudio == null)
        {
            Debug.LogWarning(
                "ReadCSV_Audioが設定されていません。音量を1にします。");

            return 1f;
        }

        // Enum名とCSVのnameを照合
        return controller.readCSVAudio.GetVolume(type.ToString());
    }

    //============================================================
    // consecutiveSE管理
    //============================================================

    /// <summary>
    /// 指定したSEが複数再生可能か確認
    /// </summary>
    static bool IsConsecutiveVoice(VoiceType type)
    {
        if (instance == null)
            return false;

        if (instance.consecutiveVoice == null)
            return false;

        foreach (VoiceType consecutiveType
                 in instance.consecutiveVoice)
        {
            if (consecutiveType == type)
                return true;
        }

        return false;
    }
}
