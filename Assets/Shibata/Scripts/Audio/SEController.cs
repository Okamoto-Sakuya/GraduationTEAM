using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using static AudioManager;

public class SEController : MonoBehaviour
{
    public static SEController instance;

    //============================================================
    // SEデータ
    //============================================================

    [System.Serializable]
    public class SEData
    {
        public SEType type;

        [Header("AddressableのSE")]
        public AssetReferenceT<AudioClip> clip;
    }

    //============================================================
    // SEの種類
    //============================================================

    public enum SEType
    {
        Enter,
        Cancel,
        OpenWindow,
    }

    //============================================================
    // SE管理
    //============================================================

    [SerializeField]
    private List<SEData> seList = new();

    // ロード済みのAudioClip
    private Dictionary<SEType, AudioClip> seDictionary = new();

    // Addressablesのロードハンドル
    private Dictionary<SEType, AsyncOperationHandle<AudioClip>>
        seHandles = new();

    // ロード中のSE
    private HashSet<SEType> loadingSE = new();

    // ロード中に要求された再生回数
    private Dictionary<SEType, int> pendingPlayCounts = new();

    //============================================================
    // SEごとの音量設定
    //============================================================

    [Header("SEごとの音量設定")]
    [SerializeField]
    private ReadCSV_Audio readCSVAudio;

    //============================================================
    // ミュートするSE
    //============================================================

    [Header("ミュートするSE")]
    [SerializeField]
    private SEType[] muteSE;

    private bool isMuteSE = false;

    //============================================================
    // 複数再生可能なSE
    //============================================================

    [Header("複数再生していいSE")]
    [SerializeField]
    private SEType[] consecutiveSE;

    //============================================================
    // AudioSource
    //============================================================

    private const int MaxSECount = 10;

    private AudioSource[] audioSources;

    // 0 = 最も古い
    private Queue<AudioSource> playOrder = new();

    //============================================================
    // 初期化
    //============================================================

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        // AudioSourceを作成
        audioSources = new AudioSource[MaxSECount];

        for (int i = 0; i < MaxSECount; i++)
        {
            audioSources[i] = gameObject.AddComponent<AudioSource>();

            audioSources[i].playOnAwake = false;
            audioSources[i].loop = false;
        }

        // AddressablesからSEをロード
        LoadAllSE();
    }

    //============================================================
    // Addressablesロード
    //============================================================

    /// <summary>
    /// Inspectorで設定したSEをすべて非同期ロードする
    /// </summary>
    private void LoadAllSE()
    {
        HashSet<SEType> registeredTypes = new();

        foreach (SEData data in seList)
        {
            if (data == null || data.clip == null)
                continue;

            if (!registeredTypes.Add(data.type))
            {
                Debug.LogWarning(
                    $"SETypeが重複しています: {data.type}");
                continue;
            }

            SEType type = data.type;

            loadingSE.Add(type);

            AsyncOperationHandle<AudioClip> handle =
                Addressables.LoadAssetAsync<AudioClip>(data.clip);

            seHandles[type] = handle;

            handle.Completed += operation =>
            {
                if (this == null)
                    return;

                loadingSE.Remove(type);

                if (operation.Status == AsyncOperationStatus.Succeeded)
                {
                    seDictionary[type] = operation.Result;

                    Debug.Log($"SEロード成功: {type}");

                    // ロード中に要求された再生を実行
                    if (pendingPlayCounts.TryGetValue(
                        type, out int count))
                    {
                        pendingPlayCounts.Remove(type);

                        for (int i = 0; i < count; i++)
                        {
                            PlaySE(type);
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

    //============================================================
    // Update
    //============================================================

    private void Update()
    {
        if (audioSources == null)
            return;

        // 全体音量を適用
        // SEごとの音量はPlayOneShotのvolumeScaleで適用する
        foreach (AudioSource source in audioSources)
        {
            source.volume = masterVolume * seVolume;
        }

        RemoveFinishedSources();

        // デバッグ用
        if (Input.GetKeyDown(KeyCode.Space))
        {
            PlaySE(SEType.Enter);
            Debug.Log("PlaySE: Enter");
        }
    }

    //============================================================
    // SEミュート管理
    //============================================================

    /// <summary>
    /// Inspectorで設定したSEをミュートする
    /// </summary>
    public static void MuteSE()
    {
        if (instance == null)
        {
            Debug.LogWarning("SEManagerが存在しません");
            return;
        }

        instance.isMuteSE = true;

        if (instance.muteSE == null)
            return;

        foreach (SEType type in instance.muteSE)
        {
            instance.RemoveSEFromAudioSource(type);
        }
    }

    /// <summary>
    /// ミュートを解除する
    /// </summary>
    public static void UnmuteSE()
    {
        if (instance == null)
        {
            Debug.LogWarning("SEManagerが存在しません");
            return;
        }

        instance.isMuteSE = false;
    }

    /// <summary>
    /// 指定したSEが現在ミュート対象か確認
    /// </summary>
    private static bool IsMuteSE(SEType type)
    {
        if (instance == null)
            return false;

        if (!instance.isMuteSE)
            return false;

        if (instance.muteSE == null)
            return false;

        foreach (SEType muteType in instance.muteSE)
        {
            if (muteType == type)
                return true;
        }

        return false;
    }

    //============================================================
    // consecutiveSE管理
    //============================================================

    /// <summary>
    /// 指定したSEが複数再生可能か確認
    /// </summary>
    private static bool IsConsecutiveSE(SEType type)
    {
        if (instance == null)
            return false;

        if (instance.consecutiveSE == null)
            return false;

        foreach (SEType consecutiveType
                 in instance.consecutiveSE)
        {
            if (consecutiveType == type)
                return true;
        }

        return false;
    }

    //============================================================
    // AudioSourceからSEを外す
    //============================================================

    /// <summary>
    /// 指定したSEを再生中のAudioSourceを停止する
    /// </summary>
    public void RemoveSEFromAudioSource(SEType type)
    {
        if (!seDictionary.TryGetValue(
            type, out AudioClip clip))
        {
            // ロード中の場合も再生要求を破棄
            pendingPlayCounts.Remove(type);
            return;
        }

        foreach (AudioSource source in audioSources)
        {
            if (source.clip == clip)
            {
                source.Stop();
                source.clip = null;

                RemoveSourceFromQueue(source);
            }
        }
    }

    //============================================================
    // 再生終了SEの整理
    //============================================================

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

    //============================================================
    // SE再生
    //============================================================

    /// <summary>
    /// SE再生
    /// </summary>
    public static void PlaySE(SEType type)
    {
        if (instance == null)
        {
            Debug.LogWarning("SEManagerが存在しません");
            return;
        }

        // ミュート中なら再生しない
        if (IsMuteSE(type))
            return;

        SEController manager = instance;

        // ロード済みか確認
        if (!manager.seDictionary.TryGetValue(
            type, out AudioClip clip))
        {
            // ロード中なら再生要求を保留する
            if (manager.loadingSE.Contains(type))
            {
                if (IsConsecutiveSE(type))
                {
                    manager.pendingPlayCounts.TryGetValue(
                        type, out int count);

                    manager.pendingPlayCounts[type] = count + 1;
                }
                else
                {
                    // 同じSEの要求は1回にまとめる
                    manager.pendingPlayCounts[type] = 1;
                }

                return;
            }

            Debug.LogWarning($"SE未登録またはロード失敗: {type}");
            return;
        }

        // 複数再生不可なら同じSEの重複再生を防ぐ
        if (!IsConsecutiveSE(type))
        {
            foreach (AudioSource source in manager.audioSources)
            {
                if (source.isPlaying && source.clip == clip)
                    return;
            }
        }

        // 空いているAudioSourceを探す
        foreach (AudioSource source in manager.audioSources)
        {
            if (!source.isPlaying)
            {
                PlayOnSource(source, clip, type);
                return;
            }
        }

        // すべて使用中なら最も古いSEを停止して再利用
        if (manager.playOrder.Count > 0)
        {
            AudioSource oldestSource =
                manager.playOrder.Dequeue();

            oldestSource.Stop();
            oldestSource.clip = null;

            PlayOnSource(oldestSource, clip, type);
        }
    }

    //============================================================
    // SEごとの音量取得
    //============================================================

    /// <summary>
    /// SETypeとClipVolumes.nameが一致する音量を取得
    /// </summary>
    private static float GetClipVolume(SEType type)
    {
        SEController manager = instance;

        if (manager == null || manager.readCSVAudio == null)
        {
            Debug.LogWarning(
                "ReadCSV_Audioが設定されていません。音量を1にします。");

            return 1f;
        }

        // Enum名とCSVのnameを照合
        return manager.readCSVAudio.GetVolume(type.ToString());
    }

    //============================================================
    // AudioSourceで再生
    //============================================================

    private static void PlayOnSource(
        AudioSource source,
        AudioClip clip,
        SEType type)
    {
        SEController manager = instance;

        manager.RemoveSourceFromQueue(source);

        // SETypeとClipVolumes.nameが一致する音量を取得
        float clipVolume = GetClipVolume(type);

        // AudioSource.volumeは全体音量にする
        source.volume = masterVolume * seVolume;

        // 再生中のSEを管理するためclipを設定
        source.clip = clip;

        // SEごとの音量はvolumeScaleで適用
        source.PlayOneShot(clip, clipVolume);

        manager.playOrder.Enqueue(source);
    }

    //============================================================
    // Queue管理
    //============================================================

    private void RemoveSourceFromQueue(AudioSource target)
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

    //============================================================
    // 全SE停止
    //============================================================

    /// <summary>
    /// 再生中のすべてのSEを停止する
    /// </summary>
    public static void StopAllSE()
    {
        if (instance == null)
        {
            Debug.LogWarning("SEManagerが存在しません");
            return;
        }

        SEController manager = instance;

        foreach (AudioSource source in manager.audioSources)
        {
            if (source.isPlaying)
                source.Stop();

            source.clip = null;
        }

        manager.playOrder.Clear();

        // ロード中の再生要求も破棄
        manager.pendingPlayCounts.Clear();
    }

    //============================================================
    // 終了処理・Addressables解放
    //============================================================

    private void OnDestroy()
    {
        if (instance != this)
            return;

        // 再生中の音声を停止
        if (audioSources != null)
        {
            foreach (AudioSource source in audioSources)
            {
                if (source != null)
                {
                    source.Stop();
                    source.clip = null;
                }
            }
        }

        // ロードしたAddressablesのハンドルを解放
        foreach (var pair in seHandles)
        {
            if (pair.Value.IsValid())
            {
                Addressables.Release(pair.Value);
            }
        }

        seHandles.Clear();
        seDictionary.Clear();
        loadingSE.Clear();
        pendingPlayCounts.Clear();

        instance = null;
    }
}
