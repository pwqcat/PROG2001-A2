using UnityEngine;
using Cinemachine;

[RequireComponent(typeof(Rigidbody), typeof(AudioSource))]
public class CollisionManager : MonoBehaviour
{
    [Header("动态力度设置")]
    [Tooltip("触发猛烈撞击特效和受力的最低相对速度")]
    public float impactThreshold = 8f;
    [Tooltip("相对速度每增加 1，增加的受力倍数。用于实现速度越快撞得越远")]
    public float forceMultiplierPerSpeed = 3f;

    [Header("不对称击退规则")]
    [Tooltip("我是攻击方时，我承受的反作用力比例 (你要求的 10%)")]
    public float attackerKnockbackRatio = 0.1f;
    [Tooltip("我是受击方，或者双方互撞时，承受的击退比例 (你要求的 100%)")]
    public float victimKnockbackRatio = 1.0f;
    [Tooltip("判定谁是攻击方的速度差阈值。我的接近速度比对方高出这个值，我就是攻击方")]
    public float aggressorSpeedDifference = 3f;

    [Header("🔥 特效与位置调整")]
    public GameObject sparkVFXPrefab;
    public float vfxCooldown = 0.1f;
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
    private float lastVfxTime = -1f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();
        impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("Enemy"))
        {
            float relativeSpeed = collision.relativeVelocity.magnitude;

            // ==========================================
            // 1. 物理受力计算 (动态力度 + 攻击方判定)
            // ==========================================
            if (relativeSpeed >= impactThreshold)
            {
                Rigidbody otherRb = collision.rigidbody;

                // 计算从我指向对方的向量
                Vector3 dirToOther = (collision.transform.position - transform.position).normalized;
                dirToOther.y = 0;

                // 使用点乘 (Dot) 计算双方朝着对方移动的速度分量
                float myApproachSpeed = Vector3.Dot(rb.velocity, dirToOther);
                float otherApproachSpeed = Vector3.Dot(otherRb.velocity, -dirToOther);

                // 确定当前的击退系数
                float currentKnockbackRatio;

                if (myApproachSpeed - otherApproachSpeed > aggressorSpeedDifference)
                {
                    // 我的接近速度明显大于对方，我是攻击方
                    currentKnockbackRatio = attackerKnockbackRatio;
                }
                else if (otherApproachSpeed - myApproachSpeed > aggressorSpeedDifference)
                {
                    // 对方的接近速度明显大于我，我是受击方
                    currentKnockbackRatio = victimKnockbackRatio;
                }
                else
                {
                    // 速度差不满足阈值，判定为对头互撞
                    currentKnockbackRatio = victimKnockbackRatio;
                }

                // 动态计算最终力度：相对速度 * 力度乘数
                float finalForce = relativeSpeed * forceMultiplierPerSpeed;

                // 击退方向与对方相反
                Vector3 knockbackDir = -dirToOther;

                // 施加计算后的击退力。注意这里乘以了 rb.mass 确保质量大的车具有该有的动量
                rb.AddForce(knockbackDir * finalForce * currentKnockbackRatio, ForceMode.Impulse);

                if (impulseSource != null && gameObject.CompareTag("Player"))
                {
                    float shakeStrength = Mathf.Clamp01(relativeSpeed / 20f);
                    impulseSource.GenerateImpulse(shakeStrength);
                }
            }

            // ==========================================
            // 2. 视听表现去重逻辑
            // ==========================================
            if (gameObject.GetInstanceID() > collision.gameObject.GetInstanceID())
            {
                if (Time.time - lastVfxTime >= vfxCooldown)
                {
                    lastVfxTime = Time.time;
                    ContactPoint contact = collision.contacts[0];

                    if (relativeSpeed >= impactThreshold)
                    {
                        if (sparkVFXPrefab != null)
                        {
                            Vector3 spawnPosition = contact.point + (contact.normal * vfxNormalOffset) + vfxExtraOffset;
                            Instantiate(sparkVFXPrefab, spawnPosition, Quaternion.LookRotation(contact.normal));
                        }

                        if (violentCrashSound != null)
                        {
                            audioSource.pitch = Random.Range(0.8f, 1.1f);
                            audioSource.PlayOneShot(violentCrashSound);
                        }
                    }
                    else
                    {
                        if (lightCrashSound != null)
                        {
                            audioSource.pitch = Random.Range(0.9f, 1.2f);
                            audioSource.PlayOneShot(lightCrashSound, lightSoundVolume);
                        }
                    }
                }
            }
        }
    }
}