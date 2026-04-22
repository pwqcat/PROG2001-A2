using UnityEngine;

public class StartGameTarget : MonoBehaviour
{
    // �����ӵ�����ʱ����
    public void HitByBullet()
    {
        if (GameManager2.instance != null)
        {
            GameManager2.instance.StartGame(); // ֪ͨ�ܹܣ���Ϸ��ʼ��
        }

        // ������Ϸ���Լ�����ʧ
        gameObject.SetActive(false);
    }
}
