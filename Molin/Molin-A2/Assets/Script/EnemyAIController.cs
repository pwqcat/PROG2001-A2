using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyAIController : MonoBehaviour
{
    public enum AIState { Chasing, Wandering }

    [Header("Targeting & AI")]
    public Transform player;
    public float avoidDistance = 4f;
    [Tooltip("基础悬崖探测距离。现在的探测距离会根据车速自动延长！")]
    public float baseLookAheadDistance = 4f;
    public float cliffCheckDepth = 2f;

    [Header("AI State Timers (战术拉扯)")]
    [Tooltip("追击超时时间：追击这么久还没撞到，就主动放弃并开始游荡")]
    public float chaseDuration = 3f;
    [Tooltip("撞击得手后，或者超时放弃后，游荡调整的时间")]
    public float wanderDuration = 2.5f;
    [Tooltip("判定为‘有效攻击’的撞击阈值，对应你 CollisionManager 里的音效阈值")]
    public float hitForceThreshold = 8f;

    [Header("Engine Power (同步玩家双段油门)")]
    public float fastAcceleration = 3500f;
    public float fastAccelThreshold = 40f;
    public float topEndAcceleration = 1200f;
    public float maxSpeed = 70f;
    public float turnSpeed = 150f;

    [Header("Custom Physics (同步玩家)")]
    public float lateralGrip = 5f;
    public float coastingDrag = 2f;
    public float downForce = 50f;
    public float groundCheckDistance = 1.0f;

    private Rigidbody rb;
    private Vector3 targetDirection;
    private bool isGrounded;
    private bool isAvoidingCliff = false;
    private AIState currentState;
    private float stateTimer;
    private Vector3 currentWanderTarget;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0, -0.5f, 0);

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        currentState = AIState.Chasing;
        stateTimer = chaseDuration;
    }

    void FixedUpdate()
    {
        if (player == null) return;

        Vector3 rayStart = transform.position + Vector3.up * 0.1f;
        isGrounded = Physics.Raycast(rayStart, Vector3.down, groundCheckDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        if (!isGrounded || rb.velocity.y > 0.1f)
        {
            rb.AddForce(Vector3.down * downForce * rb.mass, ForceMode.Force);
        }

        Vector3 lateralVelocity = transform.right * Vector3.Dot(rb.velocity, transform.right);
        rb.AddForce(-lateralVelocity * lateralGrip * rb.mass, ForceMode.Force);

        if (!isGrounded) return;

        // --- 1. 战术状态机倒计时 ---
        stateTimer -= Time.fixedDeltaTime;
        if (stateTimer <= 0)
        {
            if (currentState == AIState.Chasing)
            {
                // 追击超时，强制放弃，转为游荡
                ForceWander();
            }
            else
            {
                // 游荡结束，重新锁定玩家
                currentState = AIState.Chasing;
                stateTimer = chaseDuration + Random.Range(-0.5f, 1.0f);
            }
        }

        // --- 2. 决策树执行 ---
        isAvoidingCliff = CheckForCliff();

        if (isAvoidingCliff)
        {
            Vector3 dirToCenter = (Vector3.zero - transform.position).normalized;
            dirToCenter.y = 0;
            targetDirection = dirToCenter != Vector3.zero ? dirToCenter : -transform.forward;

            if (currentState == AIState.Wandering)
            {
                currentWanderTarget = transform.position + targetDirection * 20f;
            }
        }
        else
        {
            Vector3 baseDirection = Vector3.zero;

            if (currentState == AIState.Chasing)
            {
                baseDirection = (player.position - transform.position).normalized;
            }
            else if (currentState == AIState.Wandering)
            {
                if (Vector3.Distance(transform.position, currentWanderTarget) < 5f)
                {
                    PickNewWanderTarget();
                }
                baseDirection = (currentWanderTarget - transform.position).normalized;
            }

            baseDirection.y = 0;
            targetDirection = CalculateAvoidance(baseDirection);

            if (currentState == AIState.Wandering && targetDirection != baseDirection)
            {
                currentWanderTarget = transform.position + targetDirection * 15f;
            }
        }

        MoveAndSteer(targetDirection, isAvoidingCliff);
    }

    // --- 新增：强制进入游荡状态 ---
    private void ForceWander()
    {
        currentState = AIState.Wandering;
        stateTimer = wanderDuration + Random.Range(0f, 1f);
        PickNewWanderTarget();
    }

    // --- 新增：成功撞击玩家后的“一击脱离” ---
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            // 如果撞击力度达到了触发音效的阈值
            if (collision.relativeVelocity.magnitude >= hitForceThreshold)
            {
                // 如果当前正在追击，立刻满足，转为游荡撤退
                if (currentState == AIState.Chasing)
                {
                    ForceWander();
                }
            }
        }
    }

    private void PickNewWanderTarget()
    {
        float randomAngle = Random.Range(-70f, 70f);
        Vector3 randomDir = Quaternion.Euler(0, randomAngle, 0) * transform.forward;
        currentWanderTarget = transform.position + randomDir * 25f;
    }

    private bool CheckForCliff()
    {
        // 核心修复：根据当前速度动态延长雷达探测距离（开得越快，探测越远）
        float currentSpeed = rb.velocity.magnitude;
        float dynamicLookAhead = baseLookAheadDistance + (currentSpeed * 0.1f);

        // 核心修复：三点式扇形探测，涵盖车头左、中、右，防止漂移时侧漏
        Vector3 centerProbe = transform.position + transform.forward * dynamicLookAhead;
        Vector3 leftProbe = transform.position + (transform.forward + transform.right * -0.5f).normalized * dynamicLookAhead;
        Vector3 rightProbe = transform.position + (transform.forward + transform.right * 0.5f).normalized * dynamicLookAhead;

        centerProbe.y += 0.5f;
        leftProbe.y += 0.5f;
        rightProbe.y += 0.5f;

        bool hitCenter = Physics.Raycast(centerProbe, Vector3.down, cliffCheckDepth, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        bool hitLeft = Physics.Raycast(leftProbe, Vector3.down, cliffCheckDepth, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        bool hitRight = Physics.Raycast(rightProbe, Vector3.down, cliffCheckDepth, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        // 只要任何一个探测点踩空，就判定前方是悬崖
        return !(hitCenter && hitLeft && hitRight);
    }

    private Vector3 CalculateAvoidance(Vector3 baseDirection)
    {
        Vector3 finalDirection = baseDirection;
        Vector3 forward = transform.forward;
        Vector3 leftAngle = Quaternion.Euler(0, -35, 0) * forward;
        Vector3 rightAngle = Quaternion.Euler(0, 35, 0) * forward;

        RaycastHit centerHit;
        bool hitCenter = Physics.Raycast(transform.position, forward, out centerHit, avoidDistance);
        bool hitLeft = Physics.Raycast(transform.position, leftAngle, out _, avoidDistance);
        bool hitRight = Physics.Raycast(transform.position, rightAngle, out _, avoidDistance);

        if (hitCenter && centerHit.collider.CompareTag("Enemy"))
        {
            if (!hitLeft) finalDirection = Quaternion.Euler(0, -75, 0) * forward;
            else if (!hitRight) finalDirection = Quaternion.Euler(0, 75, 0) * forward;
            else finalDirection = -forward;
        }

        return finalDirection.normalized;
    }

    private void MoveAndSteer(Vector3 direction, bool cliffDanger)
    {
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            float currentTurnSpeed = cliffDanger ? turnSpeed * 3.0f : turnSpeed;
            Quaternion newRotation = Quaternion.RotateTowards(rb.rotation, targetRotation, currentTurnSpeed * Time.fixedDeltaTime);
            rb.MoveRotation(newRotation);
        }

        float currentForwardSpeed = Vector3.Dot(rb.velocity, transform.forward);
        float absSpeed = Mathf.Abs(currentForwardSpeed);

        // --- 核心修复：面临悬崖时的紧急制动 ---
        if (cliffDanger)
        {
            // 发现悬崖时，切断油门。如果此时车还在往前冲，立刻猛踩刹车
            if (currentForwardSpeed > 2f)
            {
                rb.AddForce(-transform.forward * fastAcceleration * rb.mass, ForceMode.Force);
            }
            return; // 直接 return，不执行下方的加速代码
        }

        // --- 正常赛车双段油门逻辑 ---
        if (absSpeed < maxSpeed)
        {
            float currentAccel = 0f;

            if (absSpeed < fastAccelThreshold)
            {
                currentAccel = fastAcceleration;
            }
            else
            {
                float speedPastThreshold = absSpeed - fastAccelThreshold;
                float highSpeedRange = maxSpeed - fastAccelThreshold;
                float decayFactor = 1f - (speedPastThreshold / highSpeedRange);

                currentAccel = topEndAcceleration * decayFactor;
            }

            rb.AddForce(transform.forward * currentAccel * rb.mass, ForceMode.Force);
        }
    }
}