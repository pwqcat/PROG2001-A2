using UnityEngine;

public class PlayerStartSound : MonoBehaviour
{
    private AudioSource audioSource;
    private bool hasPlayedStartSound = false;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        // 按 W / 上箭头 就播放启动音效
        if (!hasPlayedStartSound && Input.GetAxis("Vertical") > 0.1f)
        {
            audioSource.Play();
            hasPlayedStartSound = true; // 只播一次
        }
    }
}