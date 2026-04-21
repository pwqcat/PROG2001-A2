using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIGameButtons : MonoBehaviour
{
    [Header("按钮")]
    public Button btnRestart;
    public Button btnSelectLevel;
    public Button btnBackToMenu;
    [Header("场景名称")]
    public string mainMenuScene = "MainMenu";
    public string levelSelectScene = "LevelSelect";

    void Awake()
    {
    }

    void Start()
    {
        btnRestart.onClick.AddListener(OnRestartClick);
        btnSelectLevel.onClick.AddListener(OnSelectLevelClick);
        btnBackToMenu.onClick.AddListener(OnBackToMenuClick);
    }

    // 重新开始当前关卡
    void OnRestartClick()
    {
        Time.timeScale = 1;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // 打开选关场景
    void OnSelectLevelClick()
    {
        Time.timeScale = 1;
        SceneManager.LoadScene(levelSelectScene);
    }

    // 返回主菜单
    void OnBackToMenuClick()
    {
        Time.timeScale = 1;
        SceneManager.LoadScene(mainMenuScene);
    }
}