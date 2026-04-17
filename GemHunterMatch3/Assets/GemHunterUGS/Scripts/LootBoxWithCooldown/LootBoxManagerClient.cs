using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.CloudCode.GeneratedBindings.GemHunterUGSCloud.Models;
using GemHunterUGS.Scripts.Core;
using GemHunterUGS.Scripts.PlayerEconomyManagement;
using GemHunterUGS.Scripts.Utilities;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;

namespace GemHunterUGS.Scripts.LootBoxWithCooldown
{
    /// <summary>
    /// Manages the loot box system's Cloud Code communication and local cooldown tracking.
    /// Provides a clear state machine for availability, cooldown management, and reward claiming.
    /// Handles both client-side countdown updates and periodic server synchronization.
    /// </summary>
    public class LootBoxManagerClient : MonoBehaviour
    {
        // Constants
        private const float k_ClientTickRate = 1f;
        private const float k_ServerCheckRate = 30f;
        
        // Dependencies
        private NetworkConnectivityHandler m_NetworkConnectivityHandler;
        private PlayerEconomyManagerClient m_PlayerEconomyManagerClient;
        private LootBoxUIController m_LootBoxUIController;
        private CloudBindingsProvider m_BindingsProvider;

        private bool m_NeedsCooldownRefresh = false;
        private long m_CooldownSecondsRemaining;
        private float m_ClientTickTimer;
        private float m_ServerSyncTimer;
        private bool m_IsInitialized;
        
        public bool IsClaiming() => m_IsClaiming;
        private bool m_IsClaiming;
        
        public bool CanClaimLootBox()
        {
            return m_IsInitialized 
                && m_NetworkConnectivityHandler.IsOnline 
                && m_CooldownSecondsRemaining <= 0 
                && !m_IsClaiming;
        }
    
        public bool IsOnCooldown() => m_CooldownSecondsRemaining > 0;
        public bool IsOffline() => !m_NetworkConnectivityHandler.IsOnline;
        public bool IsInitialized() => m_IsInitialized;
        public long GetCooldownRemaining() => m_CooldownSecondsRemaining;
        
        // Events for state changes
        public event Action CooldownChanged; // Fired when cooldown starts/ends
        public event Action<long> CooldownTick;
        public event Action<LootBoxResult> ClaimSucceeded;
        public event Action<string> ClaimFailed;
        
        private void Start()
        {
            InitializeDependencies();
            SetupEventSubscriptions();
        }

        private void InitializeDependencies()
        {
            m_NetworkConnectivityHandler = GameSystemLocator.Get<NetworkConnectivityHandler>();
            m_PlayerEconomyManagerClient = GameSystemLocator.Get<PlayerEconomyManagerClient>();
            m_BindingsProvider = GameSystemLocator.Get<CloudBindingsProvider>();
            m_LootBoxUIController = GetComponent<LootBoxUIController>();
        }

        private void SetupEventSubscriptions()
        {
            m_PlayerEconomyManagerClient.PlayerEconomyInitialized += InitializeLootBoxSystem;
            m_NetworkConnectivityHandler.OnlineStatusChanged += HandleNetworkStatusChange;
            m_LootBoxUIController.OnClickClaimLootBox += ProcessClaimRequest;

            if (m_PlayerEconomyManagerClient.IsInitialized)
            {
                Logger.LogDemo("Economy already initialized, starting loot box immediately");
                InitializeLootBoxSystem();
            }
        }
        
        private void Update()
        {
            if (!m_IsInitialized) return;

            if (m_NeedsCooldownRefresh)
            {
                m_NeedsCooldownRefresh = false;
                _ = RefreshCooldownFromServer();
            }
            
            UpdateClientTick();
            UpdateServerSync();
        }

        #region Event Handlers
        
        private async void InitializeLootBoxSystem()
        {
            m_IsInitialized = true;
            Logger.LogDemo("🎁 Loot box system initialized");
            CooldownChanged?.Invoke();
            await RefreshCooldownFromServer();
        }
        
        private void HandleNetworkStatusChange(bool isOnline)
        {
            if (!m_IsInitialized) return;
            
            if (isOnline)
            {
                Logger.LogDemo("🌐 Network restored - refreshing loot box status");
                _ = RefreshCooldownFromServer();
            }
            CooldownChanged?.Invoke();
        }
        
        private async void ProcessClaimRequest()
        {
            if (!CanClaimLootBox())
            {
                Logger.LogWarning("Cannot claim loot box - requirements not met");
                ClaimFailed?.Invoke("Loot box not ready to be claimed!");
                return;
            }
            
            await ClaimLootBoxAsync();
        }
        #endregion
        
        #region Update Loops
        private void UpdateClientTick()
        {
            if (!IsOnCooldown()) return;
            
            m_ClientTickTimer += Time.deltaTime;
            
            if (m_ClientTickTimer >= k_ClientTickRate)
            {
                m_ClientTickTimer = 0;
                m_CooldownSecondsRemaining = Math.Max(0, m_CooldownSecondsRemaining - 1);
            
                if (m_CooldownSecondsRemaining <= 0)
                {
                    Logger.LogDemo("🎁 Client-side cooldown complete - checking with server");
                    CooldownChanged?.Invoke();
                    m_NeedsCooldownRefresh = true;
                }
                else
                {
                    CooldownTick?.Invoke(m_CooldownSecondsRemaining);
                }
            }
        }
        
        private void UpdateServerSync()
        {
            // Only sync if we're in a state that could benefit from server data
            if (!ShouldSyncWithServer())
            {
                return;
            }
            
            m_ServerSyncTimer += Time.deltaTime;

            if (m_ServerSyncTimer >= k_ServerCheckRate)
            {
                m_ServerSyncTimer = 0f;
                m_NeedsCooldownRefresh = true;
            }
        }
        
        private bool ShouldSyncWithServer()
        {
            return m_IsInitialized && m_NetworkConnectivityHandler.IsOnline;
        }
        
        #endregion
        
        #region Cloud Code Communication
        private async Task RefreshCooldownFromServer()
        {
            if (!m_NetworkConnectivityHandler.IsOnline)
            {
                CooldownChanged?.Invoke(); // Letting UI handle offline state
                return;
            }

            try
            {
                var cooldownResult = await m_BindingsProvider.GemHunterBindings.CheckLootBoxCooldown();
                HandleCooldownResult(cooldownResult);
            }
            catch (Exception e)
            {
                Logger.LogWarning($"Error checking loot box availability: {e.Message}");
                CooldownChanged?.Invoke();
            }
        }
        
        private void HandleCooldownResult(LootBoxCooldownResult cooldownResult)
        {
            if (cooldownResult == null)
            {
                Logger.LogError("Received null cooldown result from server");
                CooldownChanged?.Invoke();
                return;
            }

            long previousCooldown = m_CooldownSecondsRemaining;
            
            if (cooldownResult.CanGrantFlag)
            {
                Logger.LogDemo("🎁 Loot box is available!");
                m_CooldownSecondsRemaining = 0;
            }
            else
            {
                m_CooldownSecondsRemaining = cooldownResult.CurrentCooldown;
                Logger.LogDemo($"🎁 Loot box on cooldown: {m_CooldownSecondsRemaining}s remaining");
            }
            
            // UI controller will handle updates
            CooldownChanged?.Invoke();
            
            if (IsOnCooldown())
            {
                CooldownTick?.Invoke(m_CooldownSecondsRemaining);
            }
        }

        private async Task<bool> ClaimLootBoxAsync()
        {
            if (!CanClaimLootBox())
            {
                ClaimFailed?.Invoke("Loot box is not available");
                return false;
            }
        
            m_IsClaiming = true;
            CooldownChanged?.Invoke(); // UI can refresh state
        
            try
            {
                var result = await m_BindingsProvider.GemHunterBindings.ClaimLootBox();
                if (result != null)
                {
                    ClaimSucceeded?.Invoke(result);
                    
                    m_PlayerEconomyManagerClient.SyncEconomyData();

                    SetImmediateCooldown(20);
                    
                    m_NeedsCooldownRefresh = true;
                    return true;
                }
                return false;
            }
            catch (Exception e)
            {
                ClaimFailed?.Invoke(e.Message);
                return false;
            }
            finally
            {
                m_IsClaiming = false;
                CooldownChanged?.Invoke(); // UI will refresh
            }
        }
        
        private void SetImmediateCooldown(long seconds)
        {
            m_CooldownSecondsRemaining = seconds;
            CooldownChanged?.Invoke();
            CooldownTick?.Invoke(m_CooldownSecondsRemaining);
        }
        
        #endregion
        
        private void OnDisable()
        {
            if (m_LootBoxUIController != null)
            {
                m_LootBoxUIController.OnClickClaimLootBox -= ProcessClaimRequest;    
            }

            if (m_PlayerEconomyManagerClient != null)
            {
                m_PlayerEconomyManagerClient.PlayerEconomyInitialized -= InitializeLootBoxSystem;
            }

            if (m_NetworkConnectivityHandler != null)
            {
                m_NetworkConnectivityHandler.OnlineStatusChanged -= HandleNetworkStatusChange;
            }
        }
    }
}