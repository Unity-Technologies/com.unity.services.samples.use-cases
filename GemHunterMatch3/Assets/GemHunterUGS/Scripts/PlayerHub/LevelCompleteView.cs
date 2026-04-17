using UnityEngine;
using UnityEngine.UIElements;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;
namespace GemHunterUGS.Scripts.PlayerHub
{
    public class LevelCompleteView : MonoBehaviour
    {
        [SerializeField]
        private UIDocument m_Document;
        private VisualElement m_Root;
        private VisualElement m_LevelCompleteScreen;
        public Button CloseAcceptScreenButton { get; private set; }
        public Button AcceptRewardButton { get; private set; }
        
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        public void Initialize()
        {
            if (m_Document == null)
            {
                Logger.LogError("No Document assigned to LevelCompleteView");
            }
            
            m_Root = m_Document.rootVisualElement;
            m_LevelCompleteScreen = m_Root.Q<VisualElement>("LevelCompleteScreen");
            
            CloseAcceptScreenButton = m_LevelCompleteScreen.Q<Button>("CloseAcceptScreenButton");
            AcceptRewardButton = m_LevelCompleteScreen.Q<Button>("AcceptRewardButton");
        }

        public void ShowLevelCompleteScreen(bool showScreen)
        {
            m_LevelCompleteScreen.style.display = showScreen ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
