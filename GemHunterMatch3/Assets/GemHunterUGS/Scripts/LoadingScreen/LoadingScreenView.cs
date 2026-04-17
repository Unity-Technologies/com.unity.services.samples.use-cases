using UnityEngine;
using UnityEngine.UIElements;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;

namespace GemHunterUGS.Scripts.LoadingScreen
{
    public class LoadingScreenView : MonoBehaviour
    {
        [SerializeField]
        private UIDocument m_LoadingScreenDocument;
        private VisualElement m_Root;
        private ProgressBar m_LoadingProgressBar;
        
        
        private void Awake()
        {
            if (m_LoadingScreenDocument == null)
            {
                Logger.LogError("UIDocument component not found on LoadingScreenManager object!");
                return;
            }
            
            m_Root = m_LoadingScreenDocument.rootVisualElement;
            m_LoadingProgressBar = m_Root.Q<ProgressBar>("LoadingProgressBar");
        }
        
        public void ShowLoadingScreen()
        {
            if (m_Root != null)
            {
                m_Root.style.display = DisplayStyle.Flex;
            }
        }
        
        public void HideLoadingScreen()
        {
            if (m_Root != null)
            {
                m_Root.style.display = DisplayStyle.None;
            }
        }

        public void UpdateProgressBar(float progress)
        {
            m_LoadingProgressBar.value = progress * 100f;
        }
    }
}
