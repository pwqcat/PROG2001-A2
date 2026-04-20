using UnityEngine;

public class CarController : MonoBehaviour
{
    public float moveSpeed = 8f;
    public float turnSpeed = 90f;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void FixedUpdate()
    {
        float v = Input.GetAxis("Vertical");
        float h = Input.GetAxis("Horizontal");

        // 前后移动
        Vector3 moveDir = transform.forward * v * moveSpeed;

        // ✅ 核心修复：永远贴地，Y轴强制不积累
        rb.velocity = new Vector3(moveDir.x, -0.1f, moveDir.z);

        // 转向
        float turn = h * turnSpeed * Time.fixedDeltaTime;
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0, turn, 0));
    }
}