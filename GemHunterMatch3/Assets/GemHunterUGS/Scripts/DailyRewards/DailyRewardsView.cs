using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;
namespace GemHunterUGS.Scripts.DailyRewards
{
    public class DailyRewardsView : MonoBehaviour
    {
        [SerializeField]
        private UIDocument m_Document;

        private VisualElement m_Root;
        private VisualElement m_RewardsBar;
        private VisualElement m_DailyReward;
        
        public Button DailyRewardButton { get; private set; }
        public Label DailyRewardLabel { get; private set; }
        public Label DailyRewardCountdownLabel { get; private set; }
        
        public Button ClaimedRewardsButton { get; private set; }
        private VisualElement DailyClaimedRewardsMenu { get; set; }
        public Button CloseMenu { get; private set; }
        
        public VisualElement ClaimsListContainer { get; private set; }

        private VisualElement m_AcceptDailyRewardScreen { get; set; }
        public Button CloseAcceptScreenButton { get; private set; }
        private Label m_AcceptRewardAmountLabel { get; set; }
        public Button AcceptRewardsButton { get; private set; }
        
        // VFX
        
        [SerializeField]
        private GameObject m_SparklesVFX;
        [SerializeField]
        private ParticleSystem m_SparkleParticleSystem;
        [SerializeField]
        private UIDocument m_SparkleUIDocument;
        private VisualElement m_VFXElement;
        private VisualElement m_VFXPositioner { get; set; }
        private float m_DailyRewardsSparkleDelay = 1.2f;
        private float m_SparkleRadius = 5f;
        
        private void OnEnable()
        {
            m_Root = m_Document.rootVisualElement;
            
            m_RewardsBar = m_Root.Q<VisualElement>("RewardsBar");
            m_DailyReward = m_RewardsBar.Q<VisualElement>("DailyReward");
            DailyRewardButton = m_DailyReward.Q<Button>("DailyRewardButton");

            DailyRewardLabel = m_DailyReward.Q<Label>("DailyRewardLabel");
            DailyRewardCountdownLabel = m_DailyReward.Q<Label>("DailyRewardCountdown");
            
            ClaimedRewardsButton = m_Root.Q<Button>("ClaimedRewardsButton");
            DailyClaimedRewardsMenu = m_Root.Q("DailyClaimedRewardsMenu");

            CloseMenu = DailyClaimedRewardsMenu.Q<Button>("CloseMenu");
            
            ClaimsListContainer = m_Root.Q("ClaimListView");
            
            m_AcceptDailyRewardScreen = m_Root.Q("AcceptDailyRewardScreen");
            m_VFXPositioner = m_AcceptDailyRewardScreen.Q<VisualElement>("VFXPositioner");
            m_AcceptRewardAmountLabel = m_AcceptDailyRewardScreen.Q<Label>("AcceptRewardAmountLabel");
            AcceptRewardsButton = m_AcceptDailyRewardScreen.Q<Button>("AcceptRewardButton");
            CloseAcceptScreenButton = m_AcceptDailyRewardScreen.Q<Button>("CloseAcceptScreen");
            
            SetupVFXElements();
            
            m_AcceptDailyRewardScreen.style.display = DisplayStyle.None;
        }

        private void SetupVFXElements()
        {
            m_VFXElement = m_SparkleUIDocument.rootVisualElement.Q<VisualElement>("vfx-element");
        }
        
        public void SetClaimDailyRewardButton(bool isEnabled, string labelText, string countdownText)
        {
            DailyRewardButton.SetEnabled(isEnabled);
            DailyRewardLabel.text = labelText;
            DailyRewardCountdownLabel.text = countdownText;
        }

        public void LockClaimDailyRewardButton()
        {
            DailyRewardButton.SetEnabled(false);
        }
        
        public void ShowDailyRewardsMenu()
        {
            DailyClaimedRewardsMenu.style.display = DisplayStyle.Flex;
            
            DailyClaimedRewardsMenu.schedule.Execute(() =>
            {
                DailyClaimedRewardsMenu.style.display = DisplayStyle.Flex;
            }).StartingIn(16);
        }

        public void ShowAcceptDailyRewardScreen(string rewardAmountText)
        {
            m_AcceptDailyRewardScreen.style.display = DisplayStyle.Flex;
            m_AcceptRewardAmountLabel.text = rewardAmountText;
            m_AcceptDailyRewardScreen.AddToClassList("accept-reward-screen-entrance");
            
            StartCoroutine(PlaySparkleAfterDelay(m_DailyRewardsSparkleDelay));
        }

        private IEnumerator PlaySparkleAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            PlaySparkleVFX(m_VFXPositioner);
        }

        private void PlaySparkleVFX(VisualElement targetElement)
        {
            Vector2 elementCenter = targetElement.worldBound.center;
            
            float offsetX = elementCenter.x - (m_VFXElement.resolvedStyle.width / 2);
            float offsetY = elementCenter.y - (m_VFXElement.resolvedStyle.height / 2);
            
            m_VFXElement.style.left = offsetX;
            m_VFXElement.style.top = offsetY;
    
            ParticleSystem.ShapeModule particleShapeModule = m_SparkleParticleSystem.shape;
            particleShapeModule.radius = m_SparkleRadius;
            
            
            m_SparkleParticleSystem.Play();
        }

        public void HideAcceptDailyRewardScreen()
        {
            m_AcceptDailyRewardScreen.style.display = DisplayStyle.None;
            m_AcceptDailyRewardScreen.RemoveFromClassList("accept-reward-screen-entrance");
        }
        
        public void HideDailyRewardsMenu()
        {
            DailyClaimedRewardsMenu.style.display = DisplayStyle.None;
        }
    }
}
