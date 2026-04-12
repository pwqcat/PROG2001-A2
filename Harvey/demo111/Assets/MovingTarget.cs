using UnityEngine;

public class MovingTarget : MonoBehaviour
{
    [Header("移动设置")]
    public float speed = 2f;      // 移动速度
    public float distance = 3f;   // 往返移动的距离幅度

    private Vector3 startPos;     // 记录靶子初始的出生位置

    void Start()
    {
        // 游戏开始时，记录靶子的初始位置
        startPos = transform.position;
    }

    void Update()
    {
        // 使用 Mathf.Sin (正弦函数) 产生平滑的来回往复运动
        // transform.right 代表物体自身的右方（对应你图里红色的X轴方向）
        float offset = Mathf.Sin(Time.time * speed) * distance;

        // 更新位置 = 初始位置 + 往右边偏移的距离
        transform.position = startPos + transform.right * offset;
    }
}