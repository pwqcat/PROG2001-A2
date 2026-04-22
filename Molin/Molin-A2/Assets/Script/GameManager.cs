using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using TMPro;

[System.Serializable]
public class AliveMilestone
{
    public int survivorCount;
    public Color textColor = Color.white;
    public AudioClip soundEffect;
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { StartMenu, MenuTransition, Playing, GameOver }
    [Header("当前状态")]
    public GameState currentState = GameState.StartMenu;

    [Header("🎬 狂野飙车级闪切运镜 (Flash-Cut Camera)")]
    public GameObject playCamera;
    public GameObject menuCamera;

    public float timePerShot = 2.8f;
    public float driftSpeed = 0.4f;
    public float carScreenOffsetRight = 1.5f;
    public float minCameraHeight = 0.5f;

    [Header("📷 镜头防穿模设置 (Layer识别)")]
    [Tooltip("会阻挡摄像机视线的 Layer。必须勾选 Default 层，以及你的赛车所在的层。")]
    public LayerMask cameraCollisionLayers;
    [Tooltip("摄像机的物理碰撞体积大小，防止紧贴表面导致看穿模型")]
    public float cameraCollisionRadius = 0.3f;

    private float shotTimer = 0f;
    private int currentShotIndex = -1;
    private Vector3 currentDriftDirection;
    private Vector3 currentDriftOffset;

    private readonly Vector3[] shotOffsets = new Vector3[]
    {
        new Vector3(3.0f, 0.8f, 4.0f),
        new Vector3(-2.5f, 1.8f, -4.5f),
        new Vector3(4.0f, 1.0f, 0.0f),
        new Vector3(-2.0f, 0.6f, 3.5f)
    };

    [Header("🎦 电影级 UI 动画与遮幅")]
    public GameObject startMenuPanel;
    public RectTransform startMenuContent;
    public RawImage topCinematicBar;
    public RawImage bottomCinematicBar;
    public float animationDuration = 0.8f;

    [Header("UI 面板统筹")]
    public GameObject hudPanel;
    public GameObject gameOverPanel;

    [Header("大逃杀核心规则")]
    public float deathYThreshold = -5f;
    public GameObject eliminationVFX;

    [Header("HUD 数据与动态反馈 (Juice)")]
    public TextMeshProUGUI aliveCountText;
    public TextMeshProUGUI killFeedText;
    public int maxFeedLines = 4;
    public float feedStayTime = 3.5f;
    public AudioSource uiAudioSource;
    public float pulseScaleMultiplier = 1.5f;
    public float pulseDuration = 0.3f;
    public List<AliveMilestone> milestones = new List<AliveMilestone>();

    [Header("全局大字 (结算)")]
    public TextMeshProUGUI centerMessageText;

    private GameObject player;
    private List<GameObject> enemies = new List<GameObject>();
    private List<string> activeFeeds = new List<string>();
    private int lastTotalAlive = -1;
    private Coroutine pulseCoroutine;
    private Vector3 originalAliveTextScale;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        enemies.AddRange(GameObject.FindGameObjectsWithTag("Enemy"));

        if (killFeedText != null) killFeedText.text = "";
        if (aliveCountText != null) originalAliveTextScale = aliveCountText.transform.localScale;

        GenerateCinematicGradients();
        InitializeStartMenu();
    }

    void Update()
    {
        switch (currentState)
        {
            case GameState.StartMenu:
                UpdateFlashCutCamera();

                if (Input.GetKeyDown(KeyCode.Space))
                {
                    currentState = GameState.MenuTransition;
                    StartCoroutine(PlayMenuExitAnimation());
                }
                break;

            case GameState.MenuTransition:
                UpdateFlashCutCamera();
                break;

            case GameState.Playing:
                CheckEliminations();
                break;
        }
    }

    // ==========================================
    // 🎬 核心更新：基于 Layer 的球形射线防穿模
    // ==========================================
    private void UpdateFlashCutCamera()
    {
        if (menuCamera == null || player == null) return;

        shotTimer -= Time.deltaTime;
        if (shotTimer <= 0f) CutToNextShot();

        // 1. 动态计算基础机位（跟随车体可能存在的微小晃动）
        Vector3 localOffset = shotOffsets[currentShotIndex];
        Vector3 basePos = player.transform.position
                         + player.transform.right * localOffset.x
                         + player.transform.up * localOffset.y
                         + player.transform.forward * localOffset.z;

        // 2. 累加缓慢漂移
        currentDriftOffset += currentDriftDirection * driftSpeed * Time.deltaTime;
        Vector3 idealPos = basePos + currentDriftOffset;

        // 3. 海拔底线锁
        if (idealPos.y < minCameraHeight) idealPos.y = minCameraHeight;

        // 4. 计算视线基准点（看向车体中心偏上）
        Vector3 baseLookTarget = player.transform.position + Vector3.up * 0.6f;
        Vector3 dirToCam = idealPos - baseLookTarget;
        float distToCam = dirToCam.magnitude;

        Vector3 finalCamPos = idealPos;

        // 5. 【防穿模核心】从车体中心向理想相机位置发射一个带有宽度的“球形射线”
        // 如果这根射线撞到了 cameraCollisionLayers 中指定的 Layer...
        if (Physics.SphereCast(baseLookTarget, cameraCollisionRadius, dirToCam.normalized, out RaycastHit hit, distToCam, cameraCollisionLayers))
        {
            // 强行把摄像机拉到障碍物的前方，防止进入模型内部！
            finalCamPos = hit.point + hit.normal * 0.1f;
        }

        menuCamera.transform.position = finalCamPos;

        // 6. 保持黄金比例构图
        Vector3 finalLookTarget = player.transform.position - menuCamera.transform.right * carScreenOffsetRight + Vector3.up * 0.6f;
        menuCamera.transform.LookAt(finalLookTarget);
    }

    private void CutToNextShot()
    {
        shotTimer = timePerShot;
        currentShotIndex = (currentShotIndex + 1) % shotOffsets.Length;
        currentDriftOffset = Vector3.zero; // 每次切镜清空漂移累加器

        currentDriftDirection = new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(0f, 0.2f),
            Random.Range(-1f, 1f)
        ).normalized;
    }

    // ==========================================
    // UI 生成与大逃杀逻辑 (保持不变)
    // ==========================================
    private void GenerateCinematicGradients()
    {
        int resolution = 256;
        float solidBlackRatio = 0.35f;

        Texture2D gradientTex = new Texture2D(1, resolution, TextureFormat.ARGB32, false);
        gradientTex.wrapMode = TextureWrapMode.Clamp;
        gradientTex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < resolution; y++)
        {
            float t = y / (float)(resolution - 1);
            float alpha = Mathf.InverseLerp(0f, 1f - solidBlackRatio, t);
            alpha = Mathf.SmoothStep(0f, 1f, alpha);
            float noise = UnityEngine.Random.Range(-0.015f, 0.015f);
            alpha = Mathf.Clamp01(alpha + noise);
            gradientTex.SetPixel(0, y, new Color(0, 0, 0, alpha));
        }
        gradientTex.Apply();

        if (topCinematicBar != null)
        {
            topCinematicBar.texture = gradientTex;
            topCinematicBar.uvRect = new Rect(0, 0, 1, 1);
        }

        if (bottomCinematicBar != null)
        {
            bottomCinematicBar.texture = gradientTex;
            bottomCinematicBar.uvRect = new Rect(0, 1, 1, -1);
        }
    }

    private void InitializeStartMenu()
    {
        currentState = GameState.StartMenu;
        SetAllVehiclesActive(false);

        if (playCamera != null) playCamera.SetActive(false);
        if (menuCamera != null) menuCamera.SetActive(true);

        if (startMenuPanel != null) startMenuPanel.SetActive(true);
        if (hudPanel != null) hudPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (centerMessageText != null) centerMessageText.gameObject.SetActive(false);

        shotTimer = 0f;

        StartCoroutine(PlayMenuEnterAnimation());
    }

    private IEnumerator PlayMenuEnterAnimation()
    {
        float elapsed = 0f;
        Vector2 topStart = new Vector2(0, 300f);
        Vector2 botStart = new Vector2(0, -300f);
        Vector2 contentStart = new Vector2(-1200f, 0f);

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / animationDuration);

            if (topCinematicBar != null) topCinematicBar.rectTransform.anchoredPosition = Vector2.Lerp(topStart, Vector2.zero, t);
            if (bottomCinematicBar != null) bottomCinematicBar.rectTransform.anchoredPosition = Vector2.Lerp(botStart, Vector2.zero, t);
            if (startMenuContent != null) startMenuContent.anchoredPosition = Vector2.Lerp(contentStart, Vector2.zero, t);

            yield return null;
        }
    }

    private IEnumerator PlayMenuExitAnimation()
    {
        if (uiAudioSource != null) uiAudioSource.Play();

        float elapsed = 0f;
        Vector2 topEnd = new Vector2(0, 300f);
        Vector2 botEnd = new Vector2(0, -300f);
        Vector2 contentEnd = new Vector2(-1200f, 0f);

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / animationDuration);

            if (topCinematicBar != null) topCinematicBar.rectTransform.anchoredPosition = Vector2.Lerp(Vector2.zero, topEnd, t);
            if (bottomCinematicBar != null) bottomCinematicBar.rectTransform.anchoredPosition = Vector2.Lerp(Vector2.zero, botEnd, t);
            if (startMenuContent != null) startMenuContent.anchoredPosition = Vector2.Lerp(Vector2.zero, contentEnd, t);

            yield return null;
        }

        StartGame();
    }

    private void StartGame()
    {
        currentState = GameState.Playing;
        SetAllVehiclesActive(true);

        if (menuCamera != null) menuCamera.SetActive(false);
        if (playCamera != null) playCamera.SetActive(true);

        if (startMenuPanel != null) startMenuPanel.SetActive(false);
        if (hudPanel != null) hudPanel.SetActive(true);

        UpdateUI(true);
    }

    private void SetAllVehiclesActive(bool isActive)
    {
        if (player != null)
        {
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc != null) pc.enabled = isActive;
        }
        foreach (var enemy in enemies)
        {
            if (enemy != null)
            {
                EnemyAIController ai = enemy.GetComponent<EnemyAIController>();
                if (ai != null) ai.enabled = isActive;
            }
        }
    }

    private void CheckEliminations()
    {
        if (player != null && player.transform.position.y < deathYThreshold)
        {
            EliminateCar(player, "Player");
            TriggerGameOver(false);
        }
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            GameObject enemy = enemies[i];
            if (enemy != null && enemy.transform.position.y < deathYThreshold)
            {
                EliminateCar(enemy, enemy.name);
                enemies.RemoveAt(i);
                UpdateUI(false);
                if (enemies.Count == 0 && player != null) TriggerGameOver(true);
            }
        }
    }

    private void EliminateCar(GameObject car, string carName)
    {
        if (eliminationVFX != null) Instantiate(eliminationVFX, car.transform.position + Vector3.up * 2f, Quaternion.identity);
        ShowKillFeed($"{carName} fell out");
        Destroy(car);
    }

    private void ShowKillFeed(string message)
    {
        if (killFeedText == null) return;
        StartCoroutine(FeedRoutine(message));
    }

    private IEnumerator FeedRoutine(string message)
    {
        activeFeeds.Add(message);
        if (activeFeeds.Count > maxFeedLines) activeFeeds.RemoveAt(0);
        UpdateFeedText();
        yield return new WaitForSeconds(feedStayTime);
        if (activeFeeds.Contains(message))
        {
            activeFeeds.Remove(message);
            UpdateFeedText();
        }
    }

    private void UpdateFeedText() => killFeedText.text = string.Join("\n", activeFeeds);

    private void UpdateUI(bool isInitialization)
    {
        if (aliveCountText != null)
        {
            int totalAlive = enemies.Count + (player != null ? 1 : 0);
            if (totalAlive != lastTotalAlive)
            {
                aliveCountText.text = "ALIVE: " + totalAlive;
                if (!isInitialization)
                {
                    if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
                    pulseCoroutine = StartCoroutine(PulseTextRoutine());
                    foreach (var m in milestones)
                    {
                        if (totalAlive == m.survivorCount)
                        {
                            aliveCountText.color = m.textColor;
                            if (m.soundEffect != null && uiAudioSource != null) uiAudioSource.PlayOneShot(m.soundEffect);
                            break;
                        }
                    }
                }
                lastTotalAlive = totalAlive;
            }
        }
    }

    private IEnumerator PulseTextRoutine()
    {
        float elapsed = 0f;
        Vector3 targetScale = originalAliveTextScale * pulseScaleMultiplier;
        while (elapsed < pulseDuration)
        {
            elapsed += Time.deltaTime;
            float curve = Mathf.Sin((elapsed / pulseDuration) * Mathf.PI);
            aliveCountText.transform.localScale = Vector3.Lerp(originalAliveTextScale, targetScale, curve);
            yield return null;
        }
        aliveCountText.transform.localScale = originalAliveTextScale;
    }

    private void TriggerGameOver(bool isWin)
    {
        currentState = GameState.GameOver;
        if (hudPanel != null) hudPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        if (centerMessageText != null)
        {
            centerMessageText.gameObject.SetActive(true);
            centerMessageText.text = isWin ? "VICTORY" : "ELIMINATED";
            centerMessageText.color = isWin ? new Color(1f, 0.8f, 0f) : new Color(1f, 0.2f, 0.2f);
        }
    }

    public void RestartGame() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
}