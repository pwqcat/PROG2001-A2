using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float distance = 6f;
    public float height = 2.5f;
    public float smoothSpeed = 0.15f;

    // 必须是 LateUpdate，场景绝对不抖！
    void LateUpdate()
    {
        if (!target) return;

        Vector3 desiredPos = target.position - target.forward * distance + Vector3.up * height;
        transform.position = Vector3.Lerp(transform.position, desiredPos, smoothSpeed);
        transform.LookAt(target);
    }
}