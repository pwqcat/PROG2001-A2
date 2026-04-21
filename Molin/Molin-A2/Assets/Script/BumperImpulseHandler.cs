using UnityEngine;
using Cinemachine;

public class BumperImpulseHandler : MonoBehaviour
{
    private CinemachineImpulseSource _impulseSource;

    void Start()
    {
        // 即使碰撞箱在子物体上，脚本只要在带 Rigidbody 的父物体上就能工作
        _impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        // 1. 计算相对碰撞速度
        float impactForce = collision.relativeVelocity.magnitude;

        // 2. 根据力度触发不同等级的震动
        if (impactForce > 30f) // 忽略轻微摩擦
        {
            float shakeIntensity = Mathf.Clamp01(impactForce / 250f);

            // 触发震动
            _impulseSource.GenerateImpulse(Vector3.one * shakeIntensity);

            // 3. 成品级优化：加入微小的“顿帧” (Hitstop)
            // 撞击瞬间让时间稍微慢一点点，能极大增加钢铁撞击的重量感
            if (impactForce > 150f)
            {
                StartCoroutine(HitStop(0.05f));
            }
        }
    }

    private System.Collections.IEnumerator HitStop(float duration)
    {
        Time.timeScale = 0.05f; // 时间几乎停滞
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1.0f; // 恢复正常
    }
}