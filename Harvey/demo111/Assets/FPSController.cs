using UnityEngine;

// 强制要求挂载此脚本的物体必须有 CharacterController 组件
[RequireComponent(typeof(CharacterController))]
public class FPSController : MonoBehaviour
{
    [Header("移动设置")]
    public float moveSpeed = 5f;        // 移动速度
    public float gravity = -9.81f;      // 重力

    [Header("视角设置")]
    public float mouseSensitivity = 200f; // 鼠标灵敏度
    public Transform playerCamera;        // 玩家的摄像机（眼睛）

    private CharacterController controller;
    private Vector3 velocity;             // 用于计算重力下落速度
    private float xRotation = 0f;         // 记录上下低头抬头的角度

    void Start()
    {
        controller = GetComponent<CharacterController>();

        // 锁定鼠标指针并隐藏，FPS游戏必备
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // 1. 处理鼠标旋转视角
        MouseLook();

        // 2. 处理键盘移动
        PlayerMovement();
    }

    void MouseLook()
    {
        // 获取鼠标移动的距离
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // 计算上下抬低头的角度（反转Y轴因为鼠标往上推对应视角往上看）
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f); // 限制抬头低头的角度，防止脖子扭断

        // 上下旋转摄像机
        if (playerCamera != null)
        {
            playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }

        // 左右旋转整个玩家身体
        transform.Rotate(Vector3.up * mouseX);
    }

    void PlayerMovement()
    {
        // 获取键盘 WASD 或 方向键的输入 (-1 到 1 之间)
        float x = Input.GetAxis("Horizontal"); // A/D 左右
        float z = Input.GetAxis("Vertical");   // W/S 前后

        // 根据角色当前的面朝方向计算移动方向
        Vector3 move = transform.right * x + transform.forward * z;

        // 执行移动
        controller.Move(move * moveSpeed * Time.deltaTime);

        // 处理重力，防止角色飘在空中
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // 如果在地上，给个微小的向下速度贴紧地面
        }
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}