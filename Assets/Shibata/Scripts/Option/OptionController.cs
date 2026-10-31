
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static AudioManager;

public class OptionController : MonoBehaviour
{
    public static OptionController instance;

    public enum Mode
    {
        MASTER,
        BGM,
        SE,
        VOICE,
    }

    [Header("Option Settings")]
    [SerializeField] private Canvas opCanvas;
    [SerializeField] private Mode mode;
    [SerializeField] private bool isActive = false;

    [Header("Mute Icons")]
    [SerializeField] private Sprite defSprite;
    [SerializeField] private Sprite muteSprite;

    // 0: MASTER, 1: BGM, 2: SE
    [SerializeField] private Image[] muteIcons = new Image[3];

    [Header("Volume Sliders")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider seSlider;
    [SerializeField] private Slider voiceSlider;

    private bool isMasterMute = false;
    private bool isBGMMute = false;
    private bool isSEMute = false;
    private bool isVoiceMute = false;

    private Vector3 mDefPos;
    private Vector3 bDefPos;
    private Vector3 sDefPos;
    private Vector3 vDefPos;

    private Vector3 mDefScale;
    private Vector3 bDefScale;
    private Vector3 sDefScale;
    private Vector3 vDefScale;

    private float saveMasterVol;
    private float saveBgmVol;
    private float saveSeVol;
    private float saveVoiceVol;

    private void Awake()
    {
        // OptionControllerを1個だけ残す
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        // シーン間で保持
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (opCanvas == null)
        {
            Debug.LogError(
                "OptionController : OptionCanvasが設定されていません。"
            );
            return;
        }

        SetupOptionUI();

        // 最初はオプションを閉じる
        opCanvas.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ChangeOptionManu();
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        // Sliderのイベントを解除
        if (masterSlider != null)
            masterSlider.onValueChanged.RemoveListener(SetMasterVolume);

        if (bgmSlider != null)
            bgmSlider.onValueChanged.RemoveListener(SetBGMVolume);

        if (seSlider != null)
            seSlider.onValueChanged.RemoveListener(SetSEVolume);

        if(voiceSlider != null)
            voiceSlider.onValueChanged.RemoveListener(SetVoiceVolume);

        if (instance == this)
            instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        Debug.Log("Scene Loaded : " + scene.name);

        isActive = false;

        if (opCanvas != null)
            opCanvas.gameObject.SetActive(false);

        if (scene.name == "MainTitle")
        {
            BGMController.PlayBGM(BGMController.BGMType.Title);
        }
    }

    //==================================================
    // 初期設定
    //==================================================

    private void SetupOptionUI()
    {
        if (opCanvas == null)
            return;

        if (opCanvas.transform.childCount <= 1)
        {
            Debug.LogError(
                "OptionController : OptionCanvasの子構成が想定と異なります。"
            );
            return;
        }

        // ミュートアイコンの初期位置・スケールを保存
        if (muteIcons != null && muteIcons.Length >= 3 &&
            muteIcons[0] != null &&
            muteIcons[1] != null &&
            muteIcons[2] != null)
        {
            mDefPos = muteIcons[0].transform.localPosition;
            bDefPos = muteIcons[1].transform.localPosition;
            sDefPos = muteIcons[2].transform.localPosition;

            mDefScale = muteIcons[0].transform.localScale;
            bDefScale = muteIcons[1].transform.localScale;
            sDefScale = muteIcons[2].transform.localScale;
        }

        // 保存音量の初期値
        saveMasterVol = masterVolume;
        saveBgmVol = bgmVolume;
        saveSeVol = seVolume;

        // Sliderを初期化
        SetupVolumeSliders();
    }

    private void SetupVolumeSliders()
    {
        SetupSlider(masterSlider, SetMasterVolume, masterVolume);
        SetupSlider(bgmSlider, SetBGMVolume, bgmVolume);
        SetupSlider(seSlider, SetSEVolume, seVolume);
        SetupSlider(voiceSlider, SetVoiceVolume, voiceVolume);
    }

    private void SetupSlider(
        Slider slider,
        UnityEngine.Events.UnityAction<float> callback,
        float volume)
    {
        if (slider == null)
        {
            Debug.LogWarning(
                "OptionController : Sliderが設定されていません。"
            );
            return;
        }

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;

        // イベントの二重登録を防ぐ
        slider.onValueChanged.RemoveListener(callback);
        slider.onValueChanged.AddListener(callback);

        // 初期値の設定時にイベントを呼び出さない
        slider.SetValueWithoutNotify(Mathf.Clamp01(volume));
    }

    //==================================================
    // Sliderによる音量調整
    //==================================================

    public void SetMasterVolume(float value)
    {
        masterVolume = Mathf.Clamp01(value);

        if (masterVolume > 0f)
        {
            saveMasterVol = masterVolume;

            if (isMasterMute)
                RestoreMuteIcon(Mode.MASTER);

            isMasterMute = false;
        }
    }

    public void SetBGMVolume(float value)
    {
        bgmVolume = Mathf.Clamp01(value);

        if (bgmVolume > 0f)
        {
            saveBgmVol = bgmVolume;

            if (isBGMMute)
                RestoreMuteIcon(Mode.BGM);

            isBGMMute = false;
        }
    }

    public void SetSEVolume(float value)
    {
        seVolume = Mathf.Clamp01(value);

        if (seVolume > 0f)
        {
            saveSeVol = seVolume;

            if (isSEMute)
                RestoreMuteIcon(Mode.SE);

            isSEMute = false;
        }
    }

    public void SetVoiceVolume(float value)
    {
        voiceVolume = Mathf.Clamp01(value);

        if (voiceVolume > 0f)
        {
            saveVoiceVol = voiceVolume;

            if (isSEMute)
                RestoreMuteIcon(Mode.VOICE);

            isVoiceMute = false;
        }
    }

    private void UpdateVolumeSliders()
    {
        if (masterSlider != null)
            masterSlider.SetValueWithoutNotify(masterVolume);

        if (bgmSlider != null)
            bgmSlider.SetValueWithoutNotify(bgmVolume);

        if (seSlider != null)
            seSlider.SetValueWithoutNotify(seVolume);

        if (voiceSlider != null)
            voiceSlider.SetValueWithoutNotify(voiceVolume);
    }

    //==================================================
    // キーボードによる音量調整（既存機能）
    //==================================================

    public void SetVol(Mode type, bool increase)
    {
        float delta = increase ? 0.1f : -0.1f;

        switch (type)
        {
            case Mode.MASTER:
                masterVolume = Mathf.Clamp01(masterVolume + delta);

                if (masterVolume > 0f)
                {
                    saveMasterVol = masterVolume;
                    isMasterMute = false;
                    RestoreMuteIcon(Mode.MASTER);
                }
                break;

            case Mode.BGM:
                bgmVolume = Mathf.Clamp01(bgmVolume + delta);

                if (bgmVolume > 0f)
                {
                    saveBgmVol = bgmVolume;
                    isBGMMute = false;
                    RestoreMuteIcon(Mode.BGM);
                }
                break;

            case Mode.SE:
                seVolume = Mathf.Clamp01(seVolume + delta);

                if (seVolume > 0f)
                {
                    saveSeVol = seVolume;
                    isSEMute = false;
                    RestoreMuteIcon(Mode.SE);
                }
                break;

            case Mode.VOICE:
                voiceVolume = Mathf.Clamp01(voiceVolume + delta);

                if (voiceVolume > 0f)
                {
                    saveVoiceVol = voiceVolume;
                    isVoiceMute = false;
                    RestoreMuteIcon(Mode.VOICE);
                }
                break;
        }

        UpdateVolumeSliders();
    }

    //==================================================
    // ミュート切り替え
    //==================================================

    public void ToggleMute(Mode type)
    {
        switch (type)
        {
            case Mode.MASTER:
                if (!isMasterMute)
                {
                    saveMasterVol = masterVolume;

                    masterVolume = 0f;
                    isMasterMute = true;

                    ApplyMuteIcon(Mode.MASTER);
                }
                else
                {
                    masterVolume = Mathf.Clamp01(saveMasterVol);
                    isMasterMute = false;

                    RestoreMuteIcon(Mode.MASTER);
                }
                break;

            case Mode.BGM:
                if (!isBGMMute)
                {
                    saveBgmVol = bgmVolume;

                    bgmVolume = 0f;
                    isBGMMute = true;

                    ApplyMuteIcon(Mode.BGM);
                }
                else
                {
                    bgmVolume = Mathf.Clamp01(saveBgmVol);
                    isBGMMute = false;

                    RestoreMuteIcon(Mode.BGM);
                }
                break;

            case Mode.SE:
                if (!isSEMute)
                {
                    saveSeVol = seVolume;

                    seVolume = 0f;
                    isSEMute = true;

                    ApplyMuteIcon(Mode.SE);
                }
                else
                {
                    seVolume = Mathf.Clamp01(saveSeVol);
                    isSEMute = false;

                    RestoreMuteIcon(Mode.SE);
                }
                break;

            case Mode.VOICE:
                if (!isVoiceMute)
                {
                    saveVoiceVol = voiceVolume;

                    voiceVolume = 0f;
                    isVoiceMute = true;

                    ApplyMuteIcon(Mode.VOICE);
                }
                else
                {
                    seVolume = Mathf.Clamp01(saveVoiceVol);
                    isVoiceMute = false;

                    RestoreMuteIcon(Mode.VOICE);
                }
                break;
        }

        UpdateVolumeSliders();
    }

    private void ApplyMuteIcon(Mode type)
    {
        int index = (int)type;

        if (!IsMuteIconAvailable(index))
            return;

        float addPos = 25f;
        float addScale = 0.4f;

        Image iconImage = muteIcons[index];

        iconImage.sprite = muteSprite;

        iconImage.transform.localPosition = new Vector3(
            GetDefaultPosition(type).x - addPos,
            GetDefaultPosition(type).y,
            GetDefaultPosition(type).z
        );

        iconImage.transform.localScale = new Vector3(
            GetDefaultScale(type).x - addScale,
            GetDefaultScale(type).y,
            GetDefaultScale(type).z
        );
    }

    private void RestoreMuteIcon(Mode type)
    {
        int index = (int)type;

        if (!IsMuteIconAvailable(index))
            return;

        muteIcons[index].sprite = defSprite;
        muteIcons[index].transform.localPosition =
            GetDefaultPosition(type);
        muteIcons[index].transform.localScale =
            GetDefaultScale(type);
    }

    private bool IsMuteIconAvailable(int index)
    {
        return muteIcons != null &&
               index >= 0 &&
               index < muteIcons.Length &&
               muteIcons[index] != null;
    }

    private Vector3 GetDefaultPosition(Mode type)
    {
        switch (type)
        {
            case Mode.MASTER: return mDefPos;
            case Mode.BGM: return bDefPos;
            case Mode.SE: return sDefPos;
            case Mode.VOICE: return vDefPos;
            default: return Vector3.zero;
        }
    }

    private Vector3 GetDefaultScale(Mode type)
    {
        switch (type)
        {
            case Mode.MASTER: return mDefScale;
            case Mode.BGM: return bDefScale;
            case Mode.SE: return sDefScale;
            case Mode.VOICE: return vDefScale;
            default: return Vector3.one;
        }
    }

    //==================================================
    // オプション開閉
    //==================================================

    public void ChangeOptionManu()
    {
        if (opCanvas == null)
        {
            Debug.LogError(
                "OptionController : OptionCanvasがありません。"
            );
            return;
        }

        if (isActive)
        {
            isActive = false;

            opCanvas.gameObject.SetActive(false);
            SEController.UnmuteSE();
        }
        else
        {
            opCanvas.gameObject.SetActive(true);

            isActive = true;
            SEController.MuteSE();

            // 開くたびに現在の音量をSliderへ同期
            UpdateVolumeSliders();
        }
    }
}
