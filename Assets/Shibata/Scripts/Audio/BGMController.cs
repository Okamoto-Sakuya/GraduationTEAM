using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using static AudioManager;

public class BGMController : MonoBehaviour
{
    public static BGMController instance;

    //============================================================
    // BGMデータ
    //============================================================

    [System.Serializable]
    public class BGMData
    {
        public BGMType type;

        [Header("AddressableのBGM")]
        public AssetReferenceT<AudioClip> clip;
    }

    //============================================================
    // BGMの種類
    //============================================================

    public enum BGMType
    {
        Test,
    }

    //============================================================
    // BGM管理
    //============================================================

    [SerializeField]
    List<BGMData> bgmList = new();

    // ロード済みのAudioClip
    Dictionary<BGMType, AudioClip> bgmDictionary = new();

    // Addressablesのロードハンドル
    Dictionary<BGMType, AsyncOperationHandle<AudioClip>>
        bgmHandles = new();

    // ロード中のBGM
    HashSet<BGMType> loadingBGM = new();

    // ロード中に要求されたBGM
    BGMType? pendingBGM = null;

    // 現在再生中のBGM
    BGMType? currentBGM = null;

    //============================================================
    // AudioSource
    //============================================================

    AudioSource audioSource;

    //============================================================
    // 初期化
    //============================================================

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = true;

        // AddressablesからBGMをロード
        LoadAllBGM();
    }

    //============================================================
    // Addressablesロード
    //============================================================

    /// <summary>
    /// Inspectorで設定したBGMをすべて非同期ロードする
    /// </summary>
    void LoadAllBGM()
    {
        HashSet<BGMType> registeredTypes = new();

        foreach (BGMData data in bgmList)
        {
            if (data == null || data.clip == null)
                continue;

            if (!registeredTypes.Add(data.type))
            {
                Debug.LogWarning(
                    $"BGMTypeが重複しています: {data.type}");
                continue;
            }

            BGMType type = data.type;

            loadingBGM.Add(type);

            AsyncOperationHandle<AudioClip> handle =
                Addressables.LoadAssetAsync<AudioClip>(data.clip);

            bgmHandles[type] = handle;

            handle.Completed += operation =>
            {
                if (this == null)
                    return;

                loadingBGM.Remove(type);

                if (operation.Status == AsyncOperationStatus.Succeeded)
                {
                    bgmDictionary[type] = operation.Result;

                    Debug.Log($"BGMロード成功: {type}");

                    // ロード中に要求されたBGMを再生
                    if (pendingBGM.HasValue &&
                        pendingBGM.Value == type)
                    {
                        pendingBGM = null;
                        PlayBGM(type);
                    }
                }
                else
                {
                    Debug.LogError(
                        $"BGMロード失敗: {type}");

                    if (pendingBGM.HasValue &&
                        pendingBGM.Value == type)
                    {
                        pendingBGM = null;
                    }
                }
            };
        }
    }

    //============================================================
    // Start
    //============================================================

    void Start()
    {
        // 初期BGM
        StartCoroutine(Fade(3.0f, true));
        PlayBGM(BGMType.Test);
    }

    //============================================================
    // Update
    //============================================================

    void Update()
    {
        if (audioSource == null)
            return;

        // 全体音量を適用
        audioSource.volume = masterVolume * bgmVolume;
    }

    //============================================================
    // BGM再生
    //============================================================

    /// <summary>
    /// BGM再生
    /// </summary>
    public static void PlayBGM(BGMType type)
    {
        if (instance == null)
        {
            Debug.LogWarning("BGMManagerが存在しません");
            return;
        }

        BGMController manager = instance;

        // すでに同じBGMを再生中なら何もしない
        if (manager.currentBGM.HasValue &&
            manager.currentBGM.Value == type &&
            manager.audioSource.isPlaying)
        {
            return;
        }

        // ロード済みか確認
        if (!manager.bgmDictionary.TryGetValue(
            type, out AudioClip clip))
        {
            // ロード中なら再生要求を保留
            if (manager.loadingBGM.Contains(type))
            {
                // 最後に要求されたBGMを優先
                manager.pendingBGM = type;
                return;
            }

            Debug.LogWarning(
                $"BGM未登録またはロード失敗: {type}");
            return;
        }

        // 現在のBGMを停止
        manager.audioSource.Stop();

        // BGMを設定
        manager.audioSource.clip = clip;
        manager.audioSource.loop = true;

        // 再生
        manager.audioSource.Play();

        manager.currentBGM = type;

        Debug.Log($"BGM再生: {type}");
    }

    //============================================================
    // BGM停止
    //============================================================

    /// <summary>
    /// BGMを停止する
    /// </summary>
    public static void StopBGM()
    {
        if (instance == null)
        {
            Debug.LogWarning("BGMManagerが存在しません");
            return;
        }

        instance.audioSource.Stop();
        instance.audioSource.clip = null;

        instance.currentBGM = null;
        instance.pendingBGM = null;
    }

    //============================================================
    // 終了処理・Addressables解放
    //============================================================

    void OnDestroy()
    {
        if (instance != this)
            return;

        // 再生中のBGMを停止
        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = null;
        }

        // ロードしたAddressablesのハンドルを解放
        foreach (var pair in bgmHandles)
        {
            if (pair.Value.IsValid())
            {
                Addressables.Release(pair.Value);
            }
        }

        bgmHandles.Clear();
        bgmDictionary.Clear();
        loadingBGM.Clear();

        pendingBGM = null;
        currentBGM = null;

        instance = null;
    }

    //============================================================
    // フェードイン・アウト
    //============================================================

    public static IEnumerator Fade(float duration, bool isIn)
    {
        float elapsed = 0;

        if (isIn)
        {
            AudioManager.bgmVolume = 0f;

            while(elapsed < duration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / duration);
                AudioManager.bgmVolume = Mathf.Lerp(0f,1f,t);

                yield return null;
            }
        }
        else
        {
            float startVol = AudioManager.bgmVolume;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / duration);
                AudioManager.bgmVolume = Mathf.Lerp(startVol, 0f, t);

                yield return null;
            }
        }

        yield break;
    }
}
