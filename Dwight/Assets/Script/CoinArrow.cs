using UnityEngine;

public class CoinArrow : MonoBehaviour
{
    public Transform player;

    void Update()
    {
        GameObject[] coins = GameObject.FindGameObjectsWithTag("Coin");

        if (coins == null || coins.Length == 0)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        // 找最近的金币
        GameObject nearest = null;
        float minDist = Mathf.Infinity;

        foreach (GameObject coin in coins)
        {
            float dist = Vector3.Distance(player.position, coin.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = coin;
            }
        }

        // 指向金币
        Vector3 dir = nearest.transform.position - player.position;
        dir.y = 0;

        // 让平面箭头正确指向
        Quaternion lookRot = Quaternion.LookRotation(dir);
        transform.rotation = lookRot * Quaternion.Euler(-90, 180, 0);
    }
}