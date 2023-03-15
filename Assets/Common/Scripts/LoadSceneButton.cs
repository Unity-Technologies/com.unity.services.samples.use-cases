using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.Services.Samples
{
    [RequireComponent(typeof(Button))]
    public class LoadSceneButton : MonoBehaviour
    {
        public string sceneName;
#if UNITY_EDITOR
        public Object readmeFile;
#endif

        void Awake()
        {
            var button = GetComponent<Button>();
            button.onClick.AddListener(OnButtonClick);
        }

        void OnDestroy()
        {
            var button = GetComponent<Button>();
            button.onClick.RemoveListener(OnButtonClick);
        }

        void OnButtonClick()
        {
            LoadScene();
            SelectReadmeFileOnProjectWindow();
        }

        protected void LoadScene()
        {
            if (!string.IsNullOrEmpty(sceneName))
            {
                SceneManager.LoadScene(sceneName);
            }
        }

        protected void SelectReadmeFileOnProjectWindow()
        {
#if UNITY_EDITOR
            if (!(readmeFile is null))
            {
                Selection.objects = new[] { readmeFile };
            }
#endif
        }
    }
}
