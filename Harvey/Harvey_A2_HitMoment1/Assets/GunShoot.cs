using UnityEngine;

public class GunShoot : MonoBehaviour
{
    [Header("枪械设置")]
    public float damage = 100f;
    public float range = 100f;

    [Header("引用")]
    public Camera fpsCam;
    public AudioSource shootSound;
    public ParticleSystem muzzleFlash;

    [Header("视觉后坐力")]
    public Transform gunModel;          // 你的枪模型
    public float recoilKickback = 0.15f;// 开枪时向后退的距离
    public float recoilAngle = 5f;      // 开枪时枪口抬起的角度
    public float returnSpeed = 10f;     // 枪身恢复原位的速度

    // 用来记录枪原本的位置和角度
    private Vector3 originalLocalPos;
    private Quaternion originalLocalRot;

    void Start()
    {
        // 游戏开始时，记录枪的默认位置和角度
        if (gunModel != null)
        {
            originalLocalPos = gunModel.localPosition;
            originalLocalRot = gunModel.localRotation;
        }
    }

    void Update()
    {
        if (Input.GetButtonDown("Fire1"))
        {
            Shoot();
        }

        // 让弹回原来的位置
        if (gunModel != null)
        {
            gunModel.localPosition = Vector3.Lerp(gunModel.localPosition, originalLocalPos, Time.deltaTime * returnSpeed);
            gunModel.localRotation = Quaternion.Lerp(gunModel.localRotation, originalLocalRot, Time.deltaTime * returnSpeed);
        }
    }

    void Shoot()
    {
        if (shootSound != null) shootSound.Play();
        if (muzzleFlash != null) muzzleFlash.Play();

        // 瞬间向后退一点，并向上抬起一定角度
        if (gunModel != null)
        {
            gunModel.localPosition -= Vector3.forward * recoilKickback;
            gunModel.localRotation *= Quaternion.Euler(-recoilAngle, 0f, 0f);
        }

        RaycastHit hit;
        if (Physics.Raycast(fpsCam.transform.position, fpsCam.transform.forward, out hit, range))
        {
            TargetObject target = hit.collider.GetComponentInParent<TargetObject>();
            if (target != null)
            {
                target.TakeDamage(damage);
            }

            // 如果打中了开始按钮
            StartGameTarget startBtn = hit.collider.GetComponentInParent<StartGameTarget>();
            if (startBtn != null)
            {
                startBtn.HitByBullet();
            }
        }
    }
}