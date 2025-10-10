using UnityEngine;
using UnityEngine.UIElements;
namespace GemHunterUGS.Scripts.Ads
{
    public class AdRewardView : MonoBehaviour
    {
        [SerializeField]
        private UIDocument m_Document;
        private VisualElement m_Root;
        private VisualElement m_AdReward;
        public Button AdRewardButton { get; private set; }

        public void Initialize()
        {
            m_Root = m_Document.rootVisualElement;
            m_AdReward = m_Root.Q<VisualElement>("AdReward");
            AdRewardButton = m_Root.Q<Button>("AdRewardButton");
        }
    }
}
