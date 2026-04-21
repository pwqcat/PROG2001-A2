using UnityEngine;

public class SpeedVFXController : MonoBehaviour
{
    [Header("视觉目标")]
    [Tooltip("小车尾部用来发光的小部件的 GameObject")]
    public Renderer tailPartRenderer;
    [Tooltip("如果该部件有多个材质，指定发光材质的序号")]
    public int materialIndex = 0;

    [Header("无极光效配置 (HDR)")]
    [Tooltip("静止或极低速时的底色（模拟暗红色玻璃）")]
    [ColorUsage(true, true)]
    public Color idleColor = new Color(0.6f, 0.05f, 0f, 1f);

    [Tooltip("极速时的爆发色（模拟太阳般的超高亮暖黄色）")]
    [ColorUsage(true, true)]
    public Color maxSpeedColor = new Color(3f, 1.5f, 0.2f, 1f);

    [Header("动态张力")]
    [Tooltip("颜色变化的平滑延迟，数值越大变化越平缓")]
    public float transitionSpeed = 10f;
    [Tooltip("发光曲线指数：1为线性变亮；1.5-2会让高光集中在后半段爆发")]
    public float glowCurvePower = 1.5f;

    private Rigidbody rb;
    private Material glowMaterial;
    private float maxSpeed = 70f;
    private Color currentColor;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // 自动读取载具的绝对最大速度
        PlayerController pc = GetComponent<PlayerController>();
        if (pc != null)
        {
            maxSpeed = pc.maxSpeed;
        }
        else
        {
            EnemyAIController ai = GetComponent<EnemyAIController>();
            if (ai != null) maxSpeed = ai.maxSpeed;
        }

        // 实例化材质并激活自发光通道
        if (tailPartRenderer != null)
        {
            glowMaterial = tailPartRenderer.materials[materialIndex];
            glowMaterial.EnableKeyword("_EMISSION");
            currentColor = idleColor;
        }
    }

    void Update()
    {
        if (glowMaterial == null || rb == null) return;

        // 获取平面真实速度
        Vector3 flatVelocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        float currentSpeed = flatVelocity.magnitude;

        // 1. 计算无极比例：严格限制在 0.0 到 1.0 之间
        float speedRatio = Mathf.Clamp01(currentSpeed / maxSpeed);

        // 2. 引入曲线张力：让亮度不是匀速增加，而是越接近极速，爆发感越强
        float visualRatio = Mathf.Pow(speedRatio, glowCurvePower);

        // 3. 计算目标颜色：在暗红和高亮暖黄之间进行无极混合
        Color targetColor = Color.Lerp(idleColor, maxSpeedColor, visualRatio);

        // 4. 平滑过渡赋值
        currentColor = Color.Lerp(currentColor, targetColor, Time.deltaTime * transitionSpeed);
        glowMaterial.SetColor("_EmissionColor", currentColor);
    }
}