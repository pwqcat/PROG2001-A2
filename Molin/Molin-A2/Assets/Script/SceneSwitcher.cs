using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneSwitcher : MonoBehaviour
{
    // 这个方法接收一个整数参数，供 UI 按钮调用
    public void LoadSceneByIndex(int sceneIndex)
    {
        // 加载对应编号的场景
        SceneManager.LoadScene(sceneIndex);
        Debug.Log("已切换到场景编号: " + sceneIndex);
    }
}