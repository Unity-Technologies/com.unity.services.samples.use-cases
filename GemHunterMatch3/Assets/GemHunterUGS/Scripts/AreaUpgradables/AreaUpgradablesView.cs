using UnityEngine;
using UnityEngine.UIElements;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;

namespace GemHunterUGS.Scripts.AreaUpgradables
{
    /// <summary>
    /// Handles UI element references and basic display operations for the area upgrades system.
    /// Includes area items, progress indicators, pop-ups (like if player can't afford upgrade), and final area complete screen.
    /// </summary>
    public class AreaUpgradablesView : MonoBehaviour
    {
        [SerializeField]
        private UIDocument m_Document;
        [SerializeField]
        private Camera m_MainCamera;
        private VisualElement m_Root;
        
        // Area Elements
        private ProgressBar[] m_UpgradableProgressBars;
        private VisualElement[] m_UpgradableStarGraphic;
        private ProgressBar m_CurrentAreaProgressBar;
        private Label m_CurrentAreaLabel;
        private VisualElement m_AreaProgressMenu;
        
        public Button[] UpgradableButtons { get; private set; }
        [SerializeField]
        private Vector2[] m_UpgradableButtonOffsets;
        public Button CurrentAreaProgressButton { get; private set; }
        
        // Pop-up Elements
        private VisualElement m_PopUp_Timed;
        public Button PopUp_TimedButton { get; private set; }
        private VisualElement m_AreaCompletePopup;
        
        // Area Complete Elements
        public Button CloseAreaCompleteButton { get; private set; }
        public Button AcceptRewardsButton { get; private set; }
        private VisualElement m_Reward1;
        private VisualElement m_Reward2;
        private VisualElement m_Reward3;
        private Label m_RewardAmountLabel1;
        private Label m_RewardAmountLabel2;
        private Label m_RewardAmountLabel3;

        public void Initialize()
        {
            if (m_MainCamera == null)
            {
                m_MainCamera = Camera.main;
            }
            
            m_Root = m_Document.rootVisualElement;
            
            CurrentAreaProgressButton = m_Root.Q<Button>("CurrentAreaProgressButton");
            m_CurrentAreaProgressBar = CurrentAreaProgressButton.Q<ProgressBar>("CurrentAreaProgressBar");
            m_CurrentAreaLabel = CurrentAreaProgressButton.Q<Label>("CurrentAreaLabel");
            
            m_AreaProgressMenu = m_Root.Q<VisualElement>("AreaProgressMenu");
            m_AreaProgressMenu.style.display = DisplayStyle.None;
            
            SetupPopUps();
        }

        public void SetupUpgradableElements(int numOfUpgradables)
        {
            Logger.LogVerbose($"Setting up {numOfUpgradables} upgradable elements");
            UpgradableButtons = new Button[numOfUpgradables];
            m_UpgradableProgressBars = new ProgressBar[numOfUpgradables];
            m_UpgradableStarGraphic = new VisualElement[numOfUpgradables];
            
            for (int i = 0; i < numOfUpgradables; i++)
            {
                UpgradableButtons[i] = (Button)m_Root.Q<VisualElement>($"Upgradable{i+1}Button").ElementAt(0);
                m_UpgradableProgressBars[i] = UpgradableButtons[i].Q<ProgressBar>($"ProgressBar");
                m_UpgradableStarGraphic[i] = UpgradableButtons[i].Q<VisualElement>($"StarGraphic");
            }
        }
        
        private void SetupPopUps()
        {
            // Timed (for not enough currency)
            
            m_PopUp_Timed = m_Root.Q<VisualElement>("PopUp_Timed");
            PopUp_TimedButton = m_PopUp_Timed.Q<Button>("PopUpButton");

            // Area Complete
            
            m_AreaCompletePopup = m_Root.Q<VisualElement>("AreaCompleteScreen");
            AcceptRewardsButton = m_AreaCompletePopup.Q<Button>("AcceptRewardsButton");
            CloseAreaCompleteButton = m_AreaCompletePopup.Q<Button>("CloseAreaCompleteButton");
            
            m_Reward1 = m_AreaCompletePopup.Q<VisualElement>("Reward1");
            m_Reward2 = m_AreaCompletePopup.Q<VisualElement>("Reward2");
            m_Reward3 = m_AreaCompletePopup.Q<VisualElement>("Reward3");
            
            m_RewardAmountLabel1 = m_AreaCompletePopup.Q<Label>("RewardAmountLabel1");
            m_RewardAmountLabel2 = m_AreaCompletePopup.Q<Label>("RewardAmountLabel2");
            m_RewardAmountLabel3 = m_AreaCompletePopup.Q<Label>("RewardAmountLabel3");
        }
        
        public void ShowAreaProgressMenu()
        {
            m_AreaProgressMenu.style.display = DisplayStyle.Flex;
        }

        public void HideAreaProgressMenu()
        {
            m_AreaProgressMenu.style.display = DisplayStyle.None;
        }

        public void ShowAreaComplete()
        {
            m_AreaCompletePopup.style.display = DisplayStyle.Flex;
            
            AcceptRewardsButton.SetEnabled(false);
            
            m_Reward1.style.display = DisplayStyle.None;
            m_Reward2.style.display = DisplayStyle.None;
            m_Reward3.style.display = DisplayStyle.None;
        }

        public void ShowAreaCompleteRewards(Texture2D reward1, Texture2D reward2, Texture2D reward3, 
            string rewardText1, string rewardText2, string rewardText3)
        {
            
            AcceptRewardsButton.SetEnabled(true);
            m_Reward1.style.backgroundImage = reward1;
            m_Reward2.style.backgroundImage = reward2;
            m_Reward3.style.backgroundImage = reward3;
            
            m_Reward1.style.display = DisplayStyle.Flex;
            m_Reward2.style.display = DisplayStyle.Flex;
            m_Reward3.style.display = DisplayStyle.Flex;
            
            m_RewardAmountLabel1.text = rewardText1;
            m_RewardAmountLabel2.text = rewardText2;
            m_RewardAmountLabel3.text = rewardText3;
        }

        public void HideAreaItems()
        {
        }

        public void ShowAreaItems()
        {
        }

        public void HideAreaComplete()
        {
            m_AreaCompletePopup.style.display = DisplayStyle.None;
        }
        
        public void ShowPopUpTimed(string text)
        {
            m_PopUp_Timed.style.display = DisplayStyle.Flex;
            PopUp_TimedButton.text = text;
        }

        public void HidePopUpTimed()
        {
            PopUp_TimedButton.text = string.Empty;
            m_PopUp_Timed.style.display = DisplayStyle.None;
        }

        public void UpdateProgressBar(int index, float currentValue, float maxValue, bool isUnlocked)
        {
            var progressBar = m_UpgradableProgressBars[index];
            progressBar.highValue = maxValue;
            progressBar.value = currentValue;
            progressBar.style.display = isUnlocked ? DisplayStyle.Flex : DisplayStyle.None;

             if (!isUnlocked){
                UpgradableButtons[index].parent.AddToClassList("upgradable-button-locked-templatecontainer");
                UpgradableButtons[index].parent.RemoveFromClassList("upgradable-button-templatecontainer");
            } else {
                UpgradableButtons[index].parent.AddToClassList("upgradable-button-templatecontainer");
                UpgradableButtons[index].parent.RemoveFromClassList("upgradable-button-locked-templatecontainer");
            }
            
            m_UpgradableStarGraphic[index].style.display = isUnlocked ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public void UpdateUpgradableButton(int index, bool isButtonEnabled, string text)
        {
            Logger.LogVerbose($"Updating button {index} with text: {text}");
            
            var button = UpgradableButtons[index];
            button.SetEnabled(isButtonEnabled);
            button.text = text;

            ApplyButtonVisualState(index, isButtonEnabled);
        }
        
        private void ApplyButtonVisualState(int index, bool isEnabled)
        {
            var button = UpgradableButtons[index];
            var parent = button.parent;
    
            if (parent != null)
            {
                // Tint based on enabled state
                parent.style.unityBackgroundImageTintColor = isEnabled 
                    ? Color.white 
                    : new Color(0.6f, 0.6f, 0.6f, .8f); // Gray when disabled
            }
        }
        

        public void UpdateUpgradableButtonPosition(int index, Vector3 worldPosition)
        {
            var button = UpgradableButtons[index];
            
            Vector2 screenPos = m_MainCamera.WorldToScreenPoint(worldPosition);
            var panelPos= RuntimePanelUtils.ScreenToPanel(m_Root.panel,new Vector2(screenPos.x, screenPos.y)); //flip the Y axis
            
            button.parent.style.left = panelPos.x + m_UpgradableButtonOffsets[index].x;
            button.parent.style.bottom = panelPos.y + m_UpgradableButtonOffsets[index].y;
        }
        
        public void SetButtonProcessingState(int index, bool isProcessing)
        {
            var button = UpgradableButtons[index];
            var parent = button.parent;
    
            if (isProcessing)
            {
                button.SetEnabled(false);
                if (parent != null)
                {
                    // Different visual for processing (e.g., yellow tint)
                    parent.style.unityBackgroundImageTintColor = new Color(.5f, .5f, 0.5f, .8f);
                }
            }
            else
            {
                // Reset to normal state - this will trigger the normal enabled/disabled visual
                if (parent != null)
                {
                    parent.style.unityBackgroundImageTintColor = Color.white;
                }
                // Note: Don't set enabled here - let the normal state update handle it
            }
        }
        
        public void UpdateCurrentAreaProgress(float currentValue, float maxValue, string areaName)
        {
            m_CurrentAreaProgressBar.highValue = maxValue;
            m_CurrentAreaProgressBar.value = currentValue;
            m_CurrentAreaLabel.text = areaName;
        }
    }
}
