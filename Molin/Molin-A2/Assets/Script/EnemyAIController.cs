using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class EnemyAIController : MonoBehaviour
{
    public enum AIState { Chasing, Repositioning, Retreating }

    [Header("Targeting & Free-for-All (大逃杀索敌)")]
    public float targetUpdateInterval = 0.5f;
    public float baseLookAheadDistance = 4f;
    public float cliffCheckDepth = 2f;
    public float obstacleAvoidDistance = 8f;

    [Header("Tactical Timers (战术时间)")]
    public Vector2 chaseTimeRange = new Vector2(4f, 6f);
    public Vector2 retreatTimeRange = new Vector2(0.5f, 1.5f);
    public Vector2 repositionTimeRange = new Vector2(1.5f, 2.5f);
    public float hitForceThreshold = 8f;

    [Header("Engine Power (同步玩家双段油门)")]
    public float fastAcceleration = 3500f;
    public float fastAccelThreshold = 40f;
    public float topEndAcceleration = 1200f;
    public float maxSpeed = 70f;
    public float turnSpeed = 150f;
    public float speedForMaxTurn = 10f;

    [Header("Custom Physics (同步玩家)")]
    public float lateralGrip = 5f;
    public float coastingDrag = 2f;
    public float downForce = 50f;
    public float groundCheckDistance = 1.0f;

    [Header("🎵 Engine Audio (引擎音效系统)")]
    public EngineAudioConfig engineAudio;

    private Rigidbody rb;
    private bool isGrounded;
    private AIState currentState;
    private float stateTimer;

    private Transform currentTarget;
    private Rigidbody targetRb;
    private float targetUpdateTimer;
    private Vector3 tacticalWaypoint;

    // 【行为逻辑优化新增】内部缓存变量，防扎堆分离向量
    private Vector3 currentSeparationVector;

    private bool isAcceleratingCurrently;

    // 引擎音效源
    private AudioSource srcIdle, srcLowAccel, srcLowDecel, srcHighAccel, srcHighDecel;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0, -0.5f, 0);

        SwitchState(AIState.Chasing);
        FindBestTargetAndCalculateSeparation();
        InitializeEngineAudio();
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

    void Update()
    {
        UpdateEngineAudio();
    }

    void FixedUpdate()
    {
        // --- 1. 全员恶人：动态索敌与防扎堆阵型运算 ---
        targetUpdateTimer -= Time.fixedDeltaTime;
        if (targetUpdateTimer <= 0)
        {
            FindBestTargetAndCalculateSeparation();
            targetUpdateTimer = targetUpdateInterval;
        }

        if (currentTarget == null) return;

        // --- 2. 物理环境同步 ---
        Vector3 rayStart = transform.position + Vector3.up * 0.1f;
        isGrounded = Physics.Raycast(rayStart, Vector3.down, groundCheckDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        if (!isGrounded || rb.velocity.y > 0.1f)
            rb.AddForce(Vector3.down * downForce * rb.mass, ForceMode.Force);

        Vector3 lateralVelocity = transform.right * Vector3.Dot(rb.velocity, transform.right);
        rb.AddForce(-lateralVelocity * lateralGrip * rb.mass, ForceMode.Force);

        if (!isGrounded) return;

        // --- 3. 反制僵局与死斗解锁 ---
        stateTimer -= Time.fixedDeltaTime;

        if (currentState == AIState.Chasing && stateTimer < (chaseTimeRange.y - 1f))
        {
            float distToTarget = Vector3.Distance(transform.position, currentTarget.position);
            float relativeSpeed = (rb.velocity - (targetRb != null ? targetRb.velocity : Vector3.zero)).magnitude;

            // 【优化】：不仅是速度低，如果两人贴贴且车头对顶着，强制拉开
            bool isHeadOn = targetRb != null && Vector3.Dot(rb.velocity.normalized, targetRb.velocity.normalized) < -0.5f;

            if (distToTarget < 8f && (relativeSpeed < 5f || isHeadOn))
            {
                SwitchState(AIState.Repositioning);
            }
        }

        if (stateTimer <= 0)
        {
            if (currentState == AIState.Chasing) SwitchState(AIState.Repositioning);
            else SwitchState(AIState.Chasing);
        }

        // --- 4. 核心战术决策树 ---
        bool isAvoidingCliff = CheckForCliff(out float dynamicLookAhead);
        Vector3 targetDirection = Vector3.zero;

        if (isAvoidingCliff)
        {
            Vector3 dirToCenter = (Vector3.zero - transform.position).normalized;
            dirToCenter.y = 0;
            targetDirection = dirToCenter != Vector3.zero ? dirToCenter : -transform.forward;
        }
        else
        {
            Vector3 baseDirection = Vector3.zero;

            if (currentState == AIState.Chasing)
            {
                Vector3 targetVelocity = targetRb != null ? targetRb.velocity : Vector3.zero;
                Vector3 toTarget = currentTarget.position - transform.position;
                float dist = toTarget.magnitude;

                if (dist > 18f)
                {
                    // 【优化：精准预判】远距离时，使用双方相对速度来计算提前量，拦截更准
                    float closingSpeed = Mathf.Max((rb.velocity - targetVelocity).magnitude, 10f);
                    float leadTime = Mathf.Clamp(dist / closingSpeed, 0.2f, 1.5f);
                    Vector3 futurePos = currentTarget.position + targetVelocity * leadTime;
                    baseDirection = (futurePos - transform.position).normalized;
                }
                else
                {
                    // 【优化：疯狗模式】近距离直接指向车身
                    baseDirection = toTarget.normalized;

                    // 【核心优化：防平行伴飞 (PIT机动)】如果距离较近且正在并排跑（速度方向高度一致）
                    if (dist < 10f && Vector3.Dot(rb.velocity.normalized, targetVelocity.normalized) > 0.8f)
                    {
                        // 强行施加一个向内横向挤压的绝对向量，直接撞目标侧面
                        baseDirection = (baseDirection + toTarget.normalized * 2f).normalized;
                    }
                }

                // 【核心优化：防扎堆算法融合】
                if (currentSeparationVector != Vector3.zero)
                {
                    // 距离目标越远，越要保持队形散开；距离越近，则忽视分离强行聚拢撞击
                    float sepWeight = Mathf.Clamp01((dist - 8f) / 10f);
                    baseDirection = (baseDirection + currentSeparationVector.normalized * sepWeight).normalized;
                }
            }
            else if (currentState == AIState.Repositioning)
            {
                baseDirection = (tacticalWaypoint - transform.position).normalized;
                if (Vector3.Distance(transform.position, tacticalWaypoint) < 4f)
                {
                    SwitchState(AIState.Chasing);
                }
            }
            else if (currentState == AIState.Retreating)
            {
                baseDirection = (transform.position - currentTarget.position).normalized;
            }

            baseDirection.y = 0;
            targetDirection = CalculateObstacleAvoidance(baseDirection.normalized);
        }

        MoveAndSteer(targetDirection, isAvoidingCliff);
    }

    private void FindBestTargetAndCalculateSeparation()
    {
        List<GameObject> allFighters = new List<GameObject>();
        allFighters.AddRange(GameObject.FindGameObjectsWithTag("Player"));
        allFighters.AddRange(GameObject.FindGameObjectsWithTag("Enemy"));

        float closestScore = float.MaxValue;
        Transform bestTarget = null;

        currentSeparationVector = Vector3.zero;

        foreach (GameObject fighter in allFighters)
        {
            if (fighter == this.gameObject) continue;

            float dist = Vector3.Distance(transform.position, fighter.transform.position);

            // 【优化：空间分离收集】扫描周围 12 米内的其他车辆，计算排斥力，防止扎堆
            if (fighter.CompareTag("Enemy") && dist < 12f)
            {
                Vector3 diff = transform.position - fighter.transform.position;
                // 距离越近，排斥力越大
                currentSeparationVector += diff.normalized / Mathf.Max(dist, 0.1f);
            }

            if (fighter.transform.position.y < -5f) continue;

            // 【优化：目标粘性与随机扰动】
            float score = dist;

            // 粘性：倾向于继续追击当前目标，防止频繁切目标导致车头反复横跳
            if (currentTarget != null && fighter.transform == currentTarget) score -= 15f;

            // 扰动：加入一点随机距离干扰，让场上的 AI 自然分化，不会在同一时间全部去集火一个人
            score += Random.Range(0f, 15f);

            if (score < closestScore)
            {
                closestScore = score;
                bestTarget = fighter.transform;
            }
        }

        currentSeparationVector.y = 0;
        currentTarget = bestTarget;
        if (currentTarget != null)
        {
            targetRb = currentTarget.GetComponent<Rigidbody>();
        }
    }

    private void SwitchState(AIState newState)
    {
        currentState = newState;

        switch (newState)
        {
            case AIState.Chasing:
                stateTimer = Random.Range(chaseTimeRange.x, chaseTimeRange.y);
                break;
            case AIState.Retreating:
                stateTimer = Random.Range(retreatTimeRange.x, retreatTimeRange.y);
                break;
            case AIState.Repositioning:
                stateTimer = Random.Range(repositionTimeRange.x, repositionTimeRange.y);
                CalculateTacticalWaypoint();
                break;
        }
    }

    private void CalculateTacticalWaypoint()
    {
        if (currentTarget == null) return;
        Vector3 awayFromTarget = (transform.position - currentTarget.position).normalized;
        Vector3 towardCenter = (Vector3.zero - transform.position).normalized;

        // 【优化：拉扯走位】加入随机的横向规避，拉扯轨迹更像真人，且不容易直直撞墙
        Vector3 randomSide = transform.right * Random.Range(-0.6f, 0.6f);
        Vector3 mixedDirection = (awayFromTarget * 0.5f + towardCenter * 0.4f + randomSide).normalized;
        mixedDirection.y = 0;

        tacticalWaypoint = transform.position + mixedDirection * 25f;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("Enemy"))
        {
            if (collision.relativeVelocity.magnitude >= hitForceThreshold)
            {
                if (currentState == AIState.Chasing)
                {
                    SwitchState(AIState.Retreating);
                }
            }
        }
    }

    private Vector3 CalculateObstacleAvoidance(Vector3 currentDirection)
    {
        Vector3 finalDirection = currentDirection;
        Vector3 forward = transform.forward;
        forward.y = 0;
        forward.Normalize();

        Vector3 leftAngle = Quaternion.Euler(0, -35, 0) * forward;
        Vector3 rightAngle = Quaternion.Euler(0, 35, 0) * forward;
        Vector3 origin = transform.position + Vector3.up * 0.5f;

        bool isBlockedCenter = IsObstacle(origin, forward, obstacleAvoidDistance);
        bool isBlockedLeft = IsObstacle(origin, leftAngle, obstacleAvoidDistance * 0.8f);
        bool isBlockedRight = IsObstacle(origin, rightAngle, obstacleAvoidDistance * 0.8f);

        if (isBlockedCenter)
        {
            if (!isBlockedLeft) finalDirection = Quaternion.Euler(0, -75, 0) * forward;
            else if (!isBlockedRight) finalDirection = Quaternion.Euler(0, 75, 0) * forward;
            else finalDirection = -forward;
        }
        else if (isBlockedLeft)
        {
            finalDirection = Quaternion.Euler(0, 45, 0) * forward;
        }
        else if (isBlockedRight)
        {
            finalDirection = Quaternion.Euler(0, -45, 0) * forward;
        }

        return finalDirection.normalized;
    }

    private bool IsObstacle(Vector3 origin, Vector3 dir, float dist)
    {
        if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform == currentTarget) return false;

            if (!hit.transform.IsChildOf(this.transform) && hit.transform != this.transform && hit.normal.y < 0.5f)
            {
                return true;
            }
        }
        return false;
    }

    private bool CheckForCliff(out float dynamicLookAhead)
    {
        float currentSpeed = rb.velocity.magnitude;
        dynamicLookAhead = baseLookAheadDistance + (currentSpeed * 0.1f);

        Vector3 centerProbe = transform.position + transform.forward * dynamicLookAhead;
        Vector3 leftProbe = transform.position + (transform.forward + transform.right * -0.6f).normalized * dynamicLookAhead;
        Vector3 rightProbe = transform.position + (transform.forward + transform.right * 0.6f).normalized * dynamicLookAhead;

        centerProbe.y += 0.5f; leftProbe.y += 0.5f; rightProbe.y += 0.5f;

        bool hitCenter = Physics.Raycast(centerProbe, Vector3.down, cliffCheckDepth, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        bool hitLeft = Physics.Raycast(leftProbe, Vector3.down, cliffCheckDepth, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        bool hitRight = Physics.Raycast(rightProbe, Vector3.down, cliffCheckDepth, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        bool cliffDanger = !(hitCenter && hitLeft && hitRight);

        if (cliffDanger && currentState == AIState.Chasing && currentTarget != null)
        {
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, transform.forward, out RaycastHit hit, dynamicLookAhead + 5f))
            {
                if (hit.transform == currentTarget)
                {
                    cliffDanger = false;
                }
            }
        }

        return cliffDanger;
    }

    private void MoveAndSteer(Vector3 direction, bool cliffDanger)
    {
        isAcceleratingCurrently = false;

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            float currentSpeed = rb.velocity.magnitude;
            float speedFactor = Mathf.Clamp01(currentSpeed / speedForMaxTurn);
            float turnMultiplier = Mathf.Max(speedFactor, 0.2f);

            float currentTurnSpeed = turnSpeed * turnMultiplier;
            Quaternion newRotation = Quaternion.RotateTowards(rb.rotation, targetRotation, currentTurnSpeed * Time.fixedDeltaTime);
            rb.MoveRotation(newRotation);
        }

        float currentForwardSpeed = Vector3.Dot(rb.velocity, transform.forward);
        float absSpeed = Mathf.Abs(currentForwardSpeed);

        if (cliffDanger)
        {
            if (currentForwardSpeed > 2f)
                rb.AddForce(-transform.forward * fastAcceleration * rb.mass, ForceMode.Force);
            return;
        }

        if (absSpeed < maxSpeed)
        {
            float currentAccel = absSpeed < fastAccelThreshold ? fastAcceleration :
                topEndAcceleration * (1f - ((absSpeed - fastAccelThreshold) / (maxSpeed - fastAccelThreshold)));

            rb.AddForce(transform.forward * currentAccel * rb.mass, ForceMode.Force);

            isAcceleratingCurrently = true;
        }
    }

    private void UpdateEngineAudio()
    {
        if (!engineAudio.enableEngineSounds || !isGrounded) return;

        float currentForwardSpeed = Vector3.Dot(rb.velocity, transform.forward);
        float absSpeed = Mathf.Abs(currentForwardSpeed);

        bool isHighSpeed = absSpeed >= fastAccelThreshold;
        bool isAccelerating = isAcceleratingCurrently;

        bool isIdle = absSpeed < 0.5f && !isAccelerating;

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