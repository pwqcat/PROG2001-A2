using UnityEngine;
using UnityEngine.Events; // 引入事件系统，让我们能像普通 UI 按钮一样连线

public class ClickableObject : MonoBehaviour
{
    [Header("当这个3D物体被点击时触发什么：")]
    public UnityEvent onClick;

    // Unity 内置魔法函数：当鼠标左键点击这个物体的碰撞体时自动执行
    void OnMouseDown()
    {
        // 唤醒并执行下面面板里连线的所有事件
        onClick.Invoke();
    }
}