using UnityEngine;
using UnityEngine.UI;

public class ExitGame : MonoBehaviour
{


    void Awake()
    {
    }

    void Start()
    {
        GetComponent<Button>().onClick.AddListener(DoExit);
    }

    void DoExit()
    {

        // 退出游戏
        Application.Quit();

        // 编辑器里也能模拟退出
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}