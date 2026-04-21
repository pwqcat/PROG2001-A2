using UnityEngine;

public class CameraUXHandler : MonoBehaviour
{
    void Start()
    {
        // 锁定并隐藏光标，这是赛车游戏沉浸感的来源
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // 按下 Esc 键可以恢复光标（方便调试）
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}