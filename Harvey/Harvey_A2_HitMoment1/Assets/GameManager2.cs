using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class GameManager2 : MonoBehaviour
{
    public static GameManager2 instance;

    [Header("��Ϸ״̬")]
    public bool hasGameStarted = false;
    public float timeRemaining = 30f;
    public int score = 0;
    public bool isGameOver = false;

    [Header("UI ����")]
    public Text scoreText;
    public Text timeText;
    public GameObject gameOverPanel;
    public Text gameOverText;

    // ������������������Ǹ���Shoot to Start�����ذ���
    [Header("��������")]
    public GameObject startGameTarget;

    void Awake()
    {
        instance = this;
    }

    void Update()
    {
        // ===================================
        // �� ESC ���л����ò˵��Ŀ���
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (settingsPanel != null)
            {
                if (!settingsPanel.activeSelf) // ���û��
                {
                    OpenSettings(); // ����
                }
                else // ����Ѿ�����
                {
                    ResumeGame(); // �ص�����������Ϸ
                }
            }
        }
        // ===================================

        if (!hasGameStarted || isGameOver) return;

        if (timeRemaining > 0)
        {
            timeRemaining -= Time.deltaTime;
            timeText.text = "Time: " + Mathf.CeilToInt(timeRemaining).ToString() + "s";
        }
        else
        {
            timeRemaining = 0;
            isGameOver = true;
            timeText.text = "Time: 0s";
            GameOver();
        }
    }

    public void StartGame()
    {
        hasGameStarted = true;
    }

    public void AddScore(int points)
    {
        if (!hasGameStarted || isGameOver) return;
        score += points;
        scoreText.text = "Score: " + score;
    }

    void GameOver()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            gameOverText.text = "Time's up!\nScore: " + score;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // ==========================================
    public void RestartGame()
    {
        // 1. ���ؽ������
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        // 2. �������㲢˲����뿪ʼ״̬
        isGameOver = false;
        hasGameStarted = true; // ֱ�ӱ��Ϊ�ѿ�ʼ
        timeRemaining = 30f;
        score = 0;

        // 3. �����Ǹ���Shoot to Start���ĺ�ɫ���ӣ���Ϊֱ�ӿ�ʼ�ˣ�
        if (startGameTarget != null) startGameTarget.SetActive(false);

        // 4. �����������
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ��ȫ���߼���QUIT: �ر���壬�޷��˻ش��������ƶ�״̬
    public void ReturnToLobby()
    {
        // 1. ���ؽ������
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        // 2. �������㣬�ص�δ��ʼ״̬
        hasGameStarted = false;
        isGameOver = false;
        timeRemaining = 30f;
        score = 0;

        // 3. ˢ����Ļ�ϵ� UI ����
        timeText.text = "Time: 30s";
        scoreText.text = "Score: 0";

        // 4. �����½��Ǹ���Shoot to Start���İ������³��֣�
        if (startGameTarget != null)
        {
            startGameTarget.SetActive(true);
        }

        // 5. ����������꣬��������޷������׼�ǿ�����
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    // ==========================================

    public void ReviveTarget(GameObject target, float delayTime)
    {
        StartCoroutine(ReviveCoroutine(target, delayTime));
    }

    IEnumerator ReviveCoroutine(GameObject target, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (target != null && !isGameOver)
        {
            target.SetActive(true);
            TargetObject targetScript = target.GetComponent<TargetObject>();
            if (targetScript != null)
            {
                targetScript.health = targetScript.maxHealth;
            }
        }
    }
    [Header("�����������")]
    public GameObject settingsPanel;
    public FPSController fpsController; // ������ȡ��ҵĽű����޸�������

    // =====================================
    // ���ò˵�ר������
    // =====================================

    // �����ò˵�
    public void OpenSettings()
    {
        settingsPanel.SetActive(true);
        Time.timeScale = 0f; // ʱ��ֹͣ����Ϸ��ͣ��

        // �������
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // �ر����ò˵���������Ϸ
    public void ResumeGame()
    {
        settingsPanel.SetActive(false);
        Time.timeScale = 1f;

        // ǿ��ȡ��ѡ���κ� UI���ѿ���Ȩ������Ϸ��
        EventSystem.current.SetSelectedGameObject(null);

        if (!isGameOver)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // �������� (Slider��̬����)
    public void SetVolume(float value)
    {
        AudioListener.volume = value; // ֱ�ӿ���ȫ�������� (0 �� 1 ֮��)
    }

    // ���������� (Slider��̬����)
    public void SetSensitivity(float value)
    {
        if (fpsController != null)
        {
            fpsController.mouseSensitivity = value;
        }
    }

    // ��ת���˵�
    public void GoToMainMenu()
    {
        Time.timeScale = 1f; // �뿪ǰһ��Ҫ�ָ�ʱ�䣬�����¸�����Ҳ����ͣ�ģ�
        SceneManager.LoadScene("MainMenu");
    }

    // ��תѡ�ؽ���
    public void GoToLevelSelect()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("LevelSelect"); // �������ѡ�س������������
    }
}
