using UnityEngine;

public class SimpleCarController : MonoBehaviour
{
    [Header("控制参数")]
    public float moveDistance = 1.0f;  // 每次点击移动的距离
    public float rotateAngle = 15.0f;  // 每次点击旋转的角度
    public float scaleStep = 0.2f;     // 每次点击缩放的比例

    // 用来存储初始状态的变量
    private Vector3 originPosition;
    private Quaternion originRotation;
    private Vector3 originScale;
    private Rigidbody rb;

    void Start()
    {
        // 在游戏启动的第一秒，记录下当前的坐标、角度和大小
        originPosition = transform.position;
        originRotation = transform.rotation;
        originScale = transform.localScale;

        // 获取刚体组件（为了重置物理速度）
        rb = GetComponent<Rigidbody>();
    }

    // --- 新增的重置功能 ---
    public void ResetCar()
    {
        // 1. 还原变换信息
        transform.position = originPosition;
        transform.rotation = originRotation;
        transform.localScale = originScale;

        // 2. 如果你有物理引擎（Rigidbody），必须清除惯性
        // 否则重置后小车可能还会带着之前的速度继续飞出去
        if (rb != null)
        {
            rb.velocity = Vector3.zero;         // 线性速度清零
            rb.angularVelocity = Vector3.zero;  // 旋转速度清零
        }

        Debug.Log("小车已重置回起始点");
    }

    // --- 移动控制 ---
    // 使用 Space.Self 确保车辆始终朝着自己车头的方向移动
    public void MoveForward() => transform.Translate(Vector3.forward * moveDistance, Space.Self);
    public void MoveBackward() => transform.Translate(Vector3.back * moveDistance, Space.Self);
    public void MoveLeft() => transform.Translate(Vector3.left * moveDistance, Space.Self);
    public void MoveRight() => transform.Translate(Vector3.right * moveDistance, Space.Self);

    // --- 旋转控制 (绕 Y 轴) ---
    public void RotateCW() => transform.Rotate(0, rotateAngle, 0, Space.Self);
    public void RotateCCW() => transform.Rotate(0, -rotateAngle, 0, Space.Self);

    // --- 缩放控制 ---
    public void ScaleUp() => transform.localScale += Vector3.one * scaleStep;
    public void ScaleDown()
    {
        // 增加一个安全限制，防止模型缩放到 0 甚至变成负数反转
        if (transform.localScale.x > 0.5f)
        {
            transform.localScale -= Vector3.one * scaleStep;
        }
    }
}