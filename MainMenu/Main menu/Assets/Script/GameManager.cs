using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("��Ϸ״̬")]
    public bool isPaused = false;
    public bool isGameOver = false;
    public bool isWin = false;

    [Header("����ֵ")]
    public int maxHp = 5;
    public int currentHp;
    public Text hpText;

    [Header("�ռ�Ʒ")]
    public int totalCollectibles;
    public int currentCollectibles = 0;
    public Text collectText;

    [Header("��ʱ")]
    public float gameTime = 0;
    public Text timeText;
    public Text winTimeText;
    public Text winCollectText;

    [Header("UI���")]
    public GameObject startTipPanel;
    public GameObject settingPanel;
    public GameObject gameOverPanel;
    public GameObject winPanel;

    [Header("������")]
    public Transform currentRespawnPoint;
    public Transform defaultRespawnPoint;

    [Header("�Զ���������")]
    public float waitTime = 3f; // �ȴ����뷵����һ������

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        currentHp = maxHp;
        currentRespawnPoint = defaultRespawnPoint;
        UpdateUI();
    }
    // �л��������״̬
    public void ToggleMouseLock()
    {
        // ��Ϸ��ͣ�����ʱ����Ч
        if (isPaused || isGameOver || isWin)
            return;

        bool isLocked = Cursor.lockState == CursorLockMode.Locked;

        if (isLocked)
        {
            // �������
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // ������꣨��Ϸ�ӽ�ģʽ��
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
    void Update()
    {

        // ���� Tab �л��������
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleMouseLock();
        }



        if (Input.GetKeyDown(KeyCode.Escape) && !isGameOver && !isWin)
        {
            ToggleSetting();
        }

        if (!isPaused && !isGameOver && !isWin)
        {
            gameTime += Time.deltaTime;
            UpdateTimer();
        }
    }

    IEnumerator ShowStartTip()
    {
        startTipPanel.SetActive(true);
        yield return new WaitForSeconds(5f);
        startTipPanel.SetActive(false);
    }

    public void ToggleSetting()
    {
        isPaused = !isPaused;
        settingPanel.SetActive(isPaused);
        Cursor.lockState = isPaused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isPaused;
        Time.timeScale = isPaused ? 0 : 1;
    }

    public void LoseHp()
    {
        currentHp--;
        UpdateUI();

        if (currentHp <= 0)
        {
            GameOver();
        }
        else
        {
            PlayerController player = FindObjectOfType<PlayerController>();
            player.transform.position = currentRespawnPoint.position;
            player.transform.rotation = currentRespawnPoint.rotation;
            player.GetComponent<Rigidbody>().velocity = Vector3.zero;
        }
    }

    public void AddCollectible()
    {
        currentCollectibles++;
        UpdateUI();
    }

    void UpdateUI()
    {
        hpText.text = "Hp: " + currentHp;
        collectText.text = "Coins: " + currentCollectibles;
    }

    void UpdateTimer()
    {
        int minutes = Mathf.FloorToInt(gameTime / 60);
        int seconds = Mathf.FloorToInt(gameTime % 60);
        timeText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    void GameOver()
    {
        isGameOver = true;
        gameOverPanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // ʧ�ܺ�ȴ����뷵����һ������
        StartCoroutine(LoadLastSceneAfterWait());
    }

    public void WinGame()
    {
        isWin = true;
        winPanel.SetActive(true);
        winTimeText.text = "Completion time: " + timeText.text;
        winCollectText.text = "Coins: " + currentCollectibles;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        
    }

    // �ȴ����� �� �Զ�������һ������
    IEnumerator LoadLastSceneAfterWait()
    {
        yield return new WaitForSecondsRealtime(waitTime);
        int lastSceneIndex = 0;
        winPanel.SetActive(false);
        //SceneManager.LoadScene(lastSceneIndex);
        Time.timeScale = 1; // �ָ���Ϸ�ٶ�
    }

    public void UpdateRespawnPoint(Transform newPoint)
    {
        currentHp ++;
        hpText.text = "Hp: " + currentHp;
        currentRespawnPoint = newPoint;
    }
}