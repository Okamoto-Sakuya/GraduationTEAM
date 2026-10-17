
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class MenuPanelAnimator : MonoBehaviour
{
    [Header("ボタン設定")]
    // メニューを開くボタン
    [SerializeField] private GameObject menuButton;

    // メニューを閉じるボタン
    [SerializeField] private GameObject backButton;

    // Panelの子供になっている3つのボタン
    [SerializeField] private RectTransform[] menuItems;

    [Header("アニメーション設定")]
    // ボタンが右から移動してくる距離
    [SerializeField] private float slideDistance = 200f;

    // アニメーションにかかる時間（秒）
    [SerializeField] private float animationDuration = 0.35f;

    // ボタン同士のアニメーション開始時間のずれ
    [SerializeField] private float staggerDelay = 0.08f;

    // 各ボタンの本来の位置
    private Vector2[] targetPositions;

    // 各ボタンのCanvasGroup（透明度を制御する）
    private CanvasGroup[] canvasGroups;

    // アニメーション中かどうか
    private bool isAnimating = false;

    // メニューが開いているかどうか
    private bool isOpen = false;

    // 現在実行中のアニメーション
    private Coroutine animationCoroutine;

    // メニューボタンの位置を保存する
    private RectTransform menuButtonRect;

    // 戻るボタンのRectTransform
    private RectTransform backButtonRect;
    private void Awake()
    {

        // メニューボタンと戻るボタンのRectTransformを取得
        menuButtonRect = menuButton.GetComponent<RectTransform>();
        backButtonRect = backButton.GetComponent<RectTransform>();

        // 戻るボタンをメニューボタンと同じ位置にする
        backButtonRect.position = menuButtonRect.position;
        backButtonRect.rotation = menuButtonRect.rotation;
        backButtonRect.sizeDelta = menuButtonRect.sizeDelta;

        // ボタンの位置と透明度を管理する配列を用意
        targetPositions = new Vector2[menuItems.Length];
        canvasGroups = new CanvasGroup[menuItems.Length];

        for (int i = 0; i < menuItems.Length; i++)
        {
            // ボタン本来の位置を保存
            targetPositions[i] = menuItems[i].anchoredPosition;

            // CanvasGroupがなければ自動で追加する
            canvasGroups[i] = menuItems[i].GetComponent<CanvasGroup>();

            if (canvasGroups[i] == null)
            {
                canvasGroups[i] =
                    menuItems[i].gameObject.AddComponent<CanvasGroup>();
            }

            // 初期状態は右側にずらして透明にする
            menuItems[i].anchoredPosition =
                targetPositions[i] + Vector2.right * slideDistance;

            canvasGroups[i].alpha = 0f;

            // 透明なボタンをクリックできないようにする
            canvasGroups[i].interactable = false;
            canvasGroups[i].blocksRaycasts = false;
        }

        // 最初はメニューを閉じた状態にする
        menuButton.SetActive(true);
        backButton.SetActive(false);
    }

    // メニューボタンのOnClickから呼び出す
    public void OpenMenu()
    {
        Debug.Log("メニューボタンが押されました");
        // アニメーション中の連続入力を防ぐ
        if (isAnimating || isOpen)
            return;

        isOpen = true;

        // メニューを開くアニメーションを開始
        animationCoroutine = StartCoroutine(AnimateMenu(true));
    }

    // 戻るボタンのOnClickから呼び出す
    public void CloseMenu()
    {
        // アニメーション中の連続入力を防ぐ
        if (isAnimating || !isOpen)
            return;

        isOpen = false;

        // メニューを閉じるアニメーションを開始
        animationCoroutine = StartCoroutine(AnimateMenu(false));
    }

    // ボタンをスライドさせながらフェードイン・アウトする
    private IEnumerator AnimateMenu(bool open)
    {
        isAnimating = true;

        // アニメーション開始と同時にボタンを切り替える
        if (open)
        {
            // メニューを開くとき
            menuButton.SetActive(false);
            backButton.SetActive(true);
        }
        else
        {
            // メニューを閉じるとき
            backButton.SetActive(false);
            menuButton.SetActive(true);
        }

        // ボタンを順番にアニメーションさせる
        for (int i = 0; i < menuItems.Length; i++)
        {
            RectTransform item = menuItems[i];
            CanvasGroup group = canvasGroups[i];

            // 表示するときは右から本来の位置へ
            // 閉じるときは本来の位置から右へ移動
            Vector2 startPosition;
            Vector2 endPosition;

            if (open)
            {
                startPosition =
                    targetPositions[i] + Vector2.right * slideDistance;

                endPosition = targetPositions[i];

                group.alpha = 0f;
            }
            else
            {
                startPosition = targetPositions[i];

                endPosition =
                    targetPositions[i] + Vector2.right * slideDistance;
            }

            item.anchoredPosition = startPosition;

            // アニメーション中はボタンを操作できなくする
            group.interactable = false;
            group.blocksRaycasts = false;

            // 各ボタンのアニメーションを実行
            float elapsed = 0f;

            while (elapsed < animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;

                // 0～1の進行度を計算
                float t = Mathf.Clamp01(elapsed / animationDuration);

                // SmoothStepで動きを滑らかにする
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                // 位置を補間してスライドさせる
                item.anchoredPosition =
                    Vector2.Lerp(startPosition, endPosition, smoothT);

                // 透明度も同時に変化させる
                group.alpha = open ? smoothT : 1f - smoothT;

                yield return null;
            }

            // 最終位置と透明度を確実に設定
            item.anchoredPosition = endPosition;
            group.alpha = open ? 1f : 0f;

            // 次のボタンを少し遅らせて動かす
            if (staggerDelay > 0f && i < menuItems.Length - 1)
            {
                yield return new WaitForSecondsRealtime(staggerDelay);
            }
        }

        // 開いた状態ならボタンを操作可能にする
        for (int i = 0; i < menuItems.Length; i++)
        {
            canvasGroups[i].interactable = open;
            canvasGroups[i].blocksRaycasts = open;
        }

        isAnimating = false;
        animationCoroutine = null;
    }
}
