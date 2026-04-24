using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class EnemyAIController : MonoBehaviour
{
    // 状态机精简与升级：进攻、拉扯蓄力、战术撤退
    public enum AIState { Chasing, Repositioning, Retreating }

    [Header("Targeting & Free-for-All (大逃杀索敌)")]
    [Tooltip("每隔多久重新评估一次全场最佳目标")]
    public float targetUpdateInterval = 0.5f;
    [Tooltip("探测悬崖的动态基准距离")]
    public float baseLookAheadDistance = 4f;
    public float cliffCheckDepth = 2f;
    [Tooltip("静态避障距离")]
    public float obstacleAvoidDistance = 8f;

    [Header("Tactical Timers (战术时间)")]
    public Vector2 chaseTimeRange = new Vector2(4f, 6f);
    [Tooltip("撞击后后撤的时间，防止粘连")]
    public Vector2 retreatTimeRange = new Vector2(0.5f, 1.5f);
    [Tooltip("为了下一次高能冲锋而拉开距离的时间")]
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

    private Rigidbody rb;
    private bool isGrounded;
    private AIState currentState;
    private float stateTimer;

    // 大逃杀专属变量
    private Transform currentTarget;
    private Rigidbody targetRb;
    private float targetUpdateTimer;
    private Vector3 tacticalWaypoint; // 战术走位目标点

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0, -0.5f, 0);

        SwitchState(AIState.Chasing);
        FindBestTarget();
    }

    void FixedUpdate()
    {
        // --- 1. 全员恶人：动态索敌 ---
        targetUpdateTimer -= Time.fixedDeltaTime;
        if (targetUpdateTimer <= 0)
        {
            FindBestTarget();
            targetUpdateTimer = targetUpdateInterval;
        }

        // 如果场上真没目标了（比如都掉下去了），就在中心停着
        if (currentTarget == null) return;

        // --- 2. 物理环境同步 ---
        Vector3 rayStart = transform.position + Vector3.up * 0.1f;
        isGrounded = Physics.Raycast(rayStart, Vector3.down, groundCheckDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        if (!isGrounded || rb.velocity.y > 0.1f)
            rb.AddForce(Vector3.down * downForce * rb.mass, ForceMode.Force);

        Vector3 lateralVelocity = transform.right * Vector3.Dot(rb.velocity, transform.right);
        rb.AddForce(-lateralVelocity * lateralGrip * rb.mass, ForceMode.Force);

        if (!isGrounded) return;

        // --- 3. 反制僵局与状态机倒计时 ---
        stateTimer -= Time.fixedDeltaTime;

        // 【核心优化】：检测“二人转”僵局。
        // 如果正在追击，且双方距离很近，但相对速度极低，说明卡在一起或在绕圈，立刻进入拉扯蓄力状态
        if (currentState == AIState.Chasing && stateTimer < (chaseTimeRange.y - 1f))
        {
            float distToTarget = Vector3.Distance(transform.position, currentTarget.position);
            float relativeSpeed = (rb.velocity - (targetRb != null ? targetRb.velocity : Vector3.zero)).magnitude;

            if (distToTarget < 6f && relativeSpeed < 5f)
            {
                SwitchState(AIState.Repositioning);
            }
        }

        if (stateTimer <= 0)
        {
            if (currentState == AIState.Chasing) SwitchState(AIState.Repositioning);
            else SwitchState(AIState.Chasing);
        }

        // --- 4. 核心决策树 ---
        bool isAvoidingCliff = CheckForCliff(out float dynamicLookAhead);
        Vector3 targetDirection = Vector3.zero;

        if (isAvoidingCliff)
        {
            // 悬崖逃生逻辑：强行指向场地绝对中心 (0,0,0)
            Vector3 dirToCenter = (Vector3.zero - transform.position).normalized;
            dirToCenter.y = 0;
            targetDirection = dirToCenter != Vector3.zero ? dirToCenter : -transform.forward;
        }
        else
        {
            Vector3 baseDirection = Vector3.zero;

            if (currentState == AIState.Chasing)
            {
                // 预判追踪：预判目标 0.5 秒后的位置
                Vector3 targetVelocity = targetRb != null ? targetRb.velocity : Vector3.zero;
                Vector3 futurePos = currentTarget.position + targetVelocity * 0.5f;
                baseDirection = (futurePos - transform.position).normalized;
            }
            else if (currentState == AIState.Repositioning)
            {
                // 拉扯蓄力：开向之前计算好的战术点，拉开距离
                baseDirection = (tacticalWaypoint - transform.position).normalized;

                // 如果已经到了战术点附近，提前结束拉扯，回头猛撞
                if (Vector3.Distance(transform.position, tacticalWaypoint) < 4f)
                {
                    SwitchState(AIState.Chasing);
                }
            }
            else if (currentState == AIState.Retreating)
            {
                // 战术后撤：刚撞完，背对目标跑路
                baseDirection = (transform.position - currentTarget.position).normalized;
            }

            baseDirection.y = 0;

            // 静态避障 (忽略当前锁定的目标)
            targetDirection = CalculateObstacleAvoidance(baseDirection.normalized);
        }

        MoveAndSteer(targetDirection, isAvoidingCliff);
    }

    private void FindBestTarget()
    {
        // 寻找所有的玩家和其他敌人
        List<GameObject> allFighters = new List<GameObject>();
        allFighters.AddRange(GameObject.FindGameObjectsWithTag("Player"));
        allFighters.AddRange(GameObject.FindGameObjectsWithTag("Enemy"));

        float closestDist = float.MaxValue;
        Transform bestTarget = null;

        foreach (GameObject fighter in allFighters)
        {
            // 忽略自己
            if (fighter == this.gameObject) continue;

            // 忽略已经掉下悬崖的车 (假设低于 -5f 算掉落)
            if (fighter.transform.position.y < -5f) continue;

            float dist = Vector3.Distance(transform.position, fighter.transform.position);

            // 优先选择距离最近的
            if (dist < closestDist)
            {
                closestDist = dist;
                bestTarget = fighter.transform;
            }
        }

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

        // 计算一个能获得绝佳加速距离的战术点
        // 策略：背对目标，并稍微偏向场地的中心，防止把自己逼到死角
        Vector3 awayFromTarget = (transform.position - currentTarget.position).normalized;
        Vector3 towardCenter = (Vector3.zero - transform.position).normalized;

        // 混合方向：70% 远离目标，30% 偏向场地中心
        Vector3 mixedDirection = (awayFromTarget * 0.7f + towardCenter * 0.3f).normalized;
        mixedDirection.y = 0;

        // 设定战术点在 25 米开外，给予足够的直线加速空间
        tacticalWaypoint = transform.position + mixedDirection * 25f;
    }

    void OnCollisionEnter(Collision collision)
    {
        // 只要撞到的是玩家或敌人，且力度足够，就立刻后撤
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("Enemy"))
        {
            if (collision.relativeVelocity.magnitude >= hitForceThreshold)
            {
                // 如果当前正在追击，立刻转为后撤，防止粘连
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
            // 避障逻辑：忽略自己。
            // 【核心】：不要避开当前正在追击的目标，否则就撞不上去了！
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

        // --- 同归于尽终极判定 ---
        if (cliffDanger && currentState == AIState.Chasing && currentTarget != null)
        {
            // 如果悬崖前方正好是我当前锁定的目标，直接撞下去！
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
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            float currentSpeed = rb.velocity.magnitude;
            float speedFactor = Mathf.Clamp01(currentSpeed / speedForMaxTurn);
            float turnMultiplier = Mathf.Max(speedFactor, 0.2f);

            // 【已修复】：移除了悬崖状态下的强制 3 倍转弯速度
            float currentTurnSpeed = turnSpeed * turnMultiplier;
            Quaternion newRotation = Quaternion.RotateTowards(rb.rotation, targetRotation, currentTurnSpeed * Time.fixedDeltaTime);
            rb.MoveRotation(newRotation);
        }

        float currentForwardSpeed = Vector3.Dot(rb.velocity, transform.forward);
        float absSpeed = Mathf.Abs(currentForwardSpeed);

        if (cliffDanger)
        {
            // 悬崖边缘紧急刹车
            if (currentForwardSpeed > 2f)
                rb.AddForce(-transform.forward * fastAcceleration * rb.mass, ForceMode.Force);
            return;
        }

        if (absSpeed < maxSpeed)
        {
            float currentAccel = absSpeed < fastAccelThreshold ? fastAcceleration :
                topEndAcceleration * (1f - ((absSpeed - fastAccelThreshold) / (maxSpeed - fastAccelThreshold)));

            rb.AddForce(transform.forward * currentAccel * rb.mass, ForceMode.Force);
        }
    }
}