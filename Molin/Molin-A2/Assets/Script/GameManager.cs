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

    // 【新增】：Spawning 状态，用于管理生成期间的过渡
    public enum GameState { StartMenu, MenuTransition, Spawning, Playing, GameOver }
    [Header("当前状态")]
    public GameState currentState = GameState.StartMenu;

    [Header("⚙️ 全局设置与弹窗")]
    public GameObject settingsPanel;
    public GameObject helpPanel;
    public bool IsAnyPopupOpen => (settingsPanel != null && settingsPanel.activeSelf) || (helpPanel != null && helpPanel.activeSelf);

    [Header("✨ 动态生成 (传送登场) 系统")]
    [Tooltip("敌人的传送特效")]
    public GameObject spawnVFX;
    [Tooltip("敌人传送时的音效")]
    public AudioClip spawnSFX;
    [Tooltip("每个敌人生成的间隔时间 (秒)")]
    public float spawnInterval = 0.4f;
    [Tooltip("生成区域的中心点 (不填则默认为世界原点 0,0,0)")]
    public Transform spawnAreaCenter;
    [Tooltip("生成区域的长宽范围")]
    public Vector2 spawnAreaSize = new Vector2(60f, 60f);
    [Tooltip("距离玩家多近以内绝对不生成敌人")]
    public float minDistanceFromPlayer = 15f;
    [Tooltip("会被视为障碍物、不能生成在里面的 Layer")]
    public LayerMask obstacleLayer;
    [Tooltip("车辆占据的碰撞半径，越大越不容易卡墙")]
    public float obstacleCheckRadius = 3f;

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

        Time.timeScale = 1f;
    }

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        enemies.AddRange(GameObject.FindGameObjectsWithTag("Enemy"));

        if (killFeedText != null) killFeedText.text = "";
        if (aliveCountText != null) originalAliveTextScale = aliveCountText.transform.localScale;

        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (helpPanel != null) helpPanel.SetActive(false);

        GenerateCinematicGradients();
        InitializeStartMenu();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (helpPanel != null && helpPanel.activeSelf) CloseHelp();
            else if (settingsPanel != null && settingsPanel.activeSelf) CloseSettings();
            else if (currentState == GameState.Playing || currentState == GameState.StartMenu) OpenSettings();
        }

        switch (currentState)
        {
            case GameState.StartMenu:
                UpdateFlashCutCamera(Time.unscaledDeltaTime);
                if (Input.GetKeyDown(KeyCode.Space) && !IsAnyPopupOpen)
                {
                    currentState = GameState.MenuTransition;
                    StartCoroutine(PlayMenuExitAnimation());
                }
                break;

            case GameState.MenuTransition:
            case GameState.Spawning: // 【新增】：在生成敌人期间，依然保持闪切运镜！
                UpdateFlashCutCamera(Time.unscaledDeltaTime);
                break;

            case GameState.Playing:
                CheckEliminations();
                break;
        }
    }

    // ==========================================
    // ✨ 核心新增：传送生成系统
    // ==========================================
    private IEnumerator SpawnEnemiesSequence()
    {
        currentState = GameState.Spawning;

        // 打乱敌人列表，让每次生成的顺序都不一样
        ShuffleList(enemies);

        foreach (var enemy in enemies)
        {
            if (enemy == null) continue;

            // 寻找合法生成点
            Vector3 spawnPos = GetValidSpawnPosition();

            // 传送敌人
            enemy.transform.position = spawnPos;

            // 播放特效和音效
            if (spawnVFX != null) Instantiate(spawnVFX, spawnPos, Quaternion.identity);
            if (spawnSFX != null && uiAudioSource != null) uiAudioSource.PlayOneShot(spawnSFX);

            // 等待下一个
            yield return new WaitForSeconds(spawnInterval);
        }

        // 所有敌人集结完毕，正式开战！
        StartGame();
    }

    private Vector3 GetValidSpawnPosition()
    {
        Vector3 center = spawnAreaCenter != null ? spawnAreaCenter.position : Vector3.zero;

        // 尝试寻找合法点，最多尝试 30 次防止死循环
        for (int i = 0; i < 30; i++)
        {
            float randomX = Random.Range(-spawnAreaSize.x / 2f, spawnAreaSize.x / 2f);
            float randomZ = Random.Range(-spawnAreaSize.y / 2f, spawnAreaSize.y / 2f);
            Vector3 testPos = center + new Vector3(randomX, 50f, randomZ); // 从天空往下打射线

            // 射线找地面
            if (Physics.Raycast(testPos, Vector3.down, out RaycastHit hit, 100f))
            {
                Vector3 potentialPos = hit.point + Vector3.up * 1f; // 抬高一点防卡地

                // 规则 1：避开玩家
                if (player != null && Vector3.Distance(potentialPos, player.transform.position) < minDistanceFromPlayer)
                    continue;

                // 规则 2：避开指定障碍物
                if (Physics.CheckSphere(potentialPos, obstacleCheckRadius, obstacleLayer))
                    continue;

                return potentialPos;
            }
        }

        // 如果 30 次都没找到（地图太小或障碍太多），直接保底生成在中心点天上
        return center + Vector3.up * 5f;
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int randomIndex = Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }

    // ==========================================
    // ⚙️ 弹窗系统
    // ==========================================
    public void OpenSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        if (currentState == GameState.Playing) Time.timeScale = 0f;
    }

    public void CloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (currentState == GameState.Playing)
        {
            Time.timeScale = 1f;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
        else if (currentState == GameState.StartMenu)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    public void OpenHelp() { if (helpPanel != null) helpPanel.SetActive(true); }
    public void CloseHelp() { if (helpPanel != null) helpPanel.SetActive(false); }

    // ==========================================
    // 🎬 运镜逻辑
    // ==========================================
    private void UpdateFlashCutCamera(float deltaTime)
    {
        if (menuCamera == null || player == null) return;

        shotTimer -= deltaTime;
        if (shotTimer <= 0f) CutToNextShot();

        Vector3 localOffset = shotOffsets[currentShotIndex];
        Vector3 basePos = player.transform.position + player.transform.right * localOffset.x + player.transform.up * localOffset.y + player.transform.forward * localOffset.z;

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
        currentDriftDirection = new Vector3(Random.Range(-1f, 1f), Random.Range(0f, 0.2f), Random.Range(-1f, 1f)).normalized;
    }

    // ==========================================
    // 底层 UI 与控制逻辑
    // ==========================================
    private void GenerateCinematicGradients()
    {
        int resolution = 256; float solidBlackRatio = 0.35f;
        Texture2D gradientTex = new Texture2D(1, resolution, TextureFormat.ARGB32, false);
        gradientTex.wrapMode = TextureWrapMode.Clamp; gradientTex.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < resolution; y++)
        {
            float t = y / (float)(resolution - 1);
            float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 1f - solidBlackRatio, t));
            gradientTex.SetPixel(0, y, new Color(0, 0, 0, Mathf.Clamp01(alpha + UnityEngine.Random.Range(-0.015f, 0.015f))));
        }
        gradientTex.Apply();
        if (topCinematicBar != null) { topCinematicBar.texture = gradientTex; topCinematicBar.uvRect = new Rect(0, 0, 1, 1); }
        if (bottomCinematicBar != null) { bottomCinematicBar.texture = gradientTex; bottomCinematicBar.uvRect = new Rect(0, 1, 1, -1); }
    }

    private void InitializeStartMenu()
    {
        currentState = GameState.StartMenu;

        // 【核心修改】：不仅关闭控制，直接把物理引擎冻住，防止地图外的车掉下虚空
        SetAllVehiclesActive(false, true);

        if (playCamera != null) playCamera.SetActive(false);
        if (menuCamera != null) menuCamera.SetActive(true);
        if (startMenuPanel != null) startMenuPanel.SetActive(true);
        if (hudPanel != null) hudPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (centerMessageText != null) centerMessageText.gameObject.SetActive(false);

        Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
        shotTimer = 0f;
        StartCoroutine(PlayMenuEnterAnimation());
    }

    private IEnumerator PlayMenuEnterAnimation()
    {
        float elapsed = 0f;
        Vector2 topStart = new Vector2(0, 300f); Vector2 botStart = new Vector2(0, -300f); Vector2 contentStart = new Vector2(-1200f, 0f);
        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / animationDuration);
            if (topCinematicBar != null) topCinematicBar.rectTransform.anchoredPosition = Vector2.Lerp(topStart, Vector2.zero, t);
            if (bottomCinematicBar != null) bottomCinematicBar.rectTransform.anchoredPosition = Vector2.Lerp(botStart, Vector2.zero, t);
            if (startMenuContent != null) startMenuContent.anchoredPosition = Vector2.Lerp(contentStart, Vector2.zero, t);
            yield return null;
        }
    }

    private IEnumerator PlayMenuExitAnimation()
    {
        Cursor.visible = false; Cursor.lockState = CursorLockMode.Locked;
        float elapsed = 0f;
        Vector2 topEnd = new Vector2(0, 300f); Vector2 botEnd = new Vector2(0, -300f); Vector2 contentEnd = new Vector2(-1200f, 0f);
        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / animationDuration);
            if (topCinematicBar != null) topCinematicBar.rectTransform.anchoredPosition = Vector2.Lerp(Vector2.zero, topEnd, t);
            if (bottomCinematicBar != null) bottomCinematicBar.rectTransform.anchoredPosition = Vector2.Lerp(Vector2.zero, botEnd, t);
            if (startMenuContent != null) startMenuContent.anchoredPosition = Vector2.Lerp(Vector2.zero, contentEnd, t);
            yield return null;
        }

        // 【核心流转】：UI 退场后，不立刻开始，而是进入传送生成序列
        StartCoroutine(SpawnEnemiesSequence());
    }

    private void StartGame()
    {
        currentState = GameState.Playing;

        // 【核心修改】：比赛开始，解冻所有物理引擎并赋予控制权
        SetAllVehiclesActive(true, false);

        if (menuCamera != null) menuCamera.SetActive(false);
        if (playCamera != null) playCamera.SetActive(true);
        if (startMenuPanel != null) startMenuPanel.SetActive(false);
        if (hudPanel != null) hudPanel.SetActive(true);

        UpdateUI(true);
    }

    // 【核心修改】：加入了 isKinematic 的控制
    private void SetAllVehiclesActive(bool isActive, bool freezePhysics)
    {
        if (player != null)
        {
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc != null) pc.enabled = isActive;

            // 玩家最好不要完全冻住位置，但如果不动也可以开启
            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = freezePhysics;
        }
        foreach (var enemy in enemies)
        {
            if (enemy != null)
            {
                EnemyAIController ai = enemy.GetComponent<EnemyAIController>();
                if (ai != null) ai.enabled = isActive;

                Rigidbody rb = enemy.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = freezePhysics;
            }
        }
    }

    private void CheckEliminations()
    {
        if (player != null && player.transform.position.y < deathYThreshold)
        {
            EliminateCar(player, "Player"); TriggerGameOver(false);
        }
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            GameObject enemy = enemies[i];
            if (enemy != null && enemy.transform.position.y < deathYThreshold)
            {
                EliminateCar(enemy, enemy.name); enemies.RemoveAt(i); UpdateUI(false);
                if (enemies.Count == 0 && player != null) TriggerGameOver(true);
            }
        }
    }

    private void EliminateCar(GameObject car, string carName)
    {
        if (eliminationVFX != null) Instantiate(eliminationVFX, car.transform.position + Vector3.up * 2f, Quaternion.identity);
        ShowKillFeed($"{carName} fell out"); Destroy(car);
    }

    private void ShowKillFeed(string message) { if (killFeedText != null) StartCoroutine(FeedRoutine(message)); }

    private IEnumerator FeedRoutine(string message)
    {
        activeFeeds.Add(message); if (activeFeeds.Count > maxFeedLines) activeFeeds.RemoveAt(0); UpdateFeedText();
        yield return new WaitForSeconds(feedStayTime);
        if (activeFeeds.Contains(message)) { activeFeeds.Remove(message); UpdateFeedText(); }
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
        float elapsed = 0f; Vector3 targetScale = originalAliveTextScale * pulseScaleMultiplier;
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
        Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
        if (centerMessageText != null)
        {
            centerMessageText.gameObject.SetActive(true);
            centerMessageText.text = isWin ? "VICTORY" : "ELIMINATED";
            centerMessageText.color = isWin ? new Color(1f, 0.8f, 0f) : new Color(1f, 0.2f, 0.2f);
        }
    }

    public void RestartGame() { Time.timeScale = 1f; SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }

    // ==========================================
    // 🎨 辅助可视化 (只在 Scene 窗口显示)
    // ==========================================
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Vector3 center = spawnAreaCenter != null ? spawnAreaCenter.position : Vector3.zero;

        // 画出生成范围的盒子 (Y 轴高度供示意)
        Gizmos.DrawCube(center + Vector3.up * 5f, new Vector3(spawnAreaSize.x, 10f, spawnAreaSize.y));

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(center + Vector3.up * 5f, new Vector3(spawnAreaSize.x, 10f, spawnAreaSize.y));
    }
}