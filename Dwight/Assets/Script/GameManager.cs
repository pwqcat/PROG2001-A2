using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("金币设置")]
    public int totalCoins = 0;
    public int needCoins = 5;

    [Header("倒计时设置")]
    public float gameTime = 30f;
    private float currentTime;

    [Header("UI引用")]
    public TextMeshProUGUI coinText;
    public TextMeshProUGUI timerText;
    public GameObject winPanel;
    public GameObject losePanel;

    [Header("胜利面板UI")]
    public TextMeshProUGUI WinTimeText; // 拖入胜利面板的剩余时间文本

    [Header("音频设置")]
    public AudioSource bgmAudio;
    public AudioClip winSound;
    public AudioClip loseSound;

    [Header("玩家")]
    public GameObject player;

    private AudioSource _audioSource;
    private bool _isGameOver = false;

    void Awake()
    {
        if (instance == null)
            instance = this;

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
    }

    void Start()
    {
        currentTime = gameTime;
        UpdateUI();

        winPanel?.SetActive(false);
        losePanel?.SetActive(false);

        if (bgmAudio != null)
        {
            bgmAudio.loop = true;
            bgmAudio.Play();
        }
    }

    void Update()
    {
        if (_isGameOver) return;

        currentTime -= Time.deltaTime;
        currentTime = Mathf.Max(0, currentTime);
        UpdateUI();

        if (currentTime <= 0)
        {
            GameEnd(false);
        }
    }

    void UpdateUI()
    {
        if (coinText != null)
            coinText.text = "Coin:" + totalCoins;

        if (timerText != null)
            timerText.text = "Time:" + Mathf.Round(currentTime).ToString();
    }

    public void AddCoin()
    {
        if (_isGameOver) return;

        totalCoins++;
        UpdateUI();

        if (totalCoins >= needCoins)
        {
            GameEnd(true);
        }
    }

    void GameEnd(bool isWin)
    {
        _isGameOver = true;

        // 停止背景音乐
        if (bgmAudio != null)
            bgmAudio.Stop();

        // 禁用玩家
        if (player != null)
            player.SetActive(false);

        if (isWin)
        {
            _audioSource.PlayOneShot(winSound);
            winPanel?.SetActive(true);

            // 显示剩余时间
            if (WinTimeText != null)
                WinTimeText.text = "Time:" + Mathf.Round(currentTime) + " 秒";
        }
        else
        {
            _audioSource.PlayOneShot(loseSound);
            losePanel?.SetActive(true);
        }
    }

    // 重新开始游戏
    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}