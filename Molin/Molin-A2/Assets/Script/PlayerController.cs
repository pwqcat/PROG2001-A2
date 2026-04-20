using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Engine Power (双段式赛车油门)")]
    [Tooltip("爆发阶段的极高加速度（推背感）")]
    public float fastAcceleration = 3500f;
    [Tooltip("爆发阶段结束的速度阈值。例如设为 40，则 0-40 提速极快")]
    public float fastAccelThreshold = 40f;
    [Tooltip("尾速阶段的初始加速度。达到阈值后，加速度会突降到此值，并慢慢衰减")]
    public float topEndAcceleration = 1200f;
    [Tooltip("车辆的绝对最高速度")]
    public float maxSpeed = 70f;

    [Header("Steering & Handling")]
    public float maxTurnSpeed = 150f;
    public float speedForMaxTurn = 10f;

    [Header("Custom Physics (街机轮胎物理)")]
    public float lateralGrip = 5f;
    public float coastingDrag = 2f;
    public float downForce = 50f;

    [Header("Ground Detection")]
    public float groundCheckDistance = 1.0f;

    private Rigidbody rb;
    private float verticalInput;
    private float horizontalInput;
    private bool isGrounded;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0, -0.5f, 0);
    }

    void Update()
    {
        verticalInput = Input.GetAxis("Vertical");
        horizontalInput = Input.GetAxis("Horizontal");
    }

    void FixedUpdate()
    {
        Vector3 rayStart = transform.position + Vector3.up * 0.1f;
        isGrounded = Physics.Raycast(rayStart, Vector3.down, groundCheckDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        if (!isGrounded || rb.velocity.y > 0.1f)
        {
            rb.AddForce(Vector3.down * downForce * rb.mass, ForceMode.Force);
        }

        Vector3 lateralVelocity = transform.right * Vector3.Dot(rb.velocity, transform.right);
        rb.AddForce(-lateralVelocity * lateralGrip * rb.mass, ForceMode.Force);

        if (Mathf.Abs(verticalInput) < 0.05f)
        {
            Vector3 forwardVelocity = transform.forward * Vector3.Dot(rb.velocity, transform.forward);
            rb.AddForce(-forwardVelocity * coastingDrag * rb.mass, ForceMode.Force);
        }

        if (!isGrounded) return;

        // --- 双段式平滑油门核心逻辑 ---
        float currentForwardSpeed = Vector3.Dot(rb.velocity, transform.forward);
        float absSpeed = Mathf.Abs(currentForwardSpeed);

        if (Mathf.Abs(verticalInput) > 0.05f)
        {
            float currentAccel = 0f;

            // 第一阶段：爆发期
            if (absSpeed < fastAccelThreshold)
            {
                currentAccel = fastAcceleration;
            }
            // 第二阶段：尾速期（从 topEndAcceleration 平滑衰减到 0）
            else if (absSpeed < maxSpeed)
            {
                float speedPastThreshold = absSpeed - fastAccelThreshold;
                float highSpeedRange = maxSpeed - fastAccelThreshold;
                float decayFactor = 1f - (speedPastThreshold / highSpeedRange);

                currentAccel = topEndAcceleration * decayFactor;
            }

            if (verticalInput < 0 && currentForwardSpeed > -maxSpeed * 0.5f)
            {
                rb.AddForce(transform.forward * verticalInput * currentAccel * rb.mass, ForceMode.Force);
            }
            else if (verticalInput > 0 && currentForwardSpeed < maxSpeed)
            {
                rb.AddForce(transform.forward * verticalInput * currentAccel * rb.mass, ForceMode.Force);
            }
        }

        float currentSpeed = rb.velocity.magnitude;
        float turnFactor = Mathf.Clamp01(currentSpeed / speedForMaxTurn);
        float turnMultiplier = Mathf.Max(turnFactor, Mathf.Abs(verticalInput) * 0.2f);

        if (Mathf.Abs(horizontalInput) > 0.05f && turnMultiplier > 0.01f)
        {
            float turn = horizontalInput * maxTurnSpeed * turnMultiplier * Time.fixedDeltaTime;
            if (verticalInput < -0.05f && currentForwardSpeed < 0.5f) turn = -turn;

            Quaternion turnRotation = Quaternion.Euler(0f, turn, 0f);
            rb.MoveRotation(rb.rotation * turnRotation);
        }
    }
}