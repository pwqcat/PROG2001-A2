using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FPSController : MonoBehaviour
{
    [Header("移动设置")]
    public float moveSpeed = 5f;
    public float gravity = -9.81f;

    [Header("视角设置")]
    public float mouseSensitivity = 200f;
    public Transform playerCamera;

    private CharacterController controller;
    private Vector3 velocity;
    private float xRotation = 0f;

    // 【新增】用来屏蔽启动瞬间的错误鼠标数据的计时器
    private float mouseEnableTimer = 0.5f;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 强行平视前方
        xRotation = 0f;
        if (playerCamera != null)
        {
            playerCamera.localRotation = Quaternion.Euler(0f, 0f, 0f);
        }
    }

    void Update()
    {
        // 1. 处理键盘移动 (走动不受影响)
        PlayerMovement();

        // 2. 处理鼠标旋转视角 
        // 如果游戏刚运行还在0.1秒内，就不执行鼠标旋转代码
        if (mouseEnableTimer > 0)
        {
            mouseEnableTimer -= Time.deltaTime; // 倒计时
        }
        else
        {
            MouseLook(); // 0.5秒后，鼠标数据稳定了，才允许转动视角
        }
    }

    void MouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        if (playerCamera != null)
        {
            playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }

        transform.Rotate(Vector3.up * mouseX);
    }

    void PlayerMovement()
    {
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = transform.right * x + transform.forward * z;

        controller.Move(move * moveSpeed * Time.deltaTime);

        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}