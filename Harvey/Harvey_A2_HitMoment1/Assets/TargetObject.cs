using UnityEngine;

public class TargetObject : MonoBehaviour
{
    public float maxHealth = 10f; // ���Ѫ��
    public float health;
    public int pointValue = 5;    // �򱬼Ӷ��ٷ�
    public float reviveTime = 3f; // ����󸴻��������������ģ�

    void Start()
    {
        health = maxHealth; // һ��ʼѪ������
    }

    public void TakeDamage(float amount)
    {
        health -= amount;

        if (health <= 0f)
        {
            Die();
        }
    }

    void Die()
    {
        if (GameManager2.instance != null)
        {
            GameManager2.instance.AddScore(pointValue);

            // �� GameManager �ڼ���󸴻��Լ�
            GameManager2.instance.ReviveTarget(gameObject, reviveTime);
        }

        // ������ Destroy �����ˣ����ǰ�����Ϊ���أ�
        gameObject.SetActive(false);
    }
}