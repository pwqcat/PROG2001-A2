using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; 

public class MainMenu : MonoBehaviour
{
    public AudioSource backgroundMusic; 
    public Slider volumeSlider;         

    private void Start()
    {
        // 初始化音量为Slider当前的值
        if (backgroundMusic != null)
        {
            backgroundMusic.volume = volumeSlider.value;
        }

        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }
    }

    public void MainMenu1()
    {
        SceneManager.LoadScene(0);
    }

    public void StartLevel1()
    {
        SceneManager.LoadScene(1);
    }

    private void OnVolumeChanged(float value)
    {
        if (backgroundMusic != null)
        {
            backgroundMusic.volume = value; 
        }
    }
}