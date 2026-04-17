using UnityEngine;
using UnityEngine.UIElements;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;
namespace GemHunterUGS.Scripts.AreaUpgradables
{
    public class AreaProgressMenuView : MonoBehaviour
    {
        [SerializeField]
        private UIDocument m_Document;
        private VisualElement m_Root;

        public Button CloseMenuButton { get; private set; }
        public bool IsInitialized { get; private set; }
        
        private VisualElement m_AreaProgressMenu;
        
        // Total Progress
        
        private Button m_TotalProgressButton;
        private Label m_TotalProgressLabel;
        private ProgressBar m_TotalProgressBar;

        // AreaItems
        
        private VisualElement m_ItemsContainer;
        private VisualElement[] m_AreaItemContainers;
        public Button[] ItemUpgradeButtons { get; private set; }
        private VisualElement[] m_StarGraphic;
        private VisualElement[] m_AreaItems;
        private VisualElement[] m_CostIcons;
        private Label[] m_AreaItemNameLabels;
        private VisualElement[] m_GreenChecks;
        private ProgressBar[] m_ItemProgressBars;

        private Color m_EnabledTint = Color.white;
        private Color m_DisabledTint = Color.black;
        
        public void Initialize()
        {
            m_Root = m_Document.rootVisualElement;
            m_AreaProgressMenu = m_Root.Q<VisualElement>("AreaProgressMenu");
            
            m_TotalProgressButton = m_AreaProgressMenu.Q<Button>("TotalProgressButton");
            m_TotalProgressLabel = m_AreaProgressMenu.Q<Label>("AreaTotalProgressLabel");
            m_TotalProgressBar = m_AreaProgressMenu.Q<ProgressBar>("AreaTotalProgressBar");
            
            m_ItemsContainer = m_AreaProgressMenu.Q<VisualElement>("AreaItemsListContainer");
            CloseMenuButton = m_AreaProgressMenu.Q<Button>("CloseAreaProgressButton");

            IsInitialized = true;
        }
        
        public void SetupListOfAreas(int numOfUpgradables)
        {
            m_AreaItemContainers = new VisualElement[numOfUpgradables];
            ItemUpgradeButtons = new Button[numOfUpgradables];
            m_AreaItems = new VisualElement[numOfUpgradables];
            m_CostIcons = new VisualElement[numOfUpgradables];
            m_AreaItemNameLabels = new Label[numOfUpgradables];
            m_GreenChecks = new VisualElement[numOfUpgradables];
            m_ItemProgressBars = new ProgressBar[numOfUpgradables];
            
            for (int i = 0; i < numOfUpgradables; i++)
            {
                Logger.LogVerbose($"Setup item {i} in list of areas");
                m_AreaItemContainers[i] = m_ItemsContainer.Q<VisualElement>($"AreaListItemContainer{i+1}");
                SetupAreaItem(m_AreaItemContainers[i], i);
            }
        }
        
        private void SetupAreaItem(VisualElement itemContainer, int index)
        {
            if (itemContainer == null)
            {
                Logger.LogError($"Item container is null for index {index}");
                return;
            }
            
            ItemUpgradeButtons[index] = itemContainer.Q<Button>("UpgradeItemButton");
            m_AreaItems[index] = itemContainer.Q<VisualElement>("AreaItem");
            m_AreaItemNameLabels[index] = itemContainer.Q<Label>("ItemName");
            m_GreenChecks[index] = itemContainer.Q<VisualElement>("GreenCheck");
            m_ItemProgressBars[index] = itemContainer.Q<ProgressBar>("ItemProgressBar");
            m_CostIcons[index] = itemContainer.Q<VisualElement>("CostIcon");
            
            // Validate that all required elements were found
            if (ItemUpgradeButtons[index] == null || m_AreaItems[index] == null || 
                m_AreaItemNameLabels[index] == null || m_GreenChecks[index] == null || 
                m_ItemProgressBars[index] == null || m_CostIcons[index] == null)
            {
                Logger.LogError($"Failed to find all required UI elements for area item at index {index}");
            }
        }
        
        public void ShowMenu()
        {
            m_AreaProgressMenu.style.display = DisplayStyle.Flex;
        }

        public void HideMenu()
        {
            m_AreaProgressMenu.style.display = DisplayStyle.None;
        }

        public void UpdateTotalUpgradeProgress(int currentProgress, int maxProgress)
        { 
            m_TotalProgressLabel.text = currentProgress.ToString() + "/" + maxProgress.ToString() + " Upgrades";
            m_TotalProgressBar.value = currentProgress;
            m_TotalProgressBar.highValue = maxProgress;
        }
        
        public void UpdateUpgradableAreaItem(int index, string itemName, int progress, int maxProgress, int upgradeCost, bool enableButton, Sprite upgradeSprite)
        {
            // Logger.Log($"Updating area {itemName} item at index {index}");
            if (m_AreaItemNameLabels == null) { Logger.LogError("m_AreaItemNameLabels array is null"); return; }
            if (m_AreaItems == null) { Logger.LogError("m_AreaItems array is null"); return; }
            
            m_AreaItemNameLabels[index].text = itemName;
            
            var areaItem = m_AreaItems[index];
            
            Color tintColor = enableButton ? m_EnabledTint : m_DisabledTint;
            areaItem.style.unityBackgroundImageTintColor = tintColor;

            var progressBar = m_ItemProgressBars[index];
            progressBar.value = progress;
            progressBar.highValue = maxProgress;
            progressBar.style.unityBackgroundImageTintColor = tintColor;
            progressBar.style.display = DisplayStyle.Flex;
            
            var button = ItemUpgradeButtons[index];
            button.style.display = DisplayStyle.Flex;
            button.style.unityBackgroundImageTintColor = tintColor;
            button.SetEnabled(enableButton);
            button.text = upgradeCost.ToString();
            
            var costIcon = m_CostIcons[index];
            costIcon.style.display = DisplayStyle.Flex;
            costIcon.style.unityBackgroundImageTintColor = tintColor;
            costIcon.style.backgroundImage = new StyleBackground(upgradeSprite);
            
            var greenCheck = m_GreenChecks[index];
            greenCheck.style.display = DisplayStyle.None;
        }
        
        public void UpdateReadyUnlockAreaItem(int index, string itemName, int progress, int maxProgress, int unlockCost, bool enableButton, Sprite unlockSprite)
        {
            Logger.LogVerbose($"Updating item at index {index}: {itemName} button is enabled: {enableButton}");
            m_AreaItemNameLabels[index].text = "Unlock " + itemName;
            
            Color tintColor = enableButton ? m_EnabledTint : m_DisabledTint;
            
            var areaItem = m_AreaItems[index];
            areaItem.style.unityBackgroundImageTintColor = Color.white;

            var progressBar = m_ItemProgressBars[index];
            progressBar.value = progress;
            progressBar.highValue = maxProgress;
            progressBar.style.display = DisplayStyle.None;
            progressBar.style.unityBackgroundImageTintColor = Color.black;
            
            var costIcon = m_CostIcons[index];
            costIcon.style.display = DisplayStyle.Flex;
            costIcon.style.unityBackgroundImageTintColor = Color.white;
            costIcon.style.backgroundImage = new StyleBackground(unlockSprite);
            
            var button = ItemUpgradeButtons[index];
            button.style.display = DisplayStyle.Flex;
            button.style.unityBackgroundImageTintColor = tintColor;
            button.SetEnabled(enableButton);
            button.text = unlockCost.ToString();
            
            var greenCheck = m_GreenChecks[index];
            greenCheck.style.display = DisplayStyle.None;
        }

        public void LockButton(int index)
        {
            var button = ItemUpgradeButtons[index];
            button.style.display = DisplayStyle.Flex;
            button.style.unityBackgroundImageTintColor = Color.black;
            button.SetEnabled(false);
        }

        public void UpdateMaxAreaItem(int index, string itemName, int maxProgress)
        {
            // Validate index is within bounds
            if (index < 0 || m_AreaItemNameLabels == null || index >= m_AreaItemNameLabels.Length)
            {
                Logger.LogWarning($"Attempted to update max area item at invalid index: {index}");
                return;
            }
            
            // Validate UI elements exist
            if (m_AreaItemNameLabels[index] == null || m_ItemProgressBars[index] == null || 
                ItemUpgradeButtons[index] == null || m_GreenChecks[index] == null)
            {
                Logger.LogWarning($"UI elements not initialized for area item at index: {index}");
                return;
            }
            
            m_AreaItemNameLabels[index].text = itemName;
            
            var progressBar = m_ItemProgressBars[index];
            progressBar.value = maxProgress;
            progressBar.highValue = maxProgress;
            progressBar.style.display = DisplayStyle.Flex;
            
            var button = ItemUpgradeButtons[index];
            button.SetEnabled(false);
            button.text = string.Empty;
            button.style.display = DisplayStyle.None;
            
            var greenCheck = m_GreenChecks[index];
            greenCheck.style.display = DisplayStyle.Flex;
        }
    }
}
