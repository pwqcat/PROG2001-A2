using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float jumpForce = 7f;
    private Rigidbody rb;
    private bool isGrounded;

    // 动画系统
    public Animator anim;
    private int hashSpeed;
    private int hashIsGrounded;
    private int hashJump;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // --- 关键：物理完全冻结旋转，被撞也不会转 ---
        rb.freezeRotation = true;

        // 禁止动画带动角色
        anim.applyRootMotion = false;

        // 动画参数
        hashSpeed = Animator.StringToHash("Speed");
        hashIsGrounded = Animator.StringToHash("IsGrounded");
        hashJump = Animator.StringToHash("Jump");

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        // 强制禁止动画控制角色位置和旋转
        anim.applyRootMotion = false;

        // 冻结刚体旋转，物理也不能让角色乱转
        rb.freezeRotation = true;

    }

    void Update()
    {
        if (GameManager.Instance != null &&
            (GameManager.Instance.isPaused || GameManager.Instance.isGameOver))
            return;

        // 移动输入
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector3 moveDir = Camera.main.transform.right * horizontal + Camera.main.transform.forward * vertical;
        moveDir.y = 0;
        moveDir.Normalize();

        transform.Translate(moveDir * moveSpeed * Time.deltaTime, Space.World);

        // 跳跃
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.velocity = Vector3.up * jumpForce;
            anim.SetTrigger(hashJump);
        }

        // 动画
        float moveMagnitude = new Vector3(horizontal, 0, vertical).magnitude;
        anim.SetFloat(hashSpeed, moveMagnitude);
        anim.SetBool(hashIsGrounded, isGrounded);
    }

    void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
            isGrounded = true;
    }

    void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
            isGrounded = false;
    }
}