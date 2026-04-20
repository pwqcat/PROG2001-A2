using System.Collections;
using UnityEngine;

public class SafePlatform : MonoBehaviour
{
    public Transform respawnPoint; // 平台上的复活点

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            GameManager.Instance.UpdateRespawnPoint(respawnPoint);
            GetComponent<Collider>().enabled = false;
            Debug.Log("已更新复活点");
            transform.GetChild(0).gameObject.SetActive(false);
            StartCoroutine(Ondelay());
        }
    }
    private IEnumerator Ondelay()
    {
        yield return new WaitForSeconds(2);
        transform.GetChild(0).gameObject.SetActive(true);

    }
}
