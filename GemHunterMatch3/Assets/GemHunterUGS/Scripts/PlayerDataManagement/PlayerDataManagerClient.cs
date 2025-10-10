using System;
using System.Threading.Tasks;
using GemHunterUGS.Scripts.Core;
using GemHunterUGS.Scripts.Utilities;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.CloudCode.GeneratedBindings.GemHunterUGSCloud.Models;
using Task = System.Threading.Tasks.Task;

namespace GemHunterUGS.Scripts.PlayerDataManagement
{
    /// <summary>
    /// Manages player data synchronization and communication with Cloud Code services.
    /// Handles player initialization, profile management, and gameplay state updates.
    /// </summary>
    public class PlayerDataManagerClient : IDisposable
    {
        public bool IsPlayerInitializedInCloud { get; private set; }
        private bool m_IsInitializing = false;
        
        private readonly GameManagerUGS m_GameManagerUGS;
        private readonly NetworkConnectivityHandler m_NetworkHandler;
        private readonly CloudBindingsProvider m_BindingsProvider;
        private readonly PlayerAuthenticationManager m_AuthenticationManager;

        private PlayerData m_CloudPlayerData;
        private PlayerInitializationResponse m_LastInitResponse;
        
        private const int k_MaxGiftHearts = 3;
        private const int MaxInitializeRetries = 3;
        private const int RetryDelay = 1000;

        // Events
        public event Action<PlayerData, PlayerEconomyData> PlayerDataInitialized;
        public event Action<PlayerData> PlayerDataUpdated; 
        public event Action PlayerInitialized;
        public event Action<ProfilePicture> ProfilePictureFetched;
        
        public PlayerDataManagerClient(GameManagerUGS gameManagerUGS, PlayerAuthenticationManager authenticationManager, CloudBindingsProvider bindingsProvider, NetworkConnectivityHandler networkHandler)
        {
            m_GameManagerUGS = gameManagerUGS;
            m_AuthenticationManager = authenticationManager;
            m_NetworkHandler = networkHandler;
            m_BindingsProvider = bindingsProvider;
            
            m_GameManagerUGS.GameplayLevelWon += HandleLevelWon;
            m_GameManagerUGS.GameplayReplayLevelLost += HandleReplayLevelLost;
            m_AuthenticationManager.SignedIn += HandleSignInInitialization;
            m_NetworkHandler.OnlineStatusChanged += HandleConnectivityChanged;
            
            IsPlayerInitializedInCloud = false;
        }
        
        private async void HandleSignInInitialization()
        {
            if (m_IsInitializing)
            {
                Logger.LogDemo("🚫 Initialization already in progress, skipping duplicate call");
                return;
            }
            if (IsPlayerInitializedInCloud)
            {
                Logger.LogDemo("🚫 Player already initialized, skipping duplicate call");
                return;
            }
            
            m_IsInitializing = true;
            Logger.LogDemo("🟢Starting player initialization in CloudCode ☁...");
            
            try
            {
                await HandlePlayerSignIn();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Player initialization failed: {ex.Message}");
                // Could fire an event here if UI needs to handle initialization failures
            }
            finally
            {
                m_IsInitializing = false;
            }
        }
        
        private async Task HandlePlayerSignIn()
        {
            bool success = await InitializePlayerWithRetry();
            if (!success)
            {
                Logger.LogError("Failed to handle player sign in after multiple attempts.");
                return;
            }
            Logger.LogDemo("☁⚡ PlayerInitialized");
            
            // Fire events
            PlayerInitialized?.Invoke();
            PlayerDataInitialized?.Invoke(m_LastInitResponse.PlayerData, m_LastInitResponse.EconomyData);    
            PlayerDataUpdated?.Invoke(m_CloudPlayerData);
            
            if (m_LastInitResponse.ProfilePicture == null)
            {
                Logger.LogWarning("Profile picture is null!");
                return;
            }
            ProfilePictureFetched?.Invoke(m_LastInitResponse.ProfilePicture);
        }
        
        private async Task<bool> InitializePlayerWithRetry()
        {
            if (IsPlayerInitializedInCloud)
            {
                Logger.LogDemo("✅ Player already initialized in cloud, returning success");
                return true;
            }
            
            Exception lastException = null;
            
            for (int attempt = 0; attempt < MaxInitializeRetries; attempt++)
            {
                try
                {
                    Logger.LogDemo("Attempting to initialize in CloudCode...");
                    
                    var initResponse = await m_BindingsProvider.GemHunterBindings.OnSignInHandlePlayerInitialization();
                    
                    if (initResponse?.PlayerData == null || initResponse?.EconomyData == null)
                    {
                        return false;
                    }
                    
                    HandleInitializationSuccess(initResponse);
                    
                    return true;
                }
                catch (CloudCodeException ex)
                {
                    lastException = ex;
                    if (!ShouldRetryCloudCodeException(ex))
                        return false;
                        
                    HandleRetryException(ex, attempt);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    HandleRetryException(ex, attempt);
                }
            }
            return false;
        }

        private void HandleInitializationSuccess(PlayerInitializationResponse initResponse)
        {
            IsPlayerInitializedInCloud = true;
            // Store data first
            m_CloudPlayerData = initResponse.PlayerData;
            // Store the response
            m_LastInitResponse = initResponse;
            
            // Log success
            Logger.LogDemo($"☁⚡ Initialization success! PlayerData: " +
                $"Areas={m_CloudPlayerData.GameAreasData?.Count}, " +
                $"CurrentArea.Level={m_CloudPlayerData.CurrentArea?.AreaLevel}, " +
                $"UpgradableAreaItems={m_CloudPlayerData.CurrentArea?.UpgradableAreaItems?.Count}");
        }
        
        private bool ShouldRetryCloudCodeException(CloudCodeException ex)
        {
            // Don't retry authentication/authorization errors
            if (ex.Message.Contains("Authentication") || ex.Message.Contains("Unauthorized"))
            {
                Logger.LogError($"Non-retryable CloudCode error: {ex.Message}");
                return false;
            }
            return true;
        }
        
        private void HandleRetryException(Exception ex, int attempt)
        {
            string exceptionType = ex is CloudCodeException ? "CloudCode" : "General";
            Logger.LogDemo($"{exceptionType} error on attempt {attempt + 1}: {ex.Message}");
            
            // Wait before retrying (except on last attempt)
            if (attempt < MaxInitializeRetries - 1)
            {
                Task.Delay(RetryDelay).Wait();
            }
        }
        
        public void UpdateDisplayName(string displayName)
        {
            m_CloudPlayerData.DisplayName = displayName;
            PlayerDataUpdated?.Invoke(m_CloudPlayerData);
        }
        
        public void HandleCloudDataUpdate(PlayerData updatedPlayerData)
        {
            m_CloudPlayerData = updatedPlayerData;
            PlayerDataUpdated?.Invoke(m_CloudPlayerData);
        }
        
        public bool CanModifyGiftHearts(int amount)
        {
            if (m_CloudPlayerData.GiftHearts + amount < 0)
            {
                return false;
            }
            if (amount > k_MaxGiftHearts)
            {
                Logger.LogVerbose("Max gift hearts reached");
                return false;
            }
            return true;
        }

        private async void HandleLevelWon()
        {
            try
            {
                m_CloudPlayerData = await m_BindingsProvider.GemHunterBindings.HandleLevelWon();
                if (m_CloudPlayerData != null)
                {
                    Logger.LogDemo("⭐☁ Added star in cloud code");
                    PlayerDataUpdated?.Invoke(m_CloudPlayerData);
                }
                else
                {
                    Logger.LogWarning("HandleLevelWon returned null data");
                }
            }
            catch (CloudCodeException ex)
            {
                Logger.LogError($"CloudCode error handling level won: {ex.Message}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Unexpected error handling level won: {ex.Message}");
            }
        }
        
        private async void HandleReplayLevelLost()
        {
            try
            {
                var playerData = await m_BindingsProvider.GemHunterBindings.HandleLevelLost();

                if (playerData == null)
                {
                    Logger.LogWarning("HandleLevelLost returned null data");
                    return;
                }

                m_CloudPlayerData = playerData;
                
                if (m_CloudPlayerData.HasInfiniteHeartEffectActive)
                {
                    Logger.LogDemo("💖☁ Player has infinite hearts active");
                }

                if (m_CloudPlayerData.Hearts >= 0)
                {
                    Logger.LogDemo("💔☁ Deducted heart in cloud code");
                }
            
                PlayerDataUpdated?.Invoke(m_CloudPlayerData);
            }
            catch (CloudCodeException ex)
            {
                Logger.LogError($"CloudCode error handling level lost: {ex.Message}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Unexpected error handling level lost: {ex.Message}");
            }
        }
        
        private async void HandleConnectivityChanged(bool isOnline)
        {
            // Small delay to ensure scene is fully loaded
            await Task.Delay(100);
            if (!isOnline ||
                !IsPlayerInitializedInCloud ||
                !AuthenticationService.Instance.IsSignedIn ||
                m_BindingsProvider?.GemHunterBindings == null ||
                m_CloudPlayerData == null 
                )
            {
                Logger.LogDemo("Skipping ConnectivityChanged sync: " + 
                    $"Online: {isOnline}, " +
                    $"SignedIn: {AuthenticationService.Instance.IsSignedIn}, " +
                    $"Initialized: {m_CloudPlayerData != null}");
                return;
            }

            try
            {
                Logger.LogDemo("HandleConnectivityChanged about to call GetPlayerData...");
                
                var playerData = await m_BindingsProvider.GemHunterBindings.GetPlayerData();
                if (playerData != null)
                {
                    m_CloudPlayerData = playerData;
                    PlayerDataUpdated?.Invoke(m_CloudPlayerData);
                }
                else
                {
                    Logger.LogWarning("Received null player data from cloud during connectivity check");
                }
            }
            catch (CloudCodeException ex)
            {
                Logger.LogError($"CloudCode error during connectivity sync: {ex.Message}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Unexpected error during connectivity sync: {ex.Message}");
            }
        }
        
        private void ProcessOfflineQueue()
        {
            // Process any queued operations that couldn't be performed
        }
        
        public void Dispose()
        {
            m_GameManagerUGS.GameplayLevelWon -= HandleLevelWon;
            m_GameManagerUGS.GameplayReplayLevelLost -= HandleReplayLevelLost;
            m_AuthenticationManager.SignedIn -= HandleSignInInitialization;
            m_NetworkHandler.OnlineStatusChanged -= HandleConnectivityChanged;
            m_CloudPlayerData = null;
        }
    }
}
