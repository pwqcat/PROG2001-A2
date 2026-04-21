using UnityEngine;

public class RotateObstacle : MonoBehaviour
{
    public float swingAngle = 45f;    // 摆动总角度
    public float rotateSpeed = 2f;    // 摆动速度
    public float startDelayMin = 0f;
    public float startDelayMax = 2f;  // 初始随机延迟

    private float _timer;
    private bool _canRotate;

    void Start()
    {
        // 随机开始延迟
        float randomDelay = Random.Range(startDelayMin, startDelayMax);
        Invoke(nameof(StartRotate), randomDelay);
    }

    void StartRotate()
    {
        _canRotate = true;
    }

    void Update()
    {
        if (!_canRotate) return;

        _timer += Time.deltaTime * rotateSpeed;
        // 绕 Z 轴来回摆动
        float angle = Mathf.Sin(_timer) * swingAngle;
        transform.localEulerAngles = new Vector3(0, 0, angle);
    }
}