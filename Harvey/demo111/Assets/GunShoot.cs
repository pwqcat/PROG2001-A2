using UnityEngine;

public class GunShoot : MonoBehaviour
{
    [Header("枪械设置")]
    public float damage = 10f;       // 每次射击的伤害
    public float range = 100f;       // 射程

    [Header("引用")]
    public Camera fpsCam;            // 玩家的摄像机

    void Update()
    {
        // 当按下鼠标左键 (Fire1) 时开火
        if (Input.GetButtonDown("Fire1"))
        {
            Shoot();
        }
    }

    void Shoot()
    {
        // 播放开火音效、枪口火焰动画等（以后可以加在这里）
        Debug.Log("砰！开火！");

        RaycastHit hit;
        // 从摄像机的位置，向摄像机的正前方发射一条射线
        if (Physics.Raycast(fpsCam.transform.position, fpsCam.transform.forward, out hit, range))
        {
            // 如果打中了东西，打印出打中了什么
            Debug.Log("打中了: " + hit.transform.name);

            /// GetComponentInParent 可以保证无论子弹打中靶子的哪个部位（即使是子物体），都能找到靶子主体的扣血脚本
            TargetObject target = hit.collider.GetComponentInParent<TargetObject>();
            if (target != null)
            {
                target.TakeDamage(damage);
            }
        }
    }
}