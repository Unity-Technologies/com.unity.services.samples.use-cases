using System;
using System.Threading.Tasks;
using UnityEngine;
using GemHunterUGS.Scripts.AreaUpgradables;
using GemHunterUGS.Scripts.Core;
using GemHunterUGS.Scripts.EditProfile;
using GemHunterUGS.Scripts.Login_and_AccountManagement;
using GemHunterUGS.Scripts.LootBoxWithCooldown;
using GemHunterUGS.Scripts.PlayerDataManagement;
using GemHunterUGS.Scripts.PlayerEconomyManagement;
using GemHunterUGS.Scripts.Utilities;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;

namespace GemHunterUGS.Scripts.PlayerHub
{
    /// <summary>
    /// Controls the main hub UI of the game, managing navigation between different menus
    /// and coordinating player data updates with the UI display
    /// </summary>
    public class HubUIController : MonoBehaviour
    {
        [SerializeField]
        private HubView m_HubView;
        
        // Menu specific controllers
        [SerializeField]
        private EditProfileUIController m_EditProfileUIController;
        [SerializeField]
        private LootBoxUIController m_LootBoxUIController;
        [SerializeField]
        private AccountManagementUIController m_AccountManagementUIController;
        [SerializeField]
        private AreaProgressMenuUIController m_AreaProgressUIController;
        
        private PlayerDataManager m_PlayerDataManager;
        private PlayerEconomyManager m_PlayerEconomyManager;
        private NetworkConnectivityHandler m_NetworkConnectivityHandler;
        private GameManagerUGS m_GameManagerUGS;

        private void OnEnable()
        {
            // In case these were disabled...
            m_EditProfileUIController.gameObject.SetActive(true);
            m_AccountManagementUIController.gameObject.SetActive(true);
            m_LootBoxUIController.gameObject.SetActive(true);
        }
        
        private void Start()
        {
            InitializeDependencies();

            m_PlayerDataManager.CloudDataInitialized += InitializeHub;
            
            if (m_PlayerDataManager.IsCloudDataInitialized)
            {
                InitializeHub();
            }
        }

        private void InitializeDependencies()
        {
            m_GameManagerUGS = GameSystemLocator.Get<GameManagerUGS>();
            m_PlayerDataManager = GameSystemLocator.Get<PlayerDataManager>();
            m_PlayerEconomyManager = GameSystemLocator.Get<PlayerEconomyManager>();
            m_NetworkConnectivityHandler = GameSystemLocator.Get<NetworkConnectivityHandler>();
        }
        
        private void InitializeHub()
        {
            m_HubView.Initialize(m_PlayerDataManager.PlayerDataLocal, m_PlayerEconomyManager.PlayerEconomyDataLocal);
            
            InitializeControllers();
            
            UpdateOnlineStatus(m_NetworkConnectivityHandler.IsOnline);
            SetupEventHandlers();
            ShowMainHub();
            
            // TODO UpdateOnlineStatus here
            
            if (m_PlayerDataManager.ProfileSprite != null)
            {
                HandleUpdateProfilePicture(m_PlayerDataManager.ProfileSprite);
            }
        }

        private void InitializeControllers()
        {
            m_AccountManagementUIController.Initialize();
            m_LootBoxUIController.Initialize(m_HubView);
            m_AreaProgressUIController.Initialize();
        }
        
        private void SetupEventHandlers()
        {
            // Network events
            m_NetworkConnectivityHandler.OnlineStatusChanged += UpdateOnlineStatus;
            
            // Player data events
            m_PlayerDataManager.ProfilePictureUpdated += HandleUpdateProfilePicture;
            m_PlayerEconomyManager.InfiniteHeartStatusUpdated += HandleActiveInfiniteHeartEffect;
            
            // Navigation events
            m_HubView.PlayLevelButton.clicked += StartGame;
            m_HubView.ProfileButton.clicked += HandleShowEditProfile;
            m_HubView.EditProfileButton.clicked += HandleShowEditProfile;
            
            // Bottom navigation buttons
            m_HubView.InfoNavButton.clicked += HandleToggleWelcomeInfo;
            m_HubView.CloseInfoPopupButton.clicked += HandleToggleWelcomeInfo;
            m_HubView.StoreNavButton.clicked += HandleToggleStoreMenu;
            m_HubView.FriendsNavButton.clicked += HandleToggleFriendsMenu;
            m_HubView.AccountNavButton.clicked += HandleToggleAccountManagementMenu;
            m_HubView.HomeNavButton.clicked += ShowMainHub;
            
            // Top-bar navigation to Welcome
            m_HubView.SpecialOfferButton.clicked += HandleToggleWelcomeInfo;
            
            //Popup
            m_HubView.PopUp_TimedButton.clicked += m_HubView.HidePopUp;
        }
        
        /// <summary>
        /// Shows the main hub UI and hides other menus
        /// </summary>
        public void ShowMainHub()
        {
            m_HubView.SetHubVisibility(true);
            m_HubView.SetDarkenOverlayVisibility(false);
            m_HubView.ShowHubParent();
            m_HubView.ShowTopBar();
            m_HubView.ShowBottomNavBar();
            m_HubView.HideAllMenus();
        }

        public void ShowPopUpTimed(string text)
        {
            m_HubView.ShowPopUpTimed(text);
        }

        public void HideMainHub()
        {
            m_HubView.SetDarkenOverlayVisibility(true);
            m_HubView.HideHubParent();
            m_HubView.HideTopBar();
        }
        
        public void HideBottomNavBar()
        {
            m_HubView.HideBottomNavBar();
        }
        
        private void HandleToggleStoreMenu()
        {
            m_HubView.ToggleStoreMenu();
        }

        private void HandleToggleWelcomeInfo()
        {
            m_HubView.TogglePopUpWelcomeInfo();
        }
        
        private void HandleToggleFriendsMenu()
        {
            m_HubView.ToggleFriendsMenu();
        }
        
        public void HandleToggleAccountManagementMenu()
        {
            m_HubView.ToggleAccountManagementMenu();
        }

        private void HandleUpdateProfilePicture(Sprite image)
        {
            m_HubView.UpdateProfilePicture(image);
        }
        
        private void HandleShowEditProfile()
        {
            m_HubView.SetDarkenOverlayVisibility(true);
            m_EditProfileUIController.OpenEditProfile();
            m_HubView.HideAllMenus();
            m_HubView.HideBottomNavBar();
            HideMainHub();
        }
        
        private void UpdateOnlineStatus(bool isOnline)
        {
            if (isOnline)
            {
                m_HubView.SetHubViewOnlineMode();
            }
            else
            {
                m_HubView.SetHubViewOfflineMode();
            }
        }
        
        private void StartGame()
        {
            StartGameplayAsync().ConfigureAwait(false)
                .GetAwaiter()
                .OnCompleted(() => Logger.LogDemo("Gameplay started"));
        }

        private async Task StartGameplayAsync()
        {
            try 
            {
                await m_GameManagerUGS.StartGameplay();
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to start gameplay: {e}");
            }
        }

        private void HandleActiveInfiniteHeartEffect(bool active)
        {
            m_HubView.ShowInfinityHeartLabel(active);
        }
        
        private void OnDisable()
        {
            UnsubscribeFromManagerEvents();
            UnsubscribeFromUIEvents();
        }

        private void UnsubscribeFromManagerEvents()
        {
            if (m_PlayerDataManager == null)
            {
                return;
            }
            
            // Network events
            m_NetworkConnectivityHandler.OnlineStatusChanged -= UpdateOnlineStatus;
            
            m_PlayerDataManager.CloudDataInitialized -= InitializeHub;
            m_PlayerDataManager.ProfilePictureUpdated -= HandleUpdateProfilePicture;
            m_PlayerEconomyManager.InfiniteHeartStatusUpdated -= HandleActiveInfiniteHeartEffect;
        }

        private void UnsubscribeFromUIEvents()
        {
            if (m_HubView == null || m_HubView.ProfileButton == null)
            {
                return;
            }
            
            // Navigation events
            m_HubView.PlayLevelButton.clicked -= StartGame;
            m_HubView.ProfileButton.clicked -= HandleShowEditProfile;
            m_HubView.EditProfileButton.clicked -= HandleShowEditProfile;
            m_HubView.SpecialOfferButton.clicked -= HandleToggleWelcomeInfo;
            
            // Bottom navigation
            m_HubView.StoreNavButton.clicked -= HandleToggleStoreMenu;
            m_HubView.InfoNavButton.clicked -= HandleToggleWelcomeInfo;
            m_HubView.CloseInfoPopupButton.clicked -= HandleToggleWelcomeInfo;
            m_HubView.HomeNavButton.clicked -= ShowMainHub;
            m_HubView.FriendsNavButton.clicked -= HandleToggleFriendsMenu;
            m_HubView.AccountNavButton.clicked -= HandleToggleAccountManagementMenu;
            
            //Popup
            m_HubView.PopUp_TimedButton.clicked -= m_HubView.HidePopUp;
        }
    }
}