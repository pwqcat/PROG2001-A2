using UnityEngine;

public class TargetObject : MonoBehaviour
{
    public float health = 30f; // 靶子的血量

    // 这个方法会被玩家的枪调用
    public void TakeDamage(float amount)
    {
        health -= amount;

        // 当血量小于等于0时，靶子被打碎（消失）
        if (health <= 0f)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log(gameObject.name + " 被打爆了！");
        // 销毁这个靶子
        Destroy(gameObject);

        // 如果你有爆炸粒子特效，可以在这里生成
    }
}