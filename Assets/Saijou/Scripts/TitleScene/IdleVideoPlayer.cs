using System.Collections;
using UnityEngine;
using UnityEngine.Video;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem; // 新Input Systemを使っている場合
#endif

/// <summary>
/// 一定時間操作がなかったら、mp4動画をフェードインで自動再生するスクリプト。
/// 何か操作(キー・マウス・タッチ)があれば、フェードアウトして元の画面に戻す。
/// </summary>
public class IdleVideoPlayer : MonoBehaviour
{
    [Header("放置時間の設定")]
    [Tooltip("この秒数だけ操作がなかったら動画を再生する")]
    [SerializeField] private float idleTimeSeconds = 5f; // ← インスペクターでいつでも変更可能

    [Header("動画の設定")]
    [Tooltip("再生に使うVideoPlayer(mp4のVideo Clip、またはURLを設定しておく)")]
    [SerializeField] private VideoPlayer videoPlayer;

    [Tooltip("動画を表示するオブジェクト(RawImageなど)。フェードさせるために必須")]
    [SerializeField] private GameObject videoDisplay;

    [Tooltip("動画をループ再生するか")]
    [SerializeField] private bool loopVideo = true;

    [Tooltip("操作があったとき、動画を止めて元の画面に戻すか")]
    [SerializeField] private bool stopOnInput = true;

    [Header("フェードの設定")]
    [Tooltip("動画が現れるまでの時間(秒)。0ならパッと切り替わる")]
    [SerializeField] private float fadeInDuration = 1f;

    [Tooltip("動画が消えるまでの時間(秒)。0ならパッと切り替わる")]
    [SerializeField] private float fadeOutDuration = 0.5f;

    // 最後に操作があってからの経過時間
    private float idleTimer = 0f;

    // 動画の再生処理中かどうか(フェードイン開始?フェードアウト完了まで true)
    private bool isPlayingVideo = false;

    // フェードアウト中かどうか
    private bool isFadingOut = false;

    // フェード用のCanvasGroup(videoDisplayに自動で追加される)
    private CanvasGroup canvasGroup;

    // 実行中のフェード処理(途中で止めるために保持)
    private Coroutine fadeCoroutine;

    // 旧Input Manager用:前フレームのマウス位置
    private Vector3 lastMousePosition;

    private void Start()
    {
        if (videoPlayer == null || videoDisplay == null)
        {
            Debug.LogError("[IdleVideoPlayer] VideoPlayer と Video Display の両方をインスペクターで設定してください。");
            enabled = false;
            return;
        }

        // 自動再生はオフにして、このスクリプトで制御する
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = loopVideo;

        // 動画が最後まで再生されたとき(ループOFFの場合)に呼ばれる
        videoPlayer.loopPointReached += OnVideoFinished;

        // フェード用にCanvasGroupを取得(なければ自動で追加)
        canvasGroup = videoDisplay.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = videoDisplay.AddComponent<CanvasGroup>();
        }

        // 最初は透明にしておく(SetActiveで消すとフェードできないので、アルファ値で隠す)
        videoDisplay.SetActive(true);
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false; // 透明な動画が後ろのボタンなどをブロックしないように
        canvasGroup.interactable = false;

#if !ENABLE_INPUT_SYSTEM
        lastMousePosition = Input.mousePosition;
#endif
    }

    private void Update()
    {
        // 今フレームに何か操作があったか?
        if (AnyInputDetected())
        {
            // 操作があったのでタイマーをリセット
            idleTimer = 0f;

            // 動画再生中なら、フェードアウトして元の画面に戻す
            if (isPlayingVideo && !isFadingOut && stopOnInput)
            {
                StopVideo();
            }
            return;
        }

        // すでに動画の再生処理中なら、これ以上タイマーを進めない
        if (isPlayingVideo) return;

        // 操作がないので時間を加算
        idleTimer += Time.unscaledDeltaTime;

        // 設定した時間を超えたら動画を再生
        if (idleTimer >= idleTimeSeconds)
        {
            PlayVideo();
        }
    }

    /// <summary>動画を再生し、フェードインで表示する</summary>
    private void PlayVideo()
    {
        isPlayingVideo = true;
        isFadingOut = false;
        videoPlayer.isLooping = loopVideo; // 実行中にインスペクターで変えても反映
        StartFade(PlayRoutine());
    }

    /// <summary>動画をフェードアウトで消し、止める</summary>
    private void StopVideo()
    {
        isFadingOut = true;
        StartFade(StopRoutine());
    }

    /// <summary>準備 → 再生 → フェードイン</summary>
    private IEnumerator PlayRoutine()
    {
        // 再生開始時に黒い画面が一瞬映らないよう、先に動画を準備しておく
        videoPlayer.Prepare();
        while (!videoPlayer.isPrepared)
        {
            yield return null;
        }

        videoPlayer.Play();
        yield return FadeTo(1f, fadeInDuration);
    }

    /// <summary>フェードアウト → 停止</summary>
    private IEnumerator StopRoutine()
    {
        yield return FadeTo(0f, fadeOutDuration);

        videoPlayer.Stop();
        isPlayingVideo = false;
        isFadingOut = false;
        idleTimer = 0f; // ここからまた放置時間をカウント
    }

    /// <summary>
    /// 現在のアルファ値から目標値まで、指定した時間でなめらかに変化させる。
    /// フェードの途中で切り替わっても、今の透明度から続きを動かせる。
    /// </summary>
    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        if (duration <= 0f)
        {
            canvasGroup.alpha = targetAlpha;
            yield break;
        }

        while (!Mathf.Approximately(canvasGroup.alpha, targetAlpha))
        {
            canvasGroup.alpha = Mathf.MoveTowards(
                canvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime / duration);
            yield return null;
        }
        canvasGroup.alpha = targetAlpha;
    }

    /// <summary>実行中のフェード処理があれば止めてから、新しい処理を開始する</summary>
    private void StartFade(IEnumerator routine)
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }
        fadeCoroutine = StartCoroutine(routine);
    }

    /// <summary>動画が最後まで再生されたとき(ループOFF時)</summary>
    private void OnVideoFinished(VideoPlayer vp)
    {
        // 終わったらフェードアウトして元の画面に戻る
        if (isPlayingVideo && !isFadingOut)
        {
            StopVideo();
        }
    }

    /// <summary>キー・マウス・タッチなどの操作があったかを判定する</summary>
    private bool AnyInputDetected()
    {
#if ENABLE_INPUT_SYSTEM
        // ---- 新Input Systemの場合 ----
        // キーボード
        if (Keyboard.current != null && Keyboard.current.anyKey.isPressed) return true;

        // マウス(移動・クリック・ホイール)
        if (Mouse.current != null)
        {
            if (Mouse.current.delta.ReadValue().sqrMagnitude > 0.01f) return true;
            if (Mouse.current.leftButton.isPressed ||
                Mouse.current.rightButton.isPressed ||
                Mouse.current.middleButton.isPressed) return true;
            if (Mouse.current.scroll.ReadValue().sqrMagnitude > 0.01f) return true;
        }

        // タッチ
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed) return true;

        return false;
#else
        // ---- 旧Input Managerの場合 ----
        // キーボード・マウスボタン・ゲームパッドボタン
        if (Input.anyKey) return true;

        // タッチ
        if (Input.touchCount > 0) return true;

        // マウス移動(前フレームと位置が違えば操作あり)
        Vector3 currentMousePosition = Input.mousePosition;
        bool mouseMoved = currentMousePosition != lastMousePosition;
        lastMousePosition = currentMousePosition;
        if (mouseMoved) return true;

        // マウスホイール
        if (Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f) return true;

        return false;
#endif
    }

    private void OnDestroy()
    {
        // イベント登録の解除(お作法)
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoFinished;
        }
    }
}