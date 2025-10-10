using System;
using UnityEngine;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using GemHunterUGS.Scripts.AreaUpgradables;
using GemHunterUGS.Scripts.LoadingScreen;
using GemHunterUGS.Scripts.Login_and_AccountManagement;
using GemHunterUGS.Scripts.PlayerDataManagement;
using GemHunterUGS.Scripts.PlayerEconomyManagement;
using GemHunterUGS.Scripts.Utilities;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;

namespace GemHunterUGS.Scripts.Core
{
    /// <summary>
    /// Core initialization class responsible for bootstrapping the game's systems and services.
    /// Handles Unity Gaming Services setup, core game systems initialization, and service registration.
    /// This class follows a modular architecture pattern where each system is initialized independently
    /// and registered with a central locator for global access.
    /// 
    /// Key responsibilities:
    /// - Unity Gaming Services initialization
    /// - Core game systems creation and setup
    /// - Service registration via GameSystemLocator
    /// - Development utilities (account deletion)
    /// </summary>
    public class GameInitializer : MonoBehaviour
    {
        [Tooltip("For testing")]
        [SerializeField]
        private bool m_DeleteAccountOnStart = false;
        
        [SerializeField] private RandomProfilePicturesSO m_RandomProfilePicturesSO;
        [SerializeField] private GameManagerUGS m_GameManagerUGSPrefab;
        
        const string k_Environment = "production";
        
        private async void Awake()
        {
            try
            {
                await InitializeUnityServices(OnServicesInitialized);
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }
        }

        private void Start()
        {
            if (m_DeleteAccountOnStart)
            {
                DeleteAccount();
            }
            
            InitializeCoreGameSystems();
        }
        
        private async Task InitializeUnityServices(Action onSuccess)
        {
            try
            {
                var options = new InitializationOptions().SetEnvironmentName(k_Environment);
                await UnityServices.InitializeAsync(options).ContinueWith(task => onSuccess());
            }
            catch (Exception e)
            {
                Logger.LogWarning($"Error initializing Unity Services: {e.Message}");
                Logger.Log("Proceeding with offline mode...");
            }
        }
        
        private void OnServicesInitialized()
        {
            Logger.LogDemo("✅Congratulations!\nUnity Gaming Services has been successfully initialized.");
        }
        
        /// <summary>
        /// Initializes all core game systems and establishes their dependencies:
        /// 1. Scene/UI systems for loading and transitions
        /// 2. Network/Authentication for online services
        /// 3. Player systems (data, economy, areas) with local and cloud components
        ///
        /// The architecture uses a Client/Manager pattern where:
        /// - Managers handle local game state and logic
        /// - "-Client" classes interface with Cloud Code via CloudBindingsProvider
        /// - Systems are registered with GameSystemLocator for global access
        /// </summary>
        private void InitializeCoreGameSystems()
        {
            // Scene Management
            var gameManager = Instantiate(m_GameManagerUGSPrefab);
            DontDestroyOnLoad(gameManager.gameObject);
            var loadingScreenController = gameManager.GetComponent<LoadingScreenUIController>();
            var sceneLoader = new SceneLoader(loadingScreenController);
            
            // Network & Authentication
            var networkConnectivityHandler = gameManager.gameObject.GetComponent<NetworkConnectivityHandler>();
            var authenticationManager = new PlayerAuthenticationManager();
            
            // Cloud Bindings
            var bindingsProvider = new CloudBindingsProvider();
            
            // Player Data
            var localStorageSystem = new LocalStorageSystem();
            var dataManager = new PlayerDataManager(gameManager, localStorageSystem, m_RandomProfilePicturesSO);
            var dataManagerClient = new PlayerDataManagerClient(gameManager, authenticationManager, bindingsProvider, networkConnectivityHandler);
            
            // Economy
            var economyManager = new PlayerEconomyManager(localStorageSystem);
            var economyManagerClient = new PlayerEconomyManagerClient(dataManagerClient, bindingsProvider);
            
            // Areas
            var areaManager = new AreaManager(dataManager, economyManager);
            var areaManagerClient = new AreaManagerClient(dataManagerClient, bindingsProvider);
            var commandBatchSystem = new CommandBatchSystem(dataManager,areaManager, bindingsProvider, localStorageSystem);
            
            // Platform initiation
            var facebookManager = GetComponent<FacebookManager>();
            
            RegisterCoreGameSystems
            (
                authenticationManager,
                bindingsProvider,
                gameManager,
                dataManager,
                dataManagerClient,
                economyManager,
                economyManagerClient,
                areaManager,
                areaManagerClient,
                commandBatchSystem,
                networkConnectivityHandler,
                facebookManager
            );
            
            dataManager.Initialize(dataManagerClient, economyManager);
            economyManager.Initialize(economyManagerClient);
            gameManager.Initialize(authenticationManager, sceneLoader);
        }
        
        /// <summary>
        /// Registers all core game systems with the GameSystemLocator.
        /// This provides centralized access to these systems throughout the game.
        /// </summary>
        private void RegisterCoreGameSystems
        (
            PlayerAuthenticationManager authenticationManager,
            CloudBindingsProvider bindingsProvider,
            GameManagerUGS gameManagerUGS, 
            PlayerDataManager playerDataManager,
            PlayerDataManagerClient playerDataManagerClient,
            PlayerEconomyManager economyManager,
            PlayerEconomyManagerClient economyClient,
            AreaManager areaManager,
            AreaManagerClient areaManagerClient,
            CommandBatchSystem commandBatchSystem,
            NetworkConnectivityHandler networkConnectivityHandler,
            FacebookManager facebookManager
        )
        {
            GameSystemLocator.Register<PlayerAuthenticationManager>(authenticationManager);
            GameSystemLocator.Register<CloudBindingsProvider>(bindingsProvider);
            GameSystemLocator.Register<GameManagerUGS>(gameManagerUGS);
            GameSystemLocator.Register<PlayerDataManager>(playerDataManager);
            GameSystemLocator.Register<PlayerDataManagerClient>(playerDataManagerClient);
            GameSystemLocator.Register<PlayerEconomyManager>(economyManager);
            GameSystemLocator.Register<PlayerEconomyManagerClient>(economyClient);
            GameSystemLocator.Register<AreaManager>(areaManager);
            GameSystemLocator.Register<AreaManagerClient>(areaManagerClient);
            GameSystemLocator.Register<CommandBatchSystem>(commandBatchSystem);
            GameSystemLocator.Register<NetworkConnectivityHandler>(networkConnectivityHandler);
            GameSystemLocator.Register<FacebookManager>(facebookManager);
        }
        
        /// <summary>
        /// Completely removes the current account and associated local data for testing purposes (e.g. testing new player flow)
        /// Note: CloudSave data will still exist and must be manually deleted via Dashboard.
        /// </summary>
        private async void DeleteAccount()
        {
            try
            {
                Logger.LogWarning("GameInitializer set to DeleteAccountOnStart--turn off or remove in builds. This setting automatically flips off on disable.");
                if (AuthenticationService.Instance.SessionTokenExists)
                {
                    Logger.Log("Clearing token...");
                    AuthenticationService.Instance.ClearSessionToken();
                }
                if (AuthenticationService.Instance.IsSignedIn)
                {
                    Logger.Log("Deleting account...");
                    await AuthenticationService.Instance.DeleteAccountAsync();
                }
                Logger.Log("Deleting local data...");
                var localDataHandler = new LocalStorageSystem();
                localDataHandler.DeleteLocalData();
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }
        }

        private void OnDisable()
        {
            if (m_DeleteAccountOnStart)
            {
                // It is easy to forget that "delete account" is toggled on.
                Logger.LogWarning("🟡 REMINDER: Toggle 'Delete Account On Start' FALSE to keep player.");
            }
        }
    }
}