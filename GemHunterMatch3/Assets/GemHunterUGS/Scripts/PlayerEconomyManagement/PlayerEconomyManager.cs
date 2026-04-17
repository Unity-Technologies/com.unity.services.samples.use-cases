using System;
using System.Collections.Generic;
using UnityEngine;
using GemHunterUGS.Scripts.Core;
using GemHunterUGS.Scripts.LootBoxWithCooldown;
using Unity.Services.CloudCode.GeneratedBindings.GemHunterUGSCloud.Models;
using GemHunterUGS.Scripts.Utilities;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;
namespace GemHunterUGS.Scripts.PlayerEconomyManagement
{
    /// <summary>
    /// Handles client-side economy logic and local data management.
    /// Applies optimistic updates and maintains local state.
    /// </summary>
    public class PlayerEconomyManager : IDisposable
    {
        public PlayerEconomyData PlayerEconomyDataLocal { get; private set; }
        
        private readonly LocalStorageSystem m_LocalStorageSystem;
        
        private PlayerEconomyManagerClient m_PlayerEconomyManagerClient;
        private LootBoxManagerClient m_LootBoxManagerClient;
        
        public event Action<PlayerEconomyData> LocalEconomyDataUpdated;
        public event Action<bool> InfiniteHeartStatusUpdated;
        
        // Not presently subscribed to but keeping for potential UI VFX implementation
        public event Action<string, int> CurrencyUpdated;
        public event Action<string, int> InventoryItemUpdated;

        // Constants
        // Items
        public const string k_LargeBomb = "LARGE_BOMB";
        public const string k_SmallBomb = "SMALL_BOMB";
        public const string k_ColorBonus = "COLOR_BONUS";
        public const string k_InfiniteHeart = "INFINITE_HEART";
        public const string k_HorizontalRocket = "HORIZONTAL_ROCKET";
        public const string k_VerticalRocket = "VERTICAL_ROCKET";
        
        // Currencies
        public const string k_Coin = "COIN";
        
        
        // Default-starting values - will be overridden by cloud data
        private static readonly Dictionary<string, int> k_DefaultCurrencies = new()
        {
            { k_Coin, 1 },
        };
        
        private static readonly Dictionary<string, int> k_DefaultInventory = new()
        {
            { k_LargeBomb, 1 },
            { k_SmallBomb, 1 },
            { k_HorizontalRocket, 1 },
            { k_VerticalRocket, 1 },
            { k_ColorBonus, 1 }
        };
        
        public PlayerEconomyManager(LocalStorageSystem localStorageSystem)
        {
            m_LocalStorageSystem = localStorageSystem;
        }
        
        public void Initialize(PlayerEconomyManagerClient playerEconomyManagerClient)
        {
            m_PlayerEconomyManagerClient = playerEconomyManagerClient;
            m_PlayerEconomyManagerClient.EconomyDataUpdated += OverwriteLocalEconomyData;
            
            InitializePlayerEconomyData();
        }
        
        private void InitializePlayerEconomyData()
        {
            PlayerEconomyDataLocal = m_LocalStorageSystem.LoadEconomyData();

            if (PlayerEconomyDataLocal == null)
            {
                CreateNewLocalEconomyData();
                SaveLocalEconomyData();
                LocalEconomyDataUpdated?.Invoke(PlayerEconomyDataLocal);
            }
        }
        
        // For offline placeholder
        private void CreateNewLocalEconomyData()
        {
            Logger.LogDemo("Creating new local economy data with default values");
            
            PlayerEconomyDataLocal = new PlayerEconomyData
            {
                Currencies = new Dictionary<string, int>(k_DefaultCurrencies),
                ItemInventory = new Dictionary<string, int>(k_DefaultInventory),
                HasPurchasedFreeCoinPack = false,
                InfiniteHeartsExpiryTimestamp = 0,
            };
        }

        private void OverwriteLocalEconomyData(PlayerEconomyData cloudEconomyData)
        {
            Logger.LogVerbose("Overwriting local economy data with cloud data");
            
            PlayerEconomyDataLocal.Currencies = cloudEconomyData.Currencies;
            PlayerEconomyDataLocal.ItemInventory = cloudEconomyData.ItemInventory;
            
            PlayerEconomyDataLocal.InfiniteHeartsExpiryTimestamp = cloudEconomyData.InfiniteHeartsExpiryTimestamp;
            CheckInfiniteHeartStatus();
            SaveLocalEconomyData();
            LocalEconomyDataUpdated?.Invoke(PlayerEconomyDataLocal);
        }
        
        private void SaveLocalEconomyData()
        {
            m_LocalStorageSystem.SaveEconomyData(PlayerEconomyDataLocal);
        }
        
        public bool TryDeductCurrencyLocal(string currencyId, int deductionAmount)
        {
            if (!PlayerEconomyDataLocal.Currencies.TryGetValue(currencyId, out int currentAmount))
            {
                Logger.LogWarning($"Currency {currencyId} not found");
                return false;
            }

            if (currentAmount < deductionAmount)
            {
                Logger.LogWarning($"Not enough {currencyId}. Required: {deductionAmount}, Available: {currentAmount}");
                return false;
            }
            
            PlayerEconomyDataLocal.Currencies[currencyId] = currentAmount - deductionAmount;
            SaveLocalEconomyData();
            
            Logger.LogDemo($"⚡LocalEconomyDataUpdated with {PlayerEconomyDataLocal.Currencies[k_Coin]} coins");
            LocalEconomyDataUpdated?.Invoke(PlayerEconomyDataLocal);
            return true;
        }
        
        public void ApplyLocalRewards(Dictionary<string, int> currencies, Dictionary<string, int> inventoryItems = null)
        {
            if (currencies != null)
            {
                foreach (var currency in currencies)
                {
                    UpdateLocalCurrency(currency.Key, currency.Value);
                }
            }
    
            if (inventoryItems != null)
            {
                foreach (var item in inventoryItems)
                {
                    UpdateLocalInventoryItem(item.Key, item.Value);
                }
            }
    
            SaveLocalEconomyData();
            LocalEconomyDataUpdated?.Invoke(PlayerEconomyDataLocal);
        }
        
        private void UpdateLocalCurrency(string currencyId, int amount)
        {
            PlayerEconomyDataLocal.Currencies[currencyId] = 
                PlayerEconomyDataLocal.Currencies.GetValueOrDefault(currencyId) + amount;
            
            CurrencyUpdated?.Invoke(currencyId, PlayerEconomyDataLocal.Currencies[currencyId]);
        }
        
        private void UpdateLocalInventoryItem(string itemId, int quantity)
        {
            PlayerEconomyDataLocal.ItemInventory[itemId] = 
                PlayerEconomyDataLocal.ItemInventory.GetValueOrDefault(itemId) + quantity;
            
            InventoryItemUpdated?.Invoke(itemId, PlayerEconomyDataLocal.ItemInventory[itemId]);
        }

        public int GetCurrencyBalance(string currencyId)
        {
            return PlayerEconomyDataLocal.Currencies.GetValueOrDefault(currencyId);
        }
        
        public void CheckInfiniteHeartStatus()
        {
            long currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            
            if (PlayerEconomyDataLocal.InfiniteHeartsExpiryTimestamp >= currentTimestamp)
            {
                Logger.LogDemo("💖⚡InfiniteHeartStatusUpdated = true");
                InfiniteHeartStatusUpdated?.Invoke(true);
            }
            else
            {
                PlayerEconomyDataLocal.InfiniteHeartsExpiryTimestamp = 0;
                InfiniteHeartStatusUpdated?.Invoke(false);
            }
            
        }
        
        public void Dispose()
        {
            m_PlayerEconomyManagerClient.EconomyDataUpdated -= OverwriteLocalEconomyData;
        }
    }
}