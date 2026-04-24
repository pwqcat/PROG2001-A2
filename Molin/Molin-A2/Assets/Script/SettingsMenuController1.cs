using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class SettingsMenuController : MonoBehaviour
{
    [Header("音量设置")]
    [Tooltip("控制全局音量的滑动条 (0 到 1)")]
    public Slider volumeSlider;
    [Tooltip("显示音量百分比的文本")]
    public TextMeshProUGUI volumeText;

    [Header("画质设置")]
    [Tooltip("控制全局画质的下拉菜单")]
    public TMP_Dropdown qualityDropdown;

    [Header("交互按钮")]
    [Tooltip("返回游戏/关闭设置按钮")]
    public Button closeButton;
    [Tooltip("返回主菜单按钮 (可选)")]
    public Button mainMenuButton;

    private void Start()
    {
        // 1. 动态获取 Unity 项目中配置的所有画质等级名称 (如 Low, Medium, High, Ultra)
        if (qualityDropdown != null)
        {
            qualityDropdown.ClearOptions();
            List<string> options = new List<string>(QualitySettings.names);
            qualityDropdown.AddOptions(options);
        }

        // 2. 读取本地保存的玩家设置
        LoadSettings();

        // 3. 绑定 UI 事件
        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }

        if (qualityDropdown != null)
        {
            qualityDropdown.onValueChanged.AddListener(SetQuality);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(OnCloseButtonClicked);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.AddListener(OnMainMenuButtonClicked);
        }
    }

    // ==========================================
    // 核心功能：修改与保存
    // ==========================================

    public void SetVolume(float volume)
    {
        // AudioListener.volume 会直接控制整个游戏所有 AudioSource 的最终输出音量
        AudioListener.volume = volume;

        if (volumeText != null)
        {
            volumeText.text = Mathf.RoundToInt(volume * 100) + "%";
        }

        // 存入本地存档
        PlayerPrefs.SetFloat("MasterVolume", volume);
        PlayerPrefs.Save();
    }

    public void SetQuality(int qualityIndex)
    {
        QualitySettings.SetQualityLevel(qualityIndex);

        // 存入本地存档
        PlayerPrefs.SetInt("QualityLevel", qualityIndex);
        PlayerPrefs.Save();
    }

    private void LoadSettings()
    {
        // 加载音量
        if (PlayerPrefs.HasKey("MasterVolume"))
        {
            float savedVolume = PlayerPrefs.GetFloat("MasterVolume");
            if (volumeSlider != null) volumeSlider.value = savedVolume;
            SetVolume(savedVolume);
        }
        else
        {
            // 如果是第一次玩，默认音量设为 100%
            if (volumeSlider != null) volumeSlider.value = 1f;
        }

        // 加载画质
        if (PlayerPrefs.HasKey("QualityLevel"))
        {
            int savedQuality = PlayerPrefs.GetInt("QualityLevel");
            if (qualityDropdown != null)
            {
                qualityDropdown.value = savedQuality;
                qualityDropdown.RefreshShownValue();
            }
            SetQuality(savedQuality);
        }
        else
        {
            // 如果是第一次玩，读取当前系统默认画质
            if (qualityDropdown != null)
            {
                qualityDropdown.value = QualitySettings.GetQualityLevel();
            }
        }
    }

    // ==========================================
    // 按钮交互逻辑 (联动 GameManager)
    // ==========================================

    private void OnCloseButtonClicked()
    {
        // 播放弹跳动画
        RectTransform btnRect = closeButton.GetComponent<RectTransform>();
        if (GameManager.Instance != null)
        {
            GameManager.Instance.PlayGenericButtonBounce(btnRect);
            // 延迟一丁点时间关闭面板，让玩家看清弹跳动画
            Invoke(nameof(ExecuteClose), 0.2f);

            // 播放设置UI专属点击音效
            if (GameManager.Instance.settingsUIAudioSource != null && GameManager.Instance.countdownSteps.Count > 0 && GameManager.Instance.countdownSteps[0].sfx != null)
            {
                // 如果你配置了 UI 音效，这里可以调 PlayOneShot
            }
        }
        else
        {
            ExecuteClose();
        }
    }

    private void ExecuteClose()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.CloseSettings();
        }
        else
        {
            gameObject.SetActive(false); // 降级保护
        }
    }

    private void OnMainMenuButtonClicked()
    {
        RectTransform btnRect = mainMenuButton.GetComponent<RectTransform>();
        if (GameManager.Instance != null)
        {
            GameManager.Instance.PlayGenericButtonBounce(btnRect);
            // 直接调用 GameManager 的重开/返回主菜单逻辑
            Invoke(nameof(ExecuteRestart), 0.2f);
        }
    }

    private void ExecuteRestart()
    {
        if (GameManager.Instance != null) GameManager.Instance.RestartGame();
    }
}