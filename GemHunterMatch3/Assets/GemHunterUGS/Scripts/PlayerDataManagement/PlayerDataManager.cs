using System;
using System.Collections.Generic;
using GemHunterUGS.Scripts.AreaUpgradables;
using Unity.Services.Authentication;
using Unity.Services.CloudCode.GeneratedBindings.GemHunterUGSCloud.Models;
using UnityEngine;
using WebSocketSharp;
using GemHunterUGS.Scripts.Core;
using GemHunterUGS.Scripts.PlayerEconomyManagement;
using GemHunterUGS.Scripts.Utilities;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;

namespace GemHunterUGS.Scripts.PlayerDataManagement
{
    /// <summary>
    /// Core manager for player data persistence and synchronization between local storage and cloud.
    /// Handles player progression (hearts, stars), profile customization, and gameplay rewards.
    /// 
    /// Key responsibilities:
    /// - Local data management and persistence
    /// - Cloud data synchronization
    /// - Player state modifications (hearts, stars, gifts)
    /// - Profile picture management
    /// - Gameplay reward handling
    /// </summary>
    public class PlayerDataManager : IDisposable
    {
        public RandomProfilePicturesSO RandomProfilePicturesSO { get; private set; }
        
        public ProfilePicture ProfilePictureData { get; private set; }
        public Sprite ProfileSprite { get; private set; }
        public PlayerData PlayerDataLocal { get; private set; }
        public string PlayerId { get; private set; }
        public bool IsCloudDataInitialized { get; private set; }

        private readonly GameManagerUGS m_GameManagerUGS;
        private readonly LocalStorageSystem m_LocalStorageSystem;
        
        private bool m_IsAnonymous;
        private PlayerDataManagerClient m_PlayerDataManagerClient;
        private PlayerEconomyManager m_PlayerEconomyManager;

        public event Action<PlayerData> LocalPlayerDataUpdated;
        public event Action<Sprite> ProfilePictureUpdated;
        public event Action NoGiftHeartsLeftPopup;
        public event Action CloudDataInitialized;
        public event Action DeleteCachedData;
        
        // Emojis to make the demo logs easier to follow
        private const string k_HeartEmoji = "💖";
        private const string k_BrokenHeartEmoji = "💔";
        private const string k_StarEmoji = "⭐";
        private const string k_EventEmoji = "⚡";
        
        public PlayerDataManager(GameManagerUGS gameManagerUGS, LocalStorageSystem localStorageSystem, RandomProfilePicturesSO profilePics)
        {
            m_GameManagerUGS = gameManagerUGS;
            m_LocalStorageSystem = localStorageSystem;
            RandomProfilePicturesSO = profilePics;
        }
        
        public void Initialize(PlayerDataManagerClient playerDataManagerClient, PlayerEconomyManager playerEconomyManager)
        {
            m_PlayerDataManagerClient = playerDataManagerClient;
            m_PlayerDataManagerClient.ProfilePictureFetched += OverwriteProfilePicture;
            m_PlayerDataManagerClient.PlayerDataUpdated += OverwriteLocalPlayerData;
            m_PlayerDataManagerClient.PlayerInitialized += OnPlayerInitializationComplete;

            m_PlayerEconomyManager = playerEconomyManager;
            m_PlayerEconomyManager.InfiniteHeartStatusUpdated += HandleInfiniteHeartStatus;
            
            m_GameManagerUGS.GameplayLevelWon += HandleLevelWon;
            m_GameManagerUGS.GameplayReplayLevelLost += HandleReplayLostLevel;
            
            // Can be useful for offline play
            InitializeLocalPlayerData();
        }
        
        private void InitializeLocalPlayerData()
        {
            PlayerDataLocal = m_LocalStorageSystem.LoadPlayerData();
            ProfilePictureData = m_LocalStorageSystem.LoadProfilePicture();
            
            if (PlayerDataLocal == null)
            {
                Logger.LogDemo("No saved PlayerDataLocal: Creating new player data");
                
                CreateNewPlayer();
                SavePlayerDataLocal();
            }
            
            if (ProfilePictureData == null)
            {
                Logger.LogDemo("Profile picture null, will initialize in cloud");    
            }
            else
            {
                SetProfileSprite();
            }
            
            PlayerId = AuthenticationService.Instance.PlayerId;
            
            LocalPlayerDataUpdated?.Invoke(PlayerDataLocal);
        }

        private void OnPlayerInitializationComplete()
        {
            IsCloudDataInitialized = true;
            CloudDataInitialized?.Invoke();
        }
        
        private void SaveProfilePictureLocal()
        {
            m_LocalStorageSystem.SaveProfilePicture(ProfilePictureData);
        }
        
        private void CreateNewPlayer()
        {
            PlayerDataLocal = CreateStartingPlayerData();
        }

        private void OverwriteLocalPlayerData(PlayerData cloudPlayerData)
        {
            if (cloudPlayerData == null)
            {
                Logger.LogWarning("Player data is null");
            }
            if (PlayerId.IsNullOrEmpty())
            {
                PlayerId = AuthenticationService.Instance.PlayerId;
                Logger.LogDemo($"PlayerId updated: {PlayerId}");
            }
            
            PlayerDataLocal.DisplayName = cloudPlayerData.DisplayName;
            PlayerDataLocal.Hearts = cloudPlayerData.Hearts;
            PlayerDataLocal.GiftHearts = cloudPlayerData.GiftHearts;
            PlayerDataLocal.Stars = cloudPlayerData.Stars;
            PlayerDataLocal.CurrentArea = cloudPlayerData.CurrentArea;
            PlayerDataLocal.GameAreasData = cloudPlayerData.GameAreasData;
            
            SavePlayerDataLocal();
            
            Logger.LogDemo($"{k_EventEmoji} LocalPlayerDataUpdated");
            LocalPlayerDataUpdated?.Invoke(PlayerDataLocal);
        }

        private void SavePlayerDataLocal()
        {
            m_LocalStorageSystem.SavePlayerData(PlayerDataLocal);
        }
        
        public void UpdateAnonymousStatus(bool isAnonymous)
        {
            m_IsAnonymous = isAnonymous;
        }
        
        /// <summary>
        /// Creates and saves a default PlayerDataLocal object for new players.
        /// </summary>
        /// <returns>A new PlayerDataLocal object with default values.</returns>
        private PlayerData CreateStartingPlayerData()
        {
            var initialArea = CreateFirstAreaData();
            var playerData = new PlayerData()
            {
                DisplayName = "New Player",
                Hearts = 1,
                Stars = 1,
                GiftHearts = 0,
                CurrentArea = initialArea,
                HasInfiniteHeartEffectActive = false,
                GameAreasData = new List<AreaData>
                {
                    initialArea
                },
            };
            
            return playerData;
        }

        private AreaData CreateFirstAreaData()
        {
            Logger.LogDemo("Creating first area data");
            
            var newAreaData = new AreaData
            {
                AreaName = "Area1",
                AreaLevel = 1,
                CurrentProgress = 0,
                MaxProgress = 25,
                TotalUpgradableSlots = 5,
                UnlockRequirement_Stars = 1,
                UpgradableAreaItems = new List<UpgradableAreaItem>()
            };
        
            newAreaData.UpgradableAreaItems.Add(new UpgradableAreaItem
            {
                UpgradableName = "Castle",
                UpgradableId = 1,
                IsUnlocked = true,  // First item starts unlocked
                CurrentLevel = 0,
                MaxLevel = 5,
                PerLevelCoinUpgradeRequirement = 20
            });
        
            return newAreaData;
        }
        
        public void DeleteLocalPlayerData()
        {
            PlayerDataLocal = null;
            PlayerId = null;
            ProfilePictureData = null;
            IsCloudDataInitialized = false;

            GameSystemLocator.Get<CommandBatchSystem>()?.ClearForAccountDeletion();
            
            DeleteCachedData?.Invoke();
            m_LocalStorageSystem.DeleteLocalData();
        }
        
        public bool IsPlayerAnonymous()
        {
            m_IsAnonymous = AuthenticationService.Instance.IsSignedIn && AuthenticationService.Instance.IsAuthorized && AuthenticationService.Instance.PlayerInfo.Identities.Count == 0;
            return m_IsAnonymous;
        }
        
        private void SetProfileSprite()
        {
            ProfileSprite = ProfilePictureData.Type switch
            {
                "pre-made" => RandomProfilePicturesSO.ProfilePictures[ProfilePictureData.ImageId],
                "custom" => ProfilePictureData.ImageData.ConvertBase64ToSprite(),
                _ => ProfileSprite
            };
            
            Logger.LogDemo($"{k_EventEmoji} ProfilePictureUpdated");
            ProfilePictureUpdated?.Invoke(ProfileSprite);
        }

        public void OverwriteProfilePicture(ProfilePicture profilePicture)
        {
            if (profilePicture == null)
            {
                Logger.LogWarning("Profile picture is null");
                return;
            }
            ProfilePictureData = profilePicture;
            SetProfileSprite();
            SaveProfilePictureLocal();
        }

        public void HandleUpdateDisplayName(string displayName)
        {
            PlayerDataLocal.DisplayName = displayName;
            LocalPlayerDataUpdated?.Invoke(PlayerDataLocal);
        }

        public bool ModifyStars(int amount)
        {
            if (amount < 0 && PlayerDataLocal.Stars < Math.Abs(amount))
            {
                Logger.LogWarning($"Not enough stars. Required: {Math.Abs(amount)}, Available: {PlayerDataLocal.Stars}");
                return false;
            }
            
            PlayerDataLocal.Stars += amount;
            SavePlayerDataLocal();
            return true;
        }

        private bool ModifyHearts(int amount)
        {
            int newHeartCount = PlayerDataLocal.Hearts + amount;
            if (newHeartCount <= 0)
            {
                Logger.LogWarning($"Can't modify hearts: Would result in {newHeartCount} hearts");
                // TODO Handle all hearts lost
                return false;
            }
    
            PlayerDataLocal.Hearts = newHeartCount;
            SavePlayerDataLocal();
            return true;
        }

        public bool ModifyGiftHearts(int amount)
        {
            if (amount < 0 && PlayerDataLocal.GiftHearts + amount < 0)
            {
                Logger.LogWarning($"{k_BrokenHeartEmoji} No gift hearts left");
                NoGiftHeartsLeftPopup?.Invoke();
                return false;
            }
            if (amount > 3)
            {
                Logger.LogWarning("Max gift hearts reached");
                return false;
            }
            PlayerDataLocal.GiftHearts += amount;
            SavePlayerDataLocal();
            return true;
        }

        private void HandleLevelWon()
        {
            if (ModifyStars(1))
            {
                Logger.LogDemo($"{k_StarEmoji} Awarded Star on LevelComplete");
            }
        }

        private void HandleReplayLostLevel()
        {
            if (HasInfiniteHeartEffectActive())
            {
                Logger.LogDemo($"{k_HeartEmoji} No hearts deducted for replay");
                return;
            }
            
            if (ModifyHearts(-1))
            {
                Logger.LogDemo($"{k_BrokenHeartEmoji} Deducted heart on level replay");
            }
        }

        private void HandleInfiniteHeartStatus(bool active)
        {
            PlayerDataLocal.HasInfiniteHeartEffectActive = active;
        }

        private bool HasInfiniteHeartEffectActive()
        {
            long currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
            // If player has infinite hearts, and it hasn't expired, don't deduct hearts
            // TODO should probably check this in the cloud
            if (m_PlayerEconomyManager.PlayerEconomyDataLocal.InfiniteHeartsExpiryTimestamp <= currentTimestamp)
            {
                PlayerDataLocal.HasInfiniteHeartEffectActive = false;
                return false;
            }
            long minutesRemaining = (m_PlayerEconomyManager.PlayerEconomyDataLocal.InfiniteHeartsExpiryTimestamp - currentTimestamp) / 60;
            Logger.LogDemo($"{k_HeartEmoji} Infinite Hearts active - Expires in {minutesRemaining}m");
            return true;
        }

        public void Dispose()
        {
            m_GameManagerUGS.GameplayLevelWon -= HandleLevelWon;
            m_GameManagerUGS.GameplayReplayLevelLost -= HandleReplayLostLevel;
            
            m_PlayerDataManagerClient.ProfilePictureFetched -= OverwriteProfilePicture;
            m_PlayerDataManagerClient.PlayerDataUpdated -= OverwriteLocalPlayerData;
        }
    }
}