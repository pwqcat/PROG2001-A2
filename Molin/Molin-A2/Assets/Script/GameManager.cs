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

    [Header("🎬 3A级展厅运镜 (AAA Showroom Camera)")]
    public GameObject playCamera;
    public GameObject menuCamera;

    [Tooltip("基础环绕速度 (建议调慢，越慢越有质感)")]
    public float baseOrbitSpeed = 4f;
    [Tooltip("基础相机距离")]
    public float baseOrbitDistance = 5.5f;
    [Tooltip("基础相机高度")]
    public float baseOrbitHeight = 1.2f;
    [Tooltip("正数会让战车偏向屏幕右侧，给左侧UI留出空间")]
    public float carScreenOffsetRight = 1.5f;

    [Space(10)]
    [Header("运镜高级质感 (Juice)")]
    [Tooltip("推拉镜头：距离的呼吸浮动幅度")]
    public float distanceDriftAmplitude = 0.8f;
    [Tooltip("推拉镜头：距离浮动的速度")]
    public float distanceDriftSpeed = 0.5f;
    [Tooltip("升降镜头：高度的呼吸浮动幅度")]
    public float heightDriftAmplitude = 0.4f;
    [Tooltip("升降镜头：高度浮动的速度")]
    public float heightDriftSpeed = 0.7f;
    [Tooltip("镜头重量感：数值越小，镜头移动和转动越有惯性和迟滞感")]
    public float cameraDamping = 3f;

    private float currentOrbitAngle = 45f;
    private Vector3 smoothedLookTarget = Vector3.zero; // 用于阻尼平滑看向

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
                UpdateShowroomCamera();
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    currentState = GameState.MenuTransition;
                    StartCoroutine(PlayMenuExitAnimation());
                }
                break;

            case GameState.MenuTransition:
                UpdateShowroomCamera();
                break;

            case GameState.Playing:
                CheckEliminations();
                break;
        }
    }

    // ==========================================
    // 🎬 核心重写：3A 级相机控制算法
    // ==========================================
    private void UpdateShowroomCamera()
    {
        if (menuCamera == null || player == null) return;

        float time = Time.time;
        currentOrbitAngle += baseOrbitSpeed * Time.deltaTime;

        // 1. 生成带有“呼吸感”的动态推拉与升降 (利用正弦波和余弦波)
        // 这样镜头就不会死板地画圆，而是像椭圆一样有节奏地靠近、远离、升高、降低
        float dynamicDistance = baseOrbitDistance + Mathf.Cos(time * distanceDriftSpeed) * distanceDriftAmplitude;
        float dynamicHeight = baseOrbitHeight + Mathf.Sin(time * heightDriftSpeed) * heightDriftAmplitude;

        // 2. 计算相机的“绝对理想位置”
        Quaternion rotation = Quaternion.Euler(0, currentOrbitAngle, 0);
        Vector3 targetCamPos = player.transform.position + rotation * new Vector3(0, dynamicHeight, -dynamicDistance);

        // 如果是第一帧，强制对齐防止镜头瞬间瞬移
        if (smoothedLookTarget == Vector3.zero)
        {
            menuCamera.transform.position = targetCamPos;
            smoothedLookTarget = player.transform.position;
        }

        // 3. 赋予“镜头物理重量感” (Smooth Lerp 位置)
        // 镜头不会立刻到达目标点，而是被拖拽着过去，产生极佳的高级感
        menuCamera.transform.position = Vector3.Lerp(menuCamera.transform.position, targetCamPos, Time.deltaTime * cameraDamping);

        // 4. 计算黄金分割偏置看向点
        // 利用相机的右方向向量，把视觉中心往车身左侧推，从而让车在屏幕上偏右
        Vector3 idealLookTarget = player.transform.position - menuCamera.transform.right * carScreenOffsetRight;
        // 微调看向点的高度，让它跟随动态高度变化，防止盯住轮胎死看
        idealLookTarget.y += dynamicHeight * 0.4f;

        // 5. 赋予“云台阻尼感” (Smooth Lerp 看向点)
        smoothedLookTarget = Vector3.Lerp(smoothedLookTarget, idealLookTarget, Time.deltaTime * (cameraDamping * 1.5f));

        menuCamera.transform.LookAt(smoothedLookTarget);
    }

    // ==========================================
    // 电影黑边与动画生成 (保留你优化的版本)
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

    // ==========================================
    // 底层控制与大逃杀逻辑 (保持不变)
    // ==========================================
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