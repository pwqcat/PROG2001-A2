using UnityEngine;

[System.Serializable]
public class EngineAudioClip
{
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;
}

[System.Serializable]
public class EngineAudioConfig
{
    public bool enableEngineSounds = true;

    [Header("5-Phase Audio Clips (五阶段音效)")]
    [Tooltip("车辆完全静止且未踩油门时的低沉怠速声")]
    public EngineAudioClip idle;
    public EngineAudioClip lowAccel;
    public EngineAudioClip lowDecel;
    public EngineAudioClip highAccel;
    public EngineAudioClip highDecel;

    [Header("Dynamic Settings (动态参数)")]
    [Tooltip("基础音高 (怠速时的音高)")]
    [Range(0.1f, 3f)] public float pitchBase = 0.8f;
    [Tooltip("随速度增加，音高最多飙升多少")]
    [Range(0f, 3f)] public float pitchSpeedModifier = 0.7f;
    [Tooltip("音效状态切换时的平滑渐变速度")]
    public float crossfadeSpeed = 8f;

    [Header("🔊 Volume & 3D Spatial (音量与空间感)")]
    [Tooltip("全局音量放大器 (如果觉得声音小，可以适当调大这个乘数)")]
    [Range(0.1f, 5f)] public float masterVolumeMultiplier = 1f;
    [Tooltip("0 = 纯2D (无视距离，声音最大) | 1 = 纯3D (随距离衰减)")]
    [Range(0f, 1f)] public float spatialBlend = 0f;
    [Tooltip("在这个距离内，声音保持最大绝对音量，不会衰减")]
    public float minDistance = 15f;
    [Tooltip("声音能传播的最远距离")]
    public float maxDistance = 100f;
}

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Engine Power (双段式赛车油门)")]
    public float fastAcceleration = 3500f;
    public float fastAccelThreshold = 40f;
    public float topEndAcceleration = 1200f;
    public float maxSpeed = 70f;

    [Header("Steering & Handling")]
    public float maxTurnSpeed = 150f;
    public float speedForMaxTurn = 10f;

    [Header("Custom Physics (街机轮胎物理)")]
    public float lateralGrip = 5f;
    public float coastingDrag = 2f;
    public float downForce = 50f;

    [Header("Ground Detection")]
    public float groundCheckDistance = 1.0f;

    [Header("🎵 Engine Audio (引擎音效系统)")]
    public EngineAudioConfig engineAudio;

    [Header("💥 Collision Audio (高速撞击反馈)")]
    [Tooltip("触发高速撞击音效的最低速度阈值")]
    public float highSpeedCollisionThreshold = 30f;
    [Tooltip("撞击音效的触发冷却时间(秒)，防止连续卡点爆音")]
    public float collisionSfxCooldown = 0.3f;

    [Tooltip("当别人被玩家撞飞（且别人速度达标）时播放的音效")]
    public AudioClip enemyKnockedSfx;
    [Range(0f, 1f)] public float enemyKnockedVolume = 1f;

    [Tooltip("当玩家自己被撞飞（且玩家速度达标）时播放的音效")]
    public AudioClip playerKnockedSfx;
    [Range(0f, 1f)] public float playerKnockedVolume = 1f;

    private Rigidbody rb;
    private float verticalInput;
    private float horizontalInput;
    private bool isGrounded;

    // 引擎音效源
    private AudioSource srcIdle, srcLowAccel, srcLowDecel, srcHighAccel, srcHighDecel;

    // 撞击音效源与计时器
    private AudioSource collisionAudioSource;
    private float currentCollisionCooldown = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0, -0.5f, 0);

        InitializeEngineAudio();
        InitializeCollisionAudio();
    }

    private void InitializeEngineAudio()
    {
        if (!engineAudio.enableEngineSounds) return;

        srcIdle = CreateAudioSource(engineAudio.idle);
        srcLowAccel = CreateAudioSource(engineAudio.lowAccel);
        srcLowDecel = CreateAudioSource(engineAudio.lowDecel);
        srcHighAccel = CreateAudioSource(engineAudio.highAccel);
        srcHighDecel = CreateAudioSource(engineAudio.highDecel);
    }

    private AudioSource CreateAudioSource(EngineAudioClip config)
    {
        AudioSource src = gameObject.AddComponent<AudioSource>();

        src.spatialBlend = engineAudio.spatialBlend;
        src.rolloffMode = AudioRolloffMode.Logarithmic;
        src.minDistance = engineAudio.minDistance;
        src.maxDistance = engineAudio.maxDistance;

        src.loop = true;
        src.playOnAwake = false;
        src.volume = 0f;

        if (config != null && config.clip != null)
        {
            src.clip = config.clip;
            src.Play();
        }
        return src;
    }

    private void InitializeCollisionAudio()
    {
        // 创建一个纯净的 2D 音效源，专门用于播放撞击声（不带任何距离衰减）
        collisionAudioSource = gameObject.AddComponent<AudioSource>();
        collisionAudioSource.spatialBlend = 0f;
        collisionAudioSource.playOnAwake = false;
        collisionAudioSource.loop = false;
    }

    void Update()
    {
        verticalInput = Input.GetAxis("Vertical");
        horizontalInput = Input.GetAxis("Horizontal");

        UpdateEngineAudio();

        // 更新撞击音效冷却时间
        if (currentCollisionCooldown > 0f)
        {
            currentCollisionCooldown -= Time.deltaTime;
        }
    }

    void FixedUpdate()
    {
        Vector3 rayStart = transform.position + Vector3.up * 0.1f;
        isGrounded = Physics.Raycast(rayStart, Vector3.down, groundCheckDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        if (!isGrounded || rb.velocity.y > 0.1f)
        {
            rb.AddForce(Vector3.down * downForce * rb.mass, ForceMode.Force);
        }

        Vector3 lateralVelocity = transform.right * Vector3.Dot(rb.velocity, transform.right);
        rb.AddForce(-lateralVelocity * lateralGrip * rb.mass, ForceMode.Force);

        if (Mathf.Abs(verticalInput) < 0.05f)
        {
            Vector3 forwardVelocity = transform.forward * Vector3.Dot(rb.velocity, transform.forward);
            rb.AddForce(-forwardVelocity * coastingDrag * rb.mass, ForceMode.Force);
        }

        if (!isGrounded) return;

        float currentForwardSpeed = Vector3.Dot(rb.velocity, transform.forward);
        float absSpeed = Mathf.Abs(currentForwardSpeed);

        if (Mathf.Abs(verticalInput) > 0.05f)
        {
            float currentAccel = 0f;

            if (absSpeed < fastAccelThreshold)
            {
                currentAccel = fastAcceleration;
            }
            else if (absSpeed < maxSpeed)
            {
                float speedPastThreshold = absSpeed - fastAccelThreshold;
                float highSpeedRange = maxSpeed - fastAccelThreshold;
                float decayFactor = 1f - (speedPastThreshold / highSpeedRange);

                currentAccel = topEndAcceleration * decayFactor;
            }

            if (verticalInput < 0 && currentForwardSpeed > -maxSpeed * 0.5f)
            {
                rb.AddForce(transform.forward * verticalInput * currentAccel * rb.mass, ForceMode.Force);
            }
            else if (verticalInput > 0 && currentForwardSpeed < maxSpeed)
            {
                rb.AddForce(transform.forward * verticalInput * currentAccel * rb.mass, ForceMode.Force);
            }
        }

        float currentSpeed = rb.velocity.magnitude;
        float turnFactor = Mathf.Clamp01(currentSpeed / speedForMaxTurn);
        float turnMultiplier = Mathf.Max(turnFactor, Mathf.Abs(verticalInput) * 0.2f);

        if (Mathf.Abs(horizontalInput) > 0.05f && turnMultiplier > 0.01f)
        {
            float turn = horizontalInput * maxTurnSpeed * turnMultiplier * Time.fixedDeltaTime;
            if (verticalInput < -0.05f && currentForwardSpeed < 0.5f) turn = -turn;

            Quaternion turnRotation = Quaternion.Euler(0f, turn, 0f);
            rb.MoveRotation(rb.rotation * turnRotation);
        }
    }

    // --- 新增：物理碰撞事件监听 ---
    void OnCollisionEnter(Collision collision)
    {
        // 检查冷却
        if (currentCollisionCooldown > 0f) return;

        // 仅在撞击敌人时触发逻辑
        if (collision.gameObject.CompareTag("Enemy"))
        {
            Rigidbody enemyRb = collision.rigidbody;

            // Unity 在 OnCollisionEnter 时，双方的 velocity 已经是碰撞后的瞬间速度
            bool isEnemyKnockedFast = enemyRb != null && enemyRb.velocity.magnitude >= highSpeedCollisionThreshold;
            bool isPlayerKnockedFast = rb.velocity.magnitude >= highSpeedCollisionThreshold;

            bool playedAnySfx = false;

            // 1. 判定别人被撞飞
            if (isEnemyKnockedFast && enemyKnockedSfx != null)
            {
                collisionAudioSource.PlayOneShot(enemyKnockedSfx, enemyKnockedVolume);
                playedAnySfx = true;
            }

            // 2. 判定自己被撞飞
            if (isPlayerKnockedFast && playerKnockedSfx != null)
            {
                collisionAudioSource.PlayOneShot(playerKnockedSfx, playerKnockedVolume);
                playedAnySfx = true;
            }

            // 如果触发了任何音效，进入冷却
            if (playedAnySfx)
            {
                currentCollisionCooldown = collisionSfxCooldown;
            }
        }
    }

    private void UpdateEngineAudio()
    {
        if (!engineAudio.enableEngineSounds || !isGrounded) return;

        float currentForwardSpeed = Vector3.Dot(rb.velocity, transform.forward);
        float absSpeed = Mathf.Abs(currentForwardSpeed);

        bool isHighSpeed = absSpeed >= fastAccelThreshold;
        bool isAccelerating = false;

        if (Mathf.Abs(verticalInput) > 0.05f)
        {
            if (verticalInput > 0 && currentForwardSpeed >= -1f) isAccelerating = true;
            else if (verticalInput < 0 && currentForwardSpeed <= 1f) isAccelerating = true;
        }

        bool isIdle = absSpeed < 0.5f && Mathf.Abs(verticalInput) <= 0.05f;

        float targetIdle = isIdle ? Mathf.Clamp01(engineAudio.idle.volume * engineAudio.masterVolumeMultiplier) : 0f;
        float targetLowAccel = (!isIdle && !isHighSpeed && isAccelerating) ? Mathf.Clamp01(engineAudio.lowAccel.volume * engineAudio.masterVolumeMultiplier) : 0f;
        float targetLowDecel = (!isIdle && !isHighSpeed && !isAccelerating) ? Mathf.Clamp01(engineAudio.lowDecel.volume * engineAudio.masterVolumeMultiplier) : 0f;
        float targetHighAccel = (!isIdle && isHighSpeed && isAccelerating) ? Mathf.Clamp01(engineAudio.highAccel.volume * engineAudio.masterVolumeMultiplier) : 0f;
        float targetHighDecel = (!isIdle && isHighSpeed && !isAccelerating) ? Mathf.Clamp01(engineAudio.highDecel.volume * engineAudio.masterVolumeMultiplier) : 0f;

        float dt = Time.deltaTime * engineAudio.crossfadeSpeed;

        if (srcIdle != null) srcIdle.volume = Mathf.Lerp(srcIdle.volume, targetIdle, dt);
        if (srcLowAccel != null) srcLowAccel.volume = Mathf.Lerp(srcLowAccel.volume, targetLowAccel, dt);
        if (srcLowDecel != null) srcLowDecel.volume = Mathf.Lerp(srcLowDecel.volume, targetLowDecel, dt);
        if (srcHighAccel != null) srcHighAccel.volume = Mathf.Lerp(srcHighAccel.volume, targetHighAccel, dt);
        if (srcHighDecel != null) srcHighDecel.volume = Mathf.Lerp(srcHighDecel.volume, targetHighDecel, dt);

        float currentPitch = engineAudio.pitchBase + (absSpeed / maxSpeed) * engineAudio.pitchSpeedModifier;

        if (srcIdle != null) srcIdle.pitch = currentPitch;
        if (srcLowAccel != null) srcLowAccel.pitch = currentPitch;
        if (srcLowDecel != null) srcLowDecel.pitch = currentPitch;
        if (srcHighAccel != null) srcHighAccel.pitch = currentPitch;
        if (srcHighDecel != null) srcHighDecel.pitch = currentPitch;
    }
}