using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class GameOverUIController : MonoBehaviour
{
    [Header("UI 组件绑定")]
    [Tooltip("挂载在 GameOverPanel 上的 CanvasGroup（用于控制整体透明度）")]
    public CanvasGroup panelCanvasGroup;

    [Tooltip("结算大字（VICTORY / ELIMINATED）的 Transform")]
    public RectTransform messageTextTransform;

    [Tooltip("包含返回按钮的父节点 CanvasGroup（用于控制按钮的渐显和防误触）")]
    public CanvasGroup buttonCanvasGroup;

    [Header("动画参数")]
    public float fadeDuration = 0.5f;
    [Tooltip("大字弹出后，等待多久再显示按钮")]
    public float buttonDelay = 1.2f;

    private void OnEnable()
    {
        // 每次面板被 GameManager 激活时，自动播放演出动画
        StartCoroutine(PlayGameOverAnimation());
    }

    private IEnumerator PlayGameOverAnimation()
    {
        // 1. 初始状态重置（全透明、文字缩小到 0、按钮禁用）
        if (panelCanvasGroup != null) panelCanvasGroup.alpha = 0f;
        if (messageTextTransform != null) messageTextTransform.localScale = Vector3.zero;
        if (buttonCanvasGroup != null)
        {
            buttonCanvasGroup.alpha = 0f;
            buttonCanvasGroup.interactable = false;
            buttonCanvasGroup.blocksRaycasts = false;
        }

        // 2. 屏幕暗转 & 大字弹性弹出
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);

            // 背景渐黑
            if (panelCanvasGroup != null) panelCanvasGroup.alpha = t;

            // 文字弹性放大 (Overshoot 效果)
            float popCurve = 1f - Mathf.Pow(1f - t, 3f);
            if (messageTextTransform != null)
            {
                messageTextTransform.localScale = Vector3.LerpUnclamped(Vector3.zero, Vector3.one * 1.1f, popCurve);
            }

            yield return null;
        }

        // 确保文字最终大小恢复为 1
        if (messageTextTransform != null) messageTextTransform.localScale = Vector3.one;

        // 3. 悬念留白：让玩家听完音效、看清文字
        yield return new WaitForSecondsRealtime(buttonDelay);

        // 4. 返回按钮平滑浮现
        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            if (buttonCanvasGroup != null) buttonCanvasGroup.alpha = t;
            yield return null;
        }

        // 5. 动画结束，正式允许玩家点击按钮
        if (buttonCanvasGroup != null)
        {
            buttonCanvasGroup.interactable = true;
            buttonCanvasGroup.blocksRaycasts = true;
        }
    }
}