using UnityEngine;

public class CoinRotate : MonoBehaviour
{
    [Header("旋转")]
    public float rotateSpeed = 180f;

    [Header("浮动")]
    public float floatHeight = 0.2f;
    public float floatSpeed = 1.5f;

    [Header("金币音效")]
    public AudioClip collectSound; 

    private AudioSource audioSource;
    private Vector3 startPos;
    private bool collected = false;

    void Start()
    {
        startPos = transform.position;
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    void Update()
    {
        if (collected) return;

        transform.Rotate(0, rotateSpeed * Time.deltaTime, 0);

        float newY = startPos.y + Mathf.Sin(Time.time * floatSpeed) * floatHeight;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected) return;
        if (!other.CompareTag("Player")) return;

        collected = true;
        
        // 1. 立刻禁用碰撞，防止再次触发
        GetComponent<Collider>().enabled = false;

        // 2. 播放加分（如果有管理器）
        GameManager.instance?.AddCoin();

        // 3. 播放音效
        if (collectSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(collectSound);
            // 关键：根据音效长度决定延迟销毁，给多1秒缓冲确保播完
            float destroyDelay = collectSound.length + 1f; 
            Destroy(gameObject, destroyDelay);
        }
        else
        {
            // 如果没有音效，直接销毁
            Destroy(gameObject);
        }
    }
}