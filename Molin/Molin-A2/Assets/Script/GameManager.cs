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

    [Header("⚙️ 全局设置与弹窗")]
    [Tooltip("全局设置面板 (Settings Panel)")]
    public GameObject settingsPanel;
    [Tooltip("帮助弹窗面板 (Help Panel)")]
    public GameObject helpPanel;

    // 动态判断当前是否有任何弹窗遮挡
    public bool IsAnyPopupOpen =>
        (settingsPanel != null && settingsPanel.activeSelf) ||
        (helpPanel != null && helpPanel.activeSelf);

    [Header("🎬 狂野飙车级闪切运镜")]
    public GameObject playCamera;
    public GameObject menuCamera;
    public float timePerShot = 2.8f;
    public float driftSpeed = 0.4f;
    public float carScreenOffsetRight = 1.5f;
    public float minCameraHeight = 0.5f;

    [Header("📷 镜头防穿模设置")]
    public LayerMask cameraCollisionLayers;
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

        // 确保游戏开始时时间是流动的
        Time.timeScale = 1f;
    }

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        enemies.AddRange(GameObject.FindGameObjectsWithTag("Enemy"));

        if (killFeedText != null) killFeedText.text = "";
        if (aliveCountText != null) originalAliveTextScale = aliveCountText.transform.localScale;

        // 确保一开始弹窗是关闭的
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (helpPanel != null) helpPanel.SetActive(false);

        GenerateCinematicGradients();
        InitializeStartMenu();
    }

    void Update()
    {
        // ==========================================
        // 🖱️ 全局 ESC 键监听 (类似浏览器的后退逻辑)
        // ==========================================
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (helpPanel != null && helpPanel.activeSelf)
            {
                CloseHelp(); // 如果开着 Help，按 ESC 先关 Help，退回 Settings
            }
            else if (settingsPanel != null && settingsPanel.activeSelf)
            {
                CloseSettings(); // 如果开着 Settings，关掉它并恢复游戏
            }
            else if (currentState == GameState.Playing || currentState == GameState.StartMenu)
            {
                OpenSettings(); // 如果什么都没开，按 ESC 打开设置面板
            }
        }

        // ==========================================
        // 状态机核心更新
        // ==========================================
        switch (currentState)
        {
            case GameState.StartMenu:
                // 使用 unscaledDeltaTime 确保即使菜单暂停了时间，镜头也能继续运镜展示
                UpdateFlashCutCamera(Time.unscaledDeltaTime);

                // 只有当没有任何弹窗开启时，按空格才能开始游戏！
                if (Input.GetKeyDown(KeyCode.Space) && !IsAnyPopupOpen)
                {
                    currentState = GameState.MenuTransition;
                    StartCoroutine(PlayMenuExitAnimation());
                }
                break;

            case GameState.MenuTransition:
                UpdateFlashCutCamera(Time.unscaledDeltaTime);
                break;

            case GameState.Playing:
                CheckEliminations();
                break;
        }
    }

    // ==========================================
    // ⚙️ 弹窗与时间控制系统
    // ==========================================
    public void OpenSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(true);

        // 无论什么时候打开设置，都需要放出鼠标
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // 如果在游玩中，暂停时间 (物理效果、AI、车子都会被冻结)
        if (currentState == GameState.Playing)
        {
            Time.timeScale = 0f;
        }
    }

    public void CloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);

        // 如果是在游玩中关掉设置，恢复时间和隐藏鼠标
        if (currentState == GameState.Playing)
        {
            Time.timeScale = 1f;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
        else if (currentState == GameState.StartMenu)
        {
            // 准备界面本来就有鼠标，所以只需保证它是显示的
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    public void OpenHelp()
    {
        if (helpPanel != null) helpPanel.SetActive(true);
    }

    public void CloseHelp()
    {
        if (helpPanel != null) helpPanel.SetActive(false);
    }

    // ==========================================
    // 🎬 运镜逻辑 (修改为支持 Unscaled Time)
    // ==========================================
    private void UpdateFlashCutCamera(float deltaTime)
    {
        if (menuCamera == null || player == null) return;

        shotTimer -= deltaTime;
        if (shotTimer <= 0f) CutToNextShot();

        Vector3 localOffset = shotOffsets[currentShotIndex];
        Vector3 basePos = player.transform.position
                         + player.transform.right * localOffset.x
                         + player.transform.up * localOffset.y
                         + player.transform.forward * localOffset.z;

        currentDriftOffset += currentDriftDirection * driftSpeed * deltaTime;
        Vector3 idealPos = basePos + currentDriftOffset;

        if (idealPos.y < minCameraHeight) idealPos.y = minCameraHeight;

        Vector3 baseLookTarget = player.transform.position + Vector3.up * 0.6f;
        Vector3 dirToCam = idealPos - baseLookTarget;
        float distToCam = dirToCam.magnitude;

        Vector3 finalCamPos = idealPos;

        if (Physics.SphereCast(baseLookTarget, cameraCollisionRadius, dirToCam.normalized, out RaycastHit hit, distToCam, cameraCollisionLayers))
        {
            finalCamPos = hit.point + hit.normal * 0.1f;
        }

        menuCamera.transform.position = finalCamPos;
        Vector3 finalLookTarget = player.transform.position - menuCamera.transform.right * carScreenOffsetRight + Vector3.up * 0.6f;
        menuCamera.transform.LookAt(finalLookTarget);
    }

    private void CutToNextShot()
    {
        shotTimer = timePerShot;
        currentShotIndex = (currentShotIndex + 1) % shotOffsets.Length;
        currentDriftOffset = Vector3.zero;

        currentDriftDirection = new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(0f, 0.2f),
            Random.Range(-1f, 1f)
        ).normalized;
    }

    // ==========================================
    // 底层 UI 与流程逻辑 (完全保留)
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

        if (topCinematicBar != null) { topCinematicBar.texture = gradientTex; topCinematicBar.uvRect = new Rect(0, 0, 1, 1); }
        if (bottomCinematicBar != null) { bottomCinematicBar.texture = gradientTex; bottomCinematicBar.uvRect = new Rect(0, 1, 1, -1); }
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

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

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
            elapsed += Time.unscaledDeltaTime; // 即使暂停也能播放动画
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
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        float elapsed = 0f;
        Vector2 topEnd = new Vector2(0, 300f);
        Vector2 botEnd = new Vector2(0, -300f);
        Vector2 contentEnd = new Vector2(-1200f, 0f);

        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
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
            elapsed += Time.unscaledDeltaTime;
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

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (centerMessageText != null)
        {
            centerMessageText.gameObject.SetActive(true);
            centerMessageText.text = isWin ? "VICTORY" : "ELIMINATED";
            centerMessageText.color = isWin ? new Color(1f, 0.8f, 0f) : new Color(1f, 0.2f, 0.2f);
        }
    }

    public void RestartGame()
    {
        Time.timeScale = 1f; // 确保重启时时间恢复流动
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}