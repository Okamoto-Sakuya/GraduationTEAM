using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using static AudioManager;
using static SEController;

public class SEController : MonoBehaviour
{
    public static SEController Inst_SEManager;

    //============================================================
    // SEデータ
    //============================================================

    [System.Serializable]
    public class SEData
    {
        public SEType type;
        public AudioClip clip;
    }

    //============================================================
    // SEの種類
    //============================================================

    public enum SEType
    {
        Click,
    }

    //============================================================
    // SE管理
    //============================================================

    [SerializeField]
    private List<SEData> seList = new();

    private Dictionary<SEType, AudioClip> seDictionary = new();

    //============================================================
    // ミュートするSE
    //============================================================

    [Header("ミュートするSE")]

    /// <summary>
    /// MuteSE() / UnmuteSE() の対象となるSE
    /// Inspectorから設定する
    /// </summary>
    [SerializeField]
    private SEType[] muteSE;

    /// <summary>
    /// muteSEが現在ミュートされているか
    /// </summary>
    private bool isMuteSE = false;

    //============================================================
    // 複数再生可能なSE
    //============================================================

    [Header("複数再生していいSE")]

    /// <summary>
    /// 同じSEを連続して複数再生してもいいSE
    /// </summary>
    [SerializeField]
    private SEType[] consecutiveSE;

    //============================================================
    // AudioSource
    //============================================================

    // SEを同時に10個まで再生する
    private const int MaxSECount = 10;

    private AudioSource[] audioSources;

    // AudioSourceの再生順を管理する
    // 0 = 最も古い
    private Queue<AudioSource> playOrder = new();

    //============================================================
    // 初期化
    //============================================================

    private void Awake()
    {
        DontDestroyOnLoad(this.gameObject);

        if (Inst_SEManager != null)
        {
            Destroy(gameObject);
            return;
        }

        Inst_SEManager = this;

        //========================================================
        // AudioSourceを作成
        //========================================================

        audioSources = new AudioSource[MaxSECount];

        for (int i = 0; i < MaxSECount; i++)
        {
            audioSources[i] = gameObject.AddComponent<AudioSource>();

            audioSources[i].playOnAwake = false;
            audioSources[i].loop = false;
        }

        //========================================================
        // Inspectorで設定した内容をDictionaryへ変換
        //========================================================

        foreach (SEData data in seList)
        {
            if (data.clip != null)
            {
                seDictionary[data.type] = data.clip;
            }
        }
    }

    //============================================================
    // Update
    //============================================================

    private void Update()
    {
        if (audioSources == null)
            return;

        // 全AudioSourceに音量を適用
        foreach (AudioSource source in audioSources)
        {
            source.volume = masterVolume * seVolume;
        }

        // 再生が終了したAudioSourceをQueueから削除
        RemoveFinishedSources();
    }

    //============================================================
    // SEミュート管理
    //============================================================

    /// <summary>
    /// Inspectorで設定したmuteSEをミュートする
    /// </summary>
    public static void MuteSE()
    {
        if (Inst_SEManager == null)
        {
            Debug.LogWarning("SEManagerが存在しません");
            return;
        }

        // ミュート状態にする
        Inst_SEManager.isMuteSE = true;

        if (Inst_SEManager.muteSE == null)
            return;

        // 現在再生中の対象SEを停止してAudioSourceから外す
        foreach (SEType type in Inst_SEManager.muteSE)
        {
            Inst_SEManager.RemoveSEFromAudioSource(type);
        }
    }

    /// <summary>
    /// muteSEのミュートを解除する
    /// </summary>
    public static void UnmuteSE()
    {
        if (Inst_SEManager == null)
        {
            Debug.LogWarning("SEManagerが存在しません");
            return;
        }

        // ミュート解除
        Inst_SEManager.isMuteSE = false;
    }

    /// <summary>
    /// 指定したSEが現在ミュート対象になっているか
    /// </summary>
    private static bool IsMuteSE(SEType type)
    {
        if (Inst_SEManager == null)
            return false;

        // 現在MuteSE()が呼ばれていなければ再生可能
        if (!Inst_SEManager.isMuteSE)
            return false;

        if (Inst_SEManager.muteSE == null)
            return false;

        // muteSEに指定されているか確認
        foreach (SEType muteType in Inst_SEManager.muteSE)
        {
            if (muteType == type)
            {
                return true;
            }
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
        if (Inst_SEManager == null)
            return false;

        if (Inst_SEManager.consecutiveSE == null)
            return false;

        foreach (SEType consecutiveType in Inst_SEManager.consecutiveSE)
        {
            if (consecutiveType == type)
            {
                return true;
            }
        }

        return false;
    }

    //============================================================
    // AudioSourceからSEを外す
    //============================================================

    /// <summary>
    /// 指定したSEを再生しているAudioSourceからClipを外す
    /// </summary>
    public void RemoveSEFromAudioSource(SEType type)
    {
        // SETypeに対応するAudioClipを取得
        if (!seDictionary.TryGetValue(type, out AudioClip clip))
        {
            return;
        }

        foreach (AudioSource source in audioSources)
        {
            if (source.clip == clip)
            {
                // 再生停止
                source.Stop();

                // AudioClipを外す
                source.clip = null;

                // Queueから削除
                RemoveSourceFromQueue(source);
            }
        }
    }

    //============================================================
    // 再生終了SEの整理
    //============================================================

    /// <summary>
    /// 再生が終了したAudioSourceを再生順リストから削除
    /// </summary>
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
        Debug.Log("PlaySE:" +  type);

        if (Inst_SEManager == null)
        {
            Debug.LogWarning("SEManagerが存在しません");
            return;
        }

        //========================================================
        // MuteSE()が呼ばれていて、
        // かつmuteSEに登録されているSEなら再生しない
        //========================================================

        if (IsMuteSE(type))
        {
            return;
        }

        //========================================================
        // SEを取得
        //========================================================

        if (!Inst_SEManager.seDictionary.TryGetValue(type, out AudioClip clip))
        {
            Debug.LogWarning($"SE未登録: {type}");
            return;
        }

        //========================================================
        // consecutiveSEに登録されていないSEの場合
        // 同じSEが再生中なら何もしない
        //========================================================

        if (!IsConsecutiveSE(type))
        {
            foreach (AudioSource source in Inst_SEManager.audioSources)
            {
                if (source.isPlaying && source.clip == clip)
                {
                    return;
                }
            }
        }

        //========================================================
        // 空いているAudioSourceを探す
        //========================================================

        foreach (AudioSource source in Inst_SEManager.audioSources)
        {
            if (!source.isPlaying)
            {
                PlayOnSource(source, clip);
                return;
            }
        }

        //========================================================
        // 10個とも使用中
        //========================================================

        if (Inst_SEManager.playOrder.Count > 0)
        {
            // Queueの先頭 = 最も古く再生したAudioSource
            AudioSource oldestSource =
                Inst_SEManager.playOrder.Dequeue();

            oldestSource.Stop();

            PlayOnSource(oldestSource, clip);
        }
    }

    //============================================================
    // AudioSourceで再生
    //============================================================

    /// <summary>
    /// 指定したAudioSourceでSEを再生
    /// </summary>
    private static void PlayOnSource(
        AudioSource source,
        AudioClip clip)
    {
        // 念のため、Queue内に同じAudioSourceが残っていたら削除
        Inst_SEManager.RemoveSourceFromQueue(source);

        source.clip = clip;
        source.volume = masterVolume * seVolume;
        source.Play();

        // 今回再生したAudioSourceを最後尾へ追加
        Inst_SEManager.playOrder.Enqueue(source);
    }

    //============================================================
    // Queue管理
    //============================================================

    /// <summary>
    /// Queueから指定AudioSourceを削除
    /// </summary>
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
    /// 再生中のすべてのSEを停止
    /// </summary>
    public static void StopAllSE()
    {
        if (Inst_SEManager == null)
        {
            Debug.LogWarning("SEManagerが存在しません");
            return;
        }

        // 全AudioSourceを停止
        foreach (AudioSource source in Inst_SEManager.audioSources)
        {
            if (source.isPlaying)
            {
                source.Stop();
            }

            // 再生中のClipも解除
            source.clip = null;
        }

        // 再生順もリセット
        Inst_SEManager.playOrder.Clear();
    }

    //============================================================
    // Addressabe関係
    //============================================================
}