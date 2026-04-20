using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BlockPush : MonoBehaviour
{
    public float pushForce = 10f; // 推动速度，可自行调整

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    // 用 OnCollisionStay 检测持续接触
    void OnCollisionStay(Collision other)
    {
        // 检测是否是小车碰撞
        if (other.collider.CompareTag("Player"))
        {
            // 计算推动方向
            Vector3 pushDir = other.transform.forward; 
            pushDir.y = 0; // 保持水平推动

            // 施加力
            rb.AddForce(pushDir * pushForce, ForceMode.Force);
        }
    }
}