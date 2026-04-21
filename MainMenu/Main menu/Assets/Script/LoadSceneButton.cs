
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

 public class LoadSceneButton : MonoBehaviour
    {
        public string SceneName = "";

        private void Start()
        {
            GetComponent<Button>().onClick.AddListener(LoadTargetScene);
        }
        public void LoadTargetScene()
        {
            SceneManager.LoadScene(SceneName);
        }
    }
