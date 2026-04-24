using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Cinemachine;

[System.Serializable]
public class MilestoneSFX
{
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;
    public float delay = 0f;
}

[System.Serializable]
public class AliveMilestone
{
    public int survivorCount;
    public Color textColor = Color.white;
    public List<MilestoneSFX> soundEffects = new List<MilestoneSFX>();
}

[System.Serializable]
public class CountdownTextElement
{
    [Tooltip("文本内容 (支持 <sprite name=\"X\">)")]
    public string text = "READY";
    [Tooltip("字体颜色")]
    public Color color = Color.white;
    [Tooltip("相对中心的偏移位置 (X, Y)")]
    public Vector2 anchoredPosition = Vector2.zero;
}

[System.Serializable]
public class CountdownStep
{
    [Header("文本组合")]
    [Tooltip("该阶段同时弹出的所有文本（可无限添加）")]
    public List<CountdownTextElement> textElements = new List<CountdownTextElement>();

    [Header("样式与动画")]
    public TMP_FontAsset customFont;
    public TMP_SpriteAsset customSpriteAsset;
    public float targetScale = 1.0f;
    public float popDuration = 0.25f;
    public float stayDuration = 0.6f;

    [Header("音效")]
    public AudioClip sfx;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    public float sfxDelay = 0f;
}

[System.Serializable]
public class VfxControlSettings
{
    public bool enableReverse = true;
    public float totalDuration = 2.0f;
    public float playbackSpeed = 1.0f;
}

[System.Serializable]
public class EliminationVFXConfig
{
    public GameObject vfxPrefab;
    public Vector3 offset = new Vector3(0f, 2f, 0f);
    public float scale = 1.0f;
    public VfxControlSettings controlSettings;
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { StartMenu, MenuTransition, SpawningAndBlending, Countdown, Playing, GameOver }
    [Header("当前状态 (只读)")]
    public GameState currentState = GameState.StartMenu;

    [Header("⚙️ 全局设置与弹窗")]
    public GameObject settingsPanel;
    public GameObject helpPanel;
    public bool IsAnyPopupOpen => (settingsPanel != null && settingsPanel.activeSelf) || (helpPanel != null && helpPanel.activeSelf);

    [Header("✨ 动态生成 (传送登场) 系统")]
    public GameObject spawnVFX;
    public float spawnVfxScale = 1.5f;
    public Vector3 spawnVfxOffset = Vector3.zero;
    public VfxControlSettings spawnVfxControl;

    public AudioClip spawnSFX;
    [Range(0f, 1f)] public float spawnSfxVolume = 1f;
    public float spawnInterval = 0.4f;

    public Transform spawnAreaCenter;
    public Vector2 spawnAreaSize = new Vector2(60f, 60f);
    public float spawnHeight = 1.0f;

    public float minDistanceFromPlayer = 15f;
    public LayerMask obstacleLayer;
    public float obstacleCheckRadius = 3f;

    [Header("🎬 狂野飙车级闪切运镜")]
    public GameObject playCamera;
    public GameObject menuCamera;
    public float timePerShot = 2.8f;
    public float driftSpeed = 0.4f;
    public float carScreenOffsetRight = 1.5f;
    public float minCameraHeight = 0.5f;
    public float cameraBlendWaitTime = 2.0f;

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

    [Header("🏁 赛前倒计时系统 (Countdown)")]
    [Tooltip("此 Text 将作为克隆模板，放在屏幕中心即可")]
    public TextMeshProUGUI countdownTextTemplate;
    public List<CountdownStep> countdownSteps = new List<CountdownStep>();

    private List<TextMeshProUGUI> countdownTextPool = new List<TextMeshProUGUI>();

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
    public List<EliminationVFXConfig> eliminationVFXList = new List<EliminationVFXConfig>();

    [Header("HUD 数据与动态反馈 (Juice)")]
    public TextMeshProUGUI aliveCountText;
    public TextMeshProUGUI killFeedText;
    public int maxFeedLines = 4;
    public float feedStayTime = 3.5f;

    public AudioSource uiAudioSource;
    public AudioSource settingsUIAudioSource;

    public float pulseScaleMultiplier = 1.5f;
    public float pulseDuration = 0.3f;
    public List<AliveMilestone> milestones = new List<AliveMilestone>();

    [Header("结算音效 (支持多层级与延迟)")]
    public List<MilestoneSFX> victorySoundEffects = new List<MilestoneSFX>();
    public List<MilestoneSFX> eliminationSoundEffects = new List<MilestoneSFX>();

    [Header("全局大字 (结算)")]
    public TextMeshProUGUI centerMessageText;

    private GameObject player;
    private List<GameObject> enemies = new List<GameObject>();
    private HashSet<GameObject> spawnedEnemies = new HashSet<GameObject>();
    private List<string> activeFeeds = new List<string>();
    private int lastTotalAlive = -1;
    private Coroutine pulseCoroutine;
    private Vector3 originalAliveTextScale;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (settingsUIAudioSource != null)
        {
            settingsUIAudioSource.ignoreListenerPause = true;
        }

        CinemachineCore.GetInputAxis = CustomCinemachineInput;
    }

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        enemies.AddRange(GameObject.FindGameObjectsWithTag("Enemy"));

        if (killFeedText != null) killFeedText.text = "";
        if (aliveCountText != null) originalAliveTextScale = aliveCountText.transform.localScale;

        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (helpPanel != null) helpPanel.SetActive(false);

        if (countdownTextTemplate != null) countdownTextTemplate.gameObject.SetActive(false);

        GenerateCinematicGradients();
        InitializeStartMenu();
    }

    private float CustomCinemachineInput(string axisName)
    {
        if (currentState != GameState.Playing || IsAnyPopupOpen) return 0f;
        return Input.GetAxis(axisName);
    }

    private void UpdateCursorState()
    {
        if (IsAnyPopupOpen || currentState == GameState.StartMenu || currentState == GameState.GameOver)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (helpPanel != null && helpPanel.activeSelf) CloseHelp();
            else if (settingsPanel != null && settingsPanel.activeSelf) CloseSettings();
            else if (currentState != GameState.GameOver) OpenSettings();
        }

        switch (currentState)
        {
            case GameState.StartMenu:
                UpdateFlashCutCamera(Time.unscaledDeltaTime);
                if (Input.GetKeyDown(KeyCode.Space) && !IsAnyPopupOpen)
                {
                    currentState = GameState.MenuTransition;
                    UpdateCursorState();
                    StartCoroutine(PlayMenuExitAnimation());
                }
                break;

            case GameState.MenuTransition:
                UpdateFlashCutCamera(Time.unscaledDeltaTime);
                break;

            case GameState.SpawningAndBlending:
            case GameState.Countdown:
                break;

            case GameState.Playing:
                CheckEliminations();
                break;
        }
    }

    // ==========================================
    // ⚙️ 弹窗与系统时间/音频管控
    // ==========================================
    public void OpenSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(true);
        if (currentState != GameState.StartMenu && currentState != GameState.GameOver)
        {
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }

        // Settings 面板设置为 30000 层
        ForceUIOnTop(settingsPanel, 30000);
        UpdateCursorState();
    }

    public void CloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        Time.timeScale = 1f;
        AudioListener.pause = false;
        UpdateCursorState();
    }

    public void OpenHelp()
    {
        if (helpPanel != null) helpPanel.SetActive(true);
        // Help 面板设置为 30001 层，以防它在 Settings 面板内部被打开时层级冲突
        ForceUIOnTop(helpPanel, 30001);
        UpdateCursorState();
    }

    public void CloseHelp()
    {
        if (helpPanel != null) helpPanel.SetActive(false);
        UpdateCursorState();
    }

    // 【核心修复】：双管齐下的层级控制机制
    private void ForceUIOnTop(GameObject panel, int popupSortingOrder)
    {
        if (panel == null) return;

        // 1. 先把主画布提到 20000，确保包括倒计时在内的所有 UI 都能遮住 3D 粒子特效
        Canvas rootCanvas = panel.transform.root.GetComponentInChildren<Canvas>();
        if (rootCanvas != null)
        {
            rootCanvas.overrideSorting = true;
            rootCanvas.sortingOrder = 20000;
        }

        // 2. 给当前弹窗单独挂一个子 Canvas，层级提到 30000+，直接凌驾于整个 UI 体系之上！
        Canvas panelCanvas = panel.GetComponent<Canvas>();
        if (panelCanvas == null)
        {
            panelCanvas = panel.AddComponent<Canvas>();
            // 必须加 GraphicRaycaster，不然挂了 Canvas 后按钮会点不到
            if (panel.GetComponent<GraphicRaycaster>() == null)
            {
                panel.AddComponent<GraphicRaycaster>();
            }
        }
        panelCanvas.overrideSorting = true;
        panelCanvas.sortingOrder = popupSortingOrder;
    }

    // ==========================================
    // ✨ 特效生命周期接管
    // ==========================================
    private IEnumerator HandleVfxLifecycle(GameObject vfxInstance, VfxControlSettings settings)
    {
        if (vfxInstance == null) yield break;

        ParticleSystem[] particles = vfxInstance.GetComponentsInChildren<ParticleSystem>();

        foreach (var ps in particles)
        {
            var main = ps.main;
            main.simulationSpeed = settings.playbackSpeed;
        }

        if (!settings.enableReverse) yield break;

        float halfRealTime = (settings.totalDuration / 2f) / settings.playbackSpeed;
        yield return new WaitForSeconds(halfRealTime);
        if (vfxInstance == null) yield break;

        foreach (var ps in particles)
        {
            var main = ps.main;
            main.simulationSpeed = -settings.playbackSpeed;
        }

        yield return new WaitForSeconds(halfRealTime);
        if (vfxInstance != null) Destroy(vfxInstance);
    }

    // ==========================================
    // ✨ 相机过渡与异步生成
    // ==========================================
    private IEnumerator SpawnAndBlendSequence()
    {
        currentState = GameState.SpawningAndBlending;
        UpdateCursorState();

        if (menuCamera != null) menuCamera.SetActive(false);
        if (playCamera != null) playCamera.SetActive(true);

        StartCoroutine(SpawnEnemiesRoutine());

        yield return new WaitForSeconds(cameraBlendWaitTime);

        StartCoroutine(CountdownSequence());
    }

    private IEnumerator SpawnEnemiesRoutine()
    {
        ShuffleList(enemies);
        foreach (var enemy in enemies)
        {
            if (enemy == null) continue;

            Vector3 spawnPos = GetValidSpawnPosition();
            enemy.transform.position = spawnPos;
            spawnedEnemies.Add(enemy);

            if (spawnVFX != null)
            {
                Vector3 vfxPos = spawnPos + spawnVfxOffset;
                GameObject vfxInstance = Instantiate(spawnVFX, vfxPos, Quaternion.identity);

                vfxInstance.transform.localScale = Vector3.one * spawnVfxScale;
                foreach (ParticleSystem ps in vfxInstance.GetComponentsInChildren<ParticleSystem>())
                {
                    var main = ps.main; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }
                StartCoroutine(HandleVfxLifecycle(vfxInstance, spawnVfxControl));
            }

            if (spawnSFX != null && uiAudioSource != null)
            {
                uiAudioSource.PlayOneShot(spawnSFX, spawnSfxVolume);
            }

            if (currentState == GameState.Playing)
            {
                EnemyAIController ai = enemy.GetComponent<EnemyAIController>();
                if (ai != null) ai.enabled = true;
                Rigidbody rb = enemy.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = false;
            }

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    // ==========================================
    // 🏁 倒计时弹字系统 (多元素对象池版)
    // ==========================================
    private TextMeshProUGUI GetCountdownTextFromPool(int index)
    {
        if (index >= countdownTextPool.Count)
        {
            GameObject newTextObj = Instantiate(countdownTextTemplate.gameObject, countdownTextTemplate.transform.parent);
            TextMeshProUGUI newText = newTextObj.GetComponent<TextMeshProUGUI>();
            countdownTextPool.Add(newText);
        }
        return countdownTextPool[index];
    }

    private IEnumerator CountdownSequence()
    {
        currentState = GameState.Countdown;
        UpdateCursorState();

        foreach (var step in countdownSteps)
        {
            List<TextMeshProUGUI> currentStepActiveTexts = new List<TextMeshProUGUI>();

            if (countdownTextTemplate != null)
            {
                for (int i = 0; i < step.textElements.Count; i++)
                {
                    var elementConfig = step.textElements[i];
                    TextMeshProUGUI txt = GetCountdownTextFromPool(i);

                    txt.gameObject.SetActive(true);
                    txt.text = elementConfig.text;
                    txt.color = elementConfig.color;
                    txt.rectTransform.anchoredPosition = elementConfig.anchoredPosition;

                    if (step.customFont != null) txt.font = step.customFont;
                    if (step.customSpriteAsset != null) txt.spriteAsset = step.customSpriteAsset;

                    txt.transform.localScale = Vector3.zero;
                    currentStepActiveTexts.Add(txt);
                }
            }

            if (step.sfx != null && uiAudioSource != null)
            {
                if (step.sfxDelay > 0f) StartCoroutine(PlayDelayedSFX(step.sfx, step.sfxVolume, step.sfxDelay));
                else uiAudioSource.PlayOneShot(step.sfx, step.sfxVolume);
            }

            float animTimer = 0f;
            while (animTimer < step.popDuration)
            {
                animTimer += Time.deltaTime;
                float t = Mathf.Clamp01(animTimer / step.popDuration);
                float popCurve = 1f - Mathf.Pow(1f - t, 3f);

                float currentOvershootScale = step.targetScale * 1.1f;
                Vector3 currentScaleVector = Vector3.one * currentOvershootScale;

                foreach (var txt in currentStepActiveTexts)
                {
                    txt.transform.localScale = Vector3.LerpUnclamped(Vector3.zero, currentScaleVector, popCurve);
                }
                yield return null;
            }

            foreach (var txt in currentStepActiveTexts)
            {
                txt.transform.localScale = Vector3.one * step.targetScale;
            }
            yield return new WaitForSeconds(step.stayDuration);

            foreach (var txt in currentStepActiveTexts)
            {
                txt.transform.localScale = Vector3.zero;
                txt.gameObject.SetActive(false);
            }
        }

        StartGame();
    }

    private IEnumerator PlayDelayedSFX(AudioClip clip, float volume, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (clip != null && uiAudioSource != null) uiAudioSource.PlayOneShot(clip, volume);
    }

    private Vector3 GetValidSpawnPosition()
    {
        Vector3 center = spawnAreaCenter != null ? spawnAreaCenter.position : Vector3.zero;

        for (int i = 0; i < 30; i++)
        {
            float randomX = Random.Range(-spawnAreaSize.x / 2f, spawnAreaSize.x / 2f);
            float randomZ = Random.Range(-spawnAreaSize.y / 2f, spawnAreaSize.y / 2f);
            Vector3 testPos = center + new Vector3(randomX, 50f, randomZ);

            if (Physics.Raycast(testPos, Vector3.down, out RaycastHit hit, 100f))
            {
                Vector3 potentialPos = hit.point + Vector3.up * spawnHeight;

                if (player != null && Vector3.Distance(potentialPos, player.transform.position) < minDistanceFromPlayer) continue;
                if (Physics.CheckSphere(potentialPos, obstacleCheckRadius, obstacleLayer)) continue;

                return potentialPos;
            }
        }
        return center + Vector3.up * (spawnHeight + 4f);
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

        spawnedEnemies.Clear();
        FreezeAllVehiclesForStart();

        if (playCamera != null) playCamera.SetActive(false);
        if (menuCamera != null) menuCamera.SetActive(true);
        if (startMenuPanel != null) startMenuPanel.SetActive(true);
        if (hudPanel != null) hudPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (centerMessageText != null) centerMessageText.gameObject.SetActive(false);

        UpdateCursorState();
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

        StartCoroutine(SpawnAndBlendSequence());
    }

    private void StartGame()
    {
        currentState = GameState.Playing;

        UnfreezePlayerAndSpawnedEnemies();

        if (startMenuPanel != null) startMenuPanel.SetActive(false);
        if (hudPanel != null) hudPanel.SetActive(true);

        UpdateUI(true);
        UpdateCursorState();
    }

    private void FreezeAllVehiclesForStart()
    {
        if (player != null)
        {
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc != null) pc.enabled = false;
            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
        }
        foreach (var enemy in enemies)
        {
            if (enemy != null)
            {
                EnemyAIController ai = enemy.GetComponent<EnemyAIController>();
                if (ai != null) ai.enabled = false;
                Rigidbody rb = enemy.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;
            }
        }
    }

    private void UnfreezePlayerAndSpawnedEnemies()
    {
        if (player != null)
        {
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc != null) pc.enabled = true;
            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = false;
        }

        foreach (var enemy in spawnedEnemies)
        {
            if (enemy != null)
            {
                EnemyAIController ai = enemy.GetComponent<EnemyAIController>();
                if (ai != null) ai.enabled = true;
                Rigidbody rb = enemy.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = false;
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
                EliminateCar(enemy, enemy.name);
                enemies.RemoveAt(i);
                UpdateUI(false);
                if (enemies.Count == 0 && player != null) TriggerGameOver(true);
            }
        }
    }

    private void EliminateCar(GameObject car, string carName)
    {
        foreach (var vfxConfig in eliminationVFXList)
        {
            if (vfxConfig.vfxPrefab != null)
            {
                Vector3 vfxPos = car.transform.position + vfxConfig.offset;
                GameObject vfxInstance = Instantiate(vfxConfig.vfxPrefab, vfxPos, Quaternion.identity);

                vfxInstance.transform.localScale = Vector3.one * vfxConfig.scale;
                foreach (ParticleSystem ps in vfxInstance.GetComponentsInChildren<ParticleSystem>())
                {
                    var main = ps.main; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }

                StartCoroutine(HandleVfxLifecycle(vfxInstance, vfxConfig.controlSettings));
            }
        }

        ShowKillFeed($"{carName} fell out");
        spawnedEnemies.Remove(car);
        Destroy(car);
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

                            if (uiAudioSource != null)
                            {
                                foreach (var sfx in m.soundEffects)
                                {
                                    if (sfx.clip != null)
                                    {
                                        if (sfx.delay > 0f) StartCoroutine(PlayDelayedSFX(sfx.clip, sfx.volume, sfx.delay));
                                        else uiAudioSource.PlayOneShot(sfx.clip, sfx.volume);
                                    }
                                }
                            }
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
        UpdateCursorState();

        if (uiAudioSource != null)
        {
            List<MilestoneSFX> sfxList = isWin ? victorySoundEffects : eliminationSoundEffects;
            foreach (var sfx in sfxList)
            {
                if (sfx.clip != null)
                {
                    if (sfx.delay > 0f) StartCoroutine(PlayDelayedSFX(sfx.clip, sfx.volume, sfx.delay));
                    else uiAudioSource.PlayOneShot(sfx.clip, sfx.volume);
                }
            }
        }

        if (centerMessageText != null)
        {
            centerMessageText.gameObject.SetActive(true);
            centerMessageText.text = isWin ? "VICTORY" : "ELIMINATED";
            centerMessageText.color = isWin ? new Color(1f, 0.8f, 0f) : new Color(1f, 0.2f, 0.2f);
        }
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Vector3 center = spawnAreaCenter != null ? spawnAreaCenter.position : Vector3.zero;
        Gizmos.DrawCube(center + Vector3.up * 5f, new Vector3(spawnAreaSize.x, 10f, spawnAreaSize.y));
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(center + Vector3.up * 5f, new Vector3(spawnAreaSize.x, 10f, spawnAreaSize.y));
    }
}