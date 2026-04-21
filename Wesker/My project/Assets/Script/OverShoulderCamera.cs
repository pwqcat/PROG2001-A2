using UnityEngine;

public class OverShoulderCamera : MonoBehaviour
{
    public Transform player; // 绑定角色
    public float mouseSensitivity = 2f;
    public float distance = 2.5f; // 相机距离角色
    public float height = 1.2f; // 相机高度

    private float xRotation = 0f;
    private float yRotation = 0f;

    void LateUpdate()
    {
        if (!GameManager.Instance.isPaused) // 设置打开时禁止旋转
        {
            // 鼠标控制视角
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            yRotation += mouseX;
            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -15f, 30f); // 限制上下视角

            // 相机旋转与位置计算
            transform.rotation = Quaternion.Euler(xRotation, yRotation, 0);
            transform.position = player.position - transform.forward * distance + Vector3.up * height;
        }
    }
}