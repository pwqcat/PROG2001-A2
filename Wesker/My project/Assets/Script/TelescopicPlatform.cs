using UnityEngine;

public class TelescopicPlatform : MonoBehaviour
{
    private float moveDistance = 3.5f;    // 左右移动距离
    public float moveSpeed = 2f;       // 移动速度
    public float minDelay = 0f;        // 最小随机延迟
    public float maxDelay = 2f;        // 最大随机延迟

    private Vector3 startPos;
    private Vector3 targetPos;
    private bool isExtending = false;
    private float waitTime;
    private float timer;

    void Start()
    {
        startPos = transform.position;
        targetPos = startPos + transform.right * moveDistance; // 左右移动

        // 一开始随机延迟
        timer = Random.Range(minDelay, maxDelay);
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (isExtending)
        {
            // 平滑移动到伸出位置
            transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);

            if (transform.position == targetPos)
            {
                isExtending = false;
                timer = 0;
            }
        }
        else
        {
            // 平滑缩回
            transform.position = Vector3.MoveTowards(transform.position, startPos, moveSpeed * Time.deltaTime);

            // 等待随机时间后再伸出
            if (transform.position == startPos && timer >= waitTime)
            {
                isExtending = true;
                timer = 0;
                waitTime = Random.Range(minDelay, maxDelay);
            }
        }
    }
}