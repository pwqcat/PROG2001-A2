using UnityEngine;

public class MoveObstacle : MonoBehaviour
{
    public float rotateSpeed = 40f; // 旋转速度

    private Vector3 startPos;
    private int rotateDir; // 旋转方向 ±1

    void Start()
    {
        startPos = transform.position;
        // 随机顺时针或逆时针旋转
        rotateDir = Random.value > 0.5f ? 1 : -1;
    }

    void Update()
    {
        // 绕 Y 轴随机方向匀速旋转
        transform.Rotate(0, rotateDir * rotateSpeed * Time.deltaTime, 0);
    }
}