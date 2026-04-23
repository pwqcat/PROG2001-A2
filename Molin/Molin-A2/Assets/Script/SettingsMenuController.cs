using UnityEngine;
using UnityEngine.UI;

public class SettingsMenuController : MonoBehaviour
{
    [Header("UI 控件绑定")]
    [Tooltip("音量控制滑块")]
    public Slider volumeSlider;

    [Tooltip("返回主菜单/重开按钮")]
    public Button mainMenuButton;

    private void OnEnable()
    {
        // 每次打开设置面板时，简单粗暴地同步一下当前真实音量即可
        if (volumeSlider != null)
        {
            volumeSlider.value = AudioListener.volume;
        }

        // 确保重玩按钮始终显示（不再判断是否在游戏中）
        if (mainMenuButton != null)
        {
            mainMenuButton.gameObject.SetActive(true);
        }
    }

    // 给 UI Slider 的 OnValueChanged 事件调用
    public void OnVolumeChanged(float newVolume)
    {
        AudioListener.volume = newVolume;
    }

    // 给“返回主菜单/重新开始”按钮的 OnClick 事件调用
    public void ReturnToMainMenu()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RestartGame();
        }
    }
}