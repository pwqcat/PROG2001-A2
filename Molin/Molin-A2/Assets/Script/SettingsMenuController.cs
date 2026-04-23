using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsMenuController : MonoBehaviour
{
    [Header("UI 控件绑定")]
    [Tooltip("音量控制滑块")]
    public Slider volumeSlider;

    [Tooltip("敌人数量控制滑块 (或其他控件)")]
    public Slider enemyCountSlider;

    [Tooltip("用于控制整个敌人选项变暗的 CanvasGroup (挂在包含滑块和文字的父节点上)")]
    public CanvasGroup enemyCountGroup;

    [Tooltip("返回主菜单/重开按钮")]
    public Button mainMenuButton;

    private void OnEnable()
    {
        // 每次打开设置面板时，都会执行这个逻辑来刷新 UI 状态
        if (GameManager.Instance != null)
        {
            // 判断当前是否处于游玩状态
            bool isPlaying = GameManager.Instance.currentState == GameManager.GameState.Playing;

            // 1. 如果在游戏中，禁用“敌人数量”选项，并使其变半透明暗淡
            if (enemyCountSlider != null)
            {
                enemyCountSlider.interactable = !isPlaying;
            }
            if (enemyCountGroup != null)
            {
                enemyCountGroup.alpha = isPlaying ? 0.4f : 1f; // 游玩时透明度降至 40%
            }

            // 2. 音量滑块初始化（读取当前真实音量）
            if (volumeSlider != null)
            {
                volumeSlider.value = AudioListener.volume;
            }

            // 3. 只有在游玩时，才显示“放弃比赛/返回主界面”按钮
            if (mainMenuButton != null)
            {
                mainMenuButton.gameObject.SetActive(isPlaying);
            }
        }
    }

    // 给 UI Slider 的 OnValueChanged 事件调用的公开方法
    public void OnVolumeChanged(float newVolume)
    {
        // 全局改变所有声音的音量
        AudioListener.volume = newVolume;
    }

    // 给“返回主菜单”按钮的 OnClick 事件调用的方法
    public void ReturnToMainMenu()
    {
        // 直接调用 GameManager 的重启方法，因为我们是无缝场景
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RestartGame();
        }
    }
}