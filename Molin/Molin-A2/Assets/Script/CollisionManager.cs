using UnityEngine;
using Cinemachine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody), typeof(AudioSource))]
public class CollisionManager : MonoBehaviour
{
    [Header("动态力度设置")]
    public float impactThreshold = 8f;
    public float forceMultiplierPerSpeed = 0.5f;

    [Header("基于动向的击退规则")]
    public float frontalReductionRatio = 0.3f;
    public float frontalAngleRange = 45f;
    public float lowSpeedThreshold = 3f;

    [Header("🔥 特效与位置调整")]
    public GameObject sparkVFXPrefab;
    [Tooltip("针对单一目标对象的碰撞冷却时间。建议保持 0.1 到 0.15 秒，可完美过滤多碰撞箱连击，且不影响多车混战")]
    public float collisionCooldown = 0.15f;
    public float vfxNormalOffset = 0.1f;
    public Vector3 vfxExtraOffset = new Vector3(0, 0, 0);

    [Header("🎵 音效设置")]
    public AudioClip violentCrashSound;
    public AudioClip lightCrashSound;
    [Range(0f, 1f)]
    public float lightSoundVolume = 0.5f;

    private Rigidbody rb;
    private AudioSource audioSource;
    private CinemachineImpulseSource impulseSource;

    // 【核心修复】：使用字典记录针对每个具体独立对象的最后碰撞时间
    private Dictionary<int, float> objectCollisionCooldowns = new Dictionary<int, float>();
    private Vector3 lastFrameVelocity;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();
        impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    void FixedUpdate()
    {
        lastFrameVelocity = rb.velocity;
        lastFrameVelocity.y = 0;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("Enemy"))
        {
            // 【核心修复】：基于对方 InstanceID 进行独立冷却判定
            int otherID = collision.gameObject.GetInstanceID();
            if (objectCollisionCooldowns.TryGetValue(otherID, out float lastTime))
            {
                // 如果与这辆特定车的碰撞还在冷却时间内，拦截同一帧的多碰撞箱重复报警
                if (Time.time - lastTime < collisionCooldown) return;
            }

            // 更新对该特定车辆的最后碰撞时间
            objectCollisionCooldowns[otherID] = Time.time;

            float relativeSpeed = collision.relativeVelocity.magnitude;

            // ==========================================
            // 1. 物理受力计算 (双方各自独立执行，互不干涉)
            // ==========================================
            if (relativeSpeed >= impactThreshold)
            {
                Vector3 contactPoint = collision.contacts[0].point;
                Vector3 dirToImpact = (contactPoint - transform.position).normalized;
                dirToImpact.y = 0;

                float currentKnockbackRatio = 1.0f;
                float currentSpeed = lastFrameVelocity.magnitude;

                if (currentSpeed > lowSpeedThreshold)
                {
                    Vector3 moveDir = lastFrameVelocity.normalized;
                    float angleToImpact = Vector3.Angle(moveDir, dirToImpact);

                    if (angleToImpact <= frontalAngleRange)
                    {
                        currentKnockbackRatio = frontalReductionRatio;
                    }
                }

                float finalDeltaVelocity = relativeSpeed * forceMultiplierPerSpeed;
                Vector3 knockbackDir = -dirToImpact;

                rb.AddForce(knockbackDir * finalDeltaVelocity * currentKnockbackRatio, ForceMode.VelocityChange);

                if (impulseSource != null && gameObject.CompareTag("Player"))
                {
                    float shakeStrength = Mathf.Clamp01(relativeSpeed / 20f);
                    impulseSource.GenerateImpulse(shakeStrength);
                }

                // ==========================================
                // 2. 猛烈撞击的视听表现 (利用 ID 对比进行去重，防止生成两份特效)
                // ==========================================
                if (gameObject.GetInstanceID() > otherID)
                {
                    if (sparkVFXPrefab != null)
                    {
                        Vector3 spawnPosition = contactPoint + (collision.contacts[0].normal * vfxNormalOffset) + vfxExtraOffset;
                        Instantiate(sparkVFXPrefab, spawnPosition, Quaternion.LookRotation(collision.contacts[0].normal));
                    }

                    if (violentCrashSound != null)
                    {
                        audioSource.pitch = Random.Range(0.8f, 1.1f);
                        audioSource.PlayOneShot(violentCrashSound);
                    }
                }
            }
            // ==========================================
            // 3. 普通轻微碰撞
            // ==========================================
            else if (relativeSpeed > 1f)
            {
                // 轻微碰撞通常是持续的摩擦，利用 ID 去重确保只有一方发声即可
                if (lightCrashSound != null && gameObject.GetInstanceID() > otherID)
                {
                    audioSource.pitch = Random.Range(0.9f, 1.2f);
                    audioSource.PlayOneShot(lightCrashSound, lightSoundVolume);
                }
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Vector3 direction = Application.isPlaying && lastFrameVelocity.magnitude > 0.1f
                            ? lastFrameVelocity.normalized
                            : transform.forward;

        direction.y = 0;
        if (direction == Vector3.zero) return;

        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Vector3 origin = transform.position + Vector3.up * 0.5f;

        Vector3 leftBoundary = Quaternion.Euler(0, -frontalAngleRange, 0) * direction;
        Vector3 rightBoundary = Quaternion.Euler(0, frontalAngleRange, 0) * direction;

        Gizmos.DrawRay(origin, leftBoundary * 5f);
        Gizmos.DrawRay(origin, rightBoundary * 5f);

        Gizmos.color = Color.green;
        Gizmos.DrawRay(origin, direction * 5f);
    }
}