using System.Collections;
using System.Collections.Generic;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;
using GemHunterUGS.Scripts.PlayerEconomyManagement;
using GemHunterUGS.Scripts.Utilities;
using Unity.Services.CloudCode.GeneratedBindings.GemHunterUGSCloud.Models;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;

namespace GemHunterUGS.Scripts.PlayerHub
{
    /// <summary>
    /// Core view class for the player hub, handling all main menu UI elements including:
    /// - Navigation and menus
    /// - Player stats, profile pic, and currency display
    /// - Rewards and loot box interface
    /// 
    /// Note: Loot box UI is integrated here since it is very simple
    /// Other menus have their own view.
    /// </summary>
    public class HubView : MonoBehaviour
    {
        [SerializeField]
        private Sprite m_PlaceholderProfileImage;
        private UIDocument m_Document;
        private VisualElement m_Root;
        private VisualElement m_DarkenOverlay;
        private VisualElement m_HubParentContainer;
        
        public Button PlayLevelButton { get; private set; }
        
        // Top bar
        private VisualElement m_TopBarContainer { get; set; }
        private VisualElement m_ProfileImage;
        private Label m_CoinsLabel;
        private Label m_HeartLabel;
        private Label m_StarLabel;
        private Label m_InfinityHeartLabel;
        
        public Button ProfileButton { get; private set; }
        public Button EditProfileButton { get; private set; }
        
        // Shop and rewards
        private VisualElement m_RewardsBar;
        private Label m_LootBoxLabel;
        private Label m_LootBoxCountdownLabel;
        private VisualElement m_LootBoxIcon;
        
        public Button SpecialOfferButton { get; private set; }
        public Button ClaimLootBoxButton { get; private set; }
        public Button AdRewardButton { get; private set; }
        
        // Bottom Nav 
        public Button StoreNavButton { get; private set; }
        public Button InfoNavButton { get; private set; }
        public Button CloseInfoPopupButton { get; private set; }
        public Button HomeNavButton { get; private set; }
        public Button FriendsNavButton { get; private set; }
        public Button AccountNavButton { get; private set; }
        
        // Bottom Nav Menus
        private VisualElement m_BottomNavigationContainer;
        public VisualElement StoreMenu { get; private set; }
        public VisualElement PopUpWelcomeMenu { get; private set; }
        public VisualElement FriendsMenu { get; private set; }
        public VisualElement AccountMenu { get; private set; }
        
        // Pop-up
        private VisualElement m_PopUp_Timed;
        public Button PopUp_TimedButton { get; private set; }
        
        // Rewards
        // public VisualElement LootBoxRewardsPopup { get; private set; }
        // public VisualElement RewardsErrorPopup { get; private set; }
        
        // VFX
        [SerializeField]
        private GameObject m_SparklesVFX;
        [SerializeField]
        private Camera m_MainCamera;
        [SerializeField] 
        private Camera m_VFXCamera;
        [SerializeField] 
        float  m_VFXDepth = 0.5f;
        [SerializeField]
        private ParticleSystem m_SparkleParticleSystem;
        [SerializeField]
        private UIDocument m_SparkleUIDocument;
        private VisualElement m_VFXElement;
        private VisualElement m_VFXPositioner { get; set; }
        private readonly float m_LootBoxSparkleDelay = 0.2f;
        private readonly float m_SparkleRadius = 0.5f;

        public void Initialize(PlayerData playerData, PlayerEconomyData playerEconomy)
        {
            SetupMainElements();
            SetupPopUpElements();
            SetupRewards();
            SetupBottomNavigation();
            SetupBottomMenus();
            SetupTopBarBindings(playerData, playerEconomy);
            SetupVFX();
        }
        
        private void SetupMainElements()
        {
            m_Document = GetComponent<UIDocument>();
            m_Root = m_Document.rootVisualElement;
            m_DarkenOverlay = m_Root.Q<VisualElement>("DarkenOverlay");
            m_HubParentContainer = m_Root.Q<VisualElement>("HubParent");
            
            m_TopBarContainer = m_Root.Q<VisualElement>("TopBarContainer");
            
            ProfileButton = m_TopBarContainer.Q<Button>("ProfileButton");
            EditProfileButton = m_TopBarContainer.Q<Button>("EditProfileButton");
            m_ProfileImage = ProfileButton.Q<VisualElement>("ProfileImage");
            
            PlayLevelButton = m_HubParentContainer.Q<Button>("PlayLevelButton");
            
            m_CoinsLabel = m_TopBarContainer.Q<Label>("CoinsLabel");
            m_HeartLabel = m_TopBarContainer.Q<Label>("HeartLabel");
            m_InfinityHeartLabel = m_TopBarContainer.Q<Label>("InfinityHeartLabel");
            m_StarLabel = m_TopBarContainer.Q<Label>("StarLabel");
        }
        
        private void SetupPopUpElements()
        {
            m_PopUp_Timed = m_Root.Q<VisualElement>("PopUp_Timed");
            PopUp_TimedButton = m_PopUp_Timed.Q<Button>("PopUpButton");
        }
        
        private void SetupRewards()
        {
            m_RewardsBar = m_HubParentContainer.Q<VisualElement>("RewardsBar");
            SpecialOfferButton = m_RewardsBar.Q<Button>("SpecialOfferButton");
            ClaimLootBoxButton = m_RewardsBar.Q<Button>("ClaimLootBoxButton");
            AdRewardButton = m_RewardsBar.Q<Button>("AdRewardButton");
            
            m_LootBoxCountdownLabel = m_RewardsBar.Q<Label>("LootBoxCountdown");
            m_LootBoxLabel = m_RewardsBar.Q<Label>("LootBoxLabel");
            m_LootBoxIcon = m_RewardsBar.Q<VisualElement>("LootBoxIcon");
        }

        private void SetupBottomNavigation()
        {
            m_BottomNavigationContainer = m_Root.Q<VisualElement>("BottomNavigationContainer");
            StoreNavButton = m_BottomNavigationContainer.Q<Button>("StoreNavButton");
            InfoNavButton = m_BottomNavigationContainer.Q<Button>("InfoNavButton");
            HomeNavButton = m_BottomNavigationContainer.Q<Button>("HomeNavButton");
            FriendsNavButton = m_BottomNavigationContainer.Q<Button>("FriendsNavButton");
            AccountNavButton = m_BottomNavigationContainer.Q<Button>("AccountNavButton");
        }

        private void SetupBottomMenus()
        {
            StoreMenu = m_Root.Q("StoreMenu");
            PopUpWelcomeMenu = m_Root.Q("PopUp_Welcome");
            CloseInfoPopupButton = PopUpWelcomeMenu.Q<Button>("ClosePopUpButton");
            FriendsMenu = m_Root.Q("FriendsMenu");
            AccountMenu = m_Root.Q("AccountManagementMenu");
        }
        
        private void SetupTopBarBindings(PlayerData playerData, PlayerEconomyData economyData)
        {
            if (!ValidateBindingData(playerData, economyData)) return;
            
            Logger.LogVerbose($"Setting up top bar bindings with hearts: {playerData.Hearts} and coins {economyData.Currencies["COIN"]}");
            
            SetupCurrencyBindings(economyData);
            SetupPlayerDataBindings(playerData);
        }
        
        private void SetupVFX()
        {
            if (m_SparkleUIDocument != null)
            {
                m_VFXElement = m_SparkleUIDocument.rootVisualElement.Q<VisualElement>("vfx-element");
            }
            else
            {
                Logger.LogWarning("SparkleUIDocument is not assigned!");
            }
        }
        
        private bool ValidateBindingData(PlayerData playerData, PlayerEconomyData economyData)
        {
            if (playerData == null || economyData?.Currencies == null)
            {
                Logger.LogWarning("Data not ready for bindings");
                return false;
            }
            return true;
        }

        private void SetupCurrencyBindings(PlayerEconomyData economyData)
        {
            var coinBinding = new DataBinding()
            {
                dataSource = economyData,
                dataSourcePath = new PropertyPath("Currencies"),
                bindingMode = BindingMode.ToTarget
            };
            
            // Currency is stored in a Dictionary<string, int>, we need to extract a specific key
            coinBinding.sourceToUiConverters.AddConverter((ref Dictionary<string,int> currencies) => 
                currencies[PlayerEconomyManager.k_Coin].ToString());
            
            m_CoinsLabel.SetBinding("text", coinBinding);
        }

        private void SetupPlayerDataBindings(PlayerData playerData)
        {
            // Hearts and Stars are direct int properties
            m_HeartLabel.dataSource = playerData;
            m_HeartLabel.SetBinding("text", new DataBinding()
            {
                dataSourcePath = new PropertyPath("Hearts")
            });

            m_StarLabel.dataSource = playerData;
            m_StarLabel.SetBinding("text", new DataBinding()
            {
                dataSourcePath = new PropertyPath("Stars")
            });
        }

        public void UpdateProfilePicture(Sprite profilePicture)
        {
            if (profilePicture == null)
            {
                Logger.LogWarning("No profile picture provided to HubView");
                m_ProfileImage.style.backgroundImage = new StyleBackground(m_PlaceholderProfileImage);
                return;
            }
            
            Logger.LogVerbose("Profile picture updated in view");
            
            m_ProfileImage.style.backgroundImage = new StyleBackground(profilePicture);
        }

        public void SetPlaceholderProfilePicture()
        {
            UpdateProfilePicture(m_PlaceholderProfileImage);
        }
        
        public void SetDarkenOverlayVisibility(bool isVisible)
        {
            m_DarkenOverlay.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetHubVisibility(bool isVisible)
        {
            m_Root.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetHubViewOnlineMode()
        {
            AccountNavButton.SetEnabled(true);
            FriendsNavButton.SetEnabled(true);
            StoreNavButton.SetEnabled(true);
        }
        
        public void SetHubViewOfflineMode()
        {
            AccountNavButton.SetEnabled(false);
            FriendsNavButton.SetEnabled(false);
            StoreNavButton.SetEnabled(false);
        }
        
        public void ToggleStoreMenu()
        {
            bool isMenuVisible = StoreMenu.style.display == DisplayStyle.Flex;
            if (isMenuVisible)
            {
                HideMenu(StoreMenu);
                ShowHubParent();
                ShowTopBar();
                SetDarkenOverlayVisibility(false);
                SetNavButtonSize(StoreNavButton, false);
            }
            else
            {
                HideAllMenus();
                ShowMenu(StoreMenu);
                HideHubParent();
                HideTopBar();
                ResetAllNavButtonSizes();
                SetNavButtonSize(StoreNavButton, true);
            }
        }
        
        public void TogglePopUpWelcomeInfo()
        {
            bool isMenuVisible = PopUpWelcomeMenu.style.display == DisplayStyle.Flex;

            if (isMenuVisible)
            {
                HideMenu(PopUpWelcomeMenu);
                ShowHubParent();
                ShowTopBar();
                SetDarkenOverlayVisibility(false);
                SetNavButtonSize(InfoNavButton, false);
            }
            else
            {
                HideAllMenus();
                HideHubParent();
                ShowMenu(PopUpWelcomeMenu);
                ResetAllNavButtonSizes();
                SetDarkenOverlayVisibility(true);
                SetNavButtonSize(InfoNavButton, true);
            }
        }

        public void ToggleFriendsMenu()
        {
            bool isMenuVisible = FriendsMenu.style.display == DisplayStyle.Flex;
            if (isMenuVisible)
            {
                HideMenu(FriendsMenu);
                SetDarkenOverlayVisibility(false);
                ShowHubParent();
                ShowTopBar();
                SetNavButtonSize(FriendsNavButton, false);
            }
            else
            {
                HideAllMenus();
                ShowMenu(FriendsMenu);
                SetDarkenOverlayVisibility(true);
                HideHubParent();
                HideTopBar();
                ResetAllNavButtonSizes();
                SetNavButtonSize(FriendsNavButton, true);
            }
        }
        
        public void ToggleAccountManagementMenu()
        {
            bool isMenuVisible = AccountMenu.style.display == DisplayStyle.Flex;
            if (isMenuVisible)
            {
                HideMenu(AccountMenu);
                ShowHubParent();
                ShowTopBar();
                SetDarkenOverlayVisibility(false);
                SetNavButtonSize(AccountNavButton, false);
            }
            else
            {
                HideAllMenus();
                ShowMenu(AccountMenu);
                HideHubParent();
                ShowTopBar();
                ResetAllNavButtonSizes();
                SetDarkenOverlayVisibility(true);
                SetNavButtonSize(AccountNavButton, true);
            }
        }
        
        private void SetNavButtonSize(Button button, bool isActive)
        {
            if (isActive)
            {
                button.AddToClassList("bottom-nav-button-open");
            }
            else
            {
                button.RemoveFromClassList("bottom-nav-button-open");
            }
        }

        private void ResetAllNavButtonSizes()
        {
            SetNavButtonSize(StoreNavButton, false);
            SetNavButtonSize(InfoNavButton, false);
            SetNavButtonSize(HomeNavButton, false);
            SetNavButtonSize(FriendsNavButton, false);
            SetNavButtonSize(AccountNavButton, false);
        }

        public void ShowHubParent()
        {
            m_HubParentContainer.style.display = DisplayStyle.Flex;
            ResetAllNavButtonSizes();
            SetNavButtonSize(HomeNavButton, true);
        }

        public void HideHubParent()
        {
            m_HubParentContainer.style.display = DisplayStyle.None;
        }

        public void HideTopBar()
        {
            m_TopBarContainer.style.display = DisplayStyle.None;
        }

        public void HideBottomNavBar()
        {
            m_BottomNavigationContainer.style.display = DisplayStyle.None;
        }

        public void ShowBottomNavBar()
        {
            m_BottomNavigationContainer.style.display = DisplayStyle.Flex;
        }

        public void ShowTopBar()
        {
            m_TopBarContainer.style.display = DisplayStyle.Flex;
        }

        private void ShowMenu(VisualElement menu)
        {
            menu.style.display = DisplayStyle.Flex;
        }

        private void HideMenu(VisualElement menu)
        {
            menu.style.display = DisplayStyle.None;
        }

        public void HideAllMenus()
        {
            StoreMenu.style.display = DisplayStyle.None;
            PopUpWelcomeMenu.style.display = DisplayStyle.None;
            FriendsMenu.style.display = DisplayStyle.None;
            AccountMenu.style.display = DisplayStyle.None;
        }
        
        // Popups

        public void ShowLootboxRewardsPopup()
        {
            // The timed lootbox is a simple implementation, no full-screen reward splash is required
        }

        public void ShowPopUpTimed(string text, float time = 2f)
        {
            m_PopUp_Timed.style.display = DisplayStyle.Flex;
            PopUp_TimedButton.text = text;
            StartCoroutine(HidePopUpAfterWait(time));
        }
        
        private IEnumerator HidePopUpAfterWait(float time = 2f)
        {
            yield return new WaitForSeconds(time);
            HidePopUp();
        }

        public void HidePopUp()
        {
            m_PopUp_Timed.style.display = DisplayStyle.None;
            PopUp_TimedButton.text = string.Empty;
        }

        public void ShowErrorPopup(string errorMessage)
        {
            ShowPopUpTimed(errorMessage);
        }
        
        public void SetLootBoxState(bool canClaim)
        {
            ClaimLootBoxButton.SetEnabled(canClaim);
            
            var disabledColor = new Color(0.5f, 0.5f, 0.5f, 1f); // Gray tint when disabled
            
            // Apply visual feedback - darken when inactive
            ClaimLootBoxButton.style.unityBackgroundImageTintColor = canClaim
                ? Color.white
                : disabledColor;
            
            m_LootBoxIcon.style.opacity = canClaim ? 1f : 0.7f;
            ClaimLootBoxButton.style.opacity = canClaim ? 1f : 0.7f;
        }
        
        public void SetLootBoxLabelText(string labelText)
        {
            m_LootBoxLabel.text = labelText;
        }
        
        public void SetCountdownVisibility(bool visible)
        {
            m_LootBoxCountdownLabel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
        
        public void SetLootBoxCountdownText(string countdownText)
        {
            m_LootBoxCountdownLabel.text = countdownText;
        }

        public void ShowInfinityHeartLabel(bool visible)
        {
            m_InfinityHeartLabel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
        
        // VFX

        public void PlayLootBoxClaimedVFX()
        {
            StartCoroutine(PlaySparkleAfterDelay(m_LootBoxSparkleDelay, m_CoinsLabel));
        }
        
        private IEnumerator PlaySparkleAfterDelay(float delay, VisualElement targetElement)
        {
            yield return new WaitForSeconds(delay);
            PlaySparkleVFX(targetElement);
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
    }
}
