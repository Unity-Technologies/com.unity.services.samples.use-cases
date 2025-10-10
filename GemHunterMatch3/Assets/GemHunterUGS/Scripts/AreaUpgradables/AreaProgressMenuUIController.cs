using System;
using System.Collections.Generic;
using System.Linq;
using GemHunterUGS.Scripts.Core;
using GemHunterUGS.Scripts.PlayerDataManagement;
using GemHunterUGS.Scripts.PlayerEconomyManagement;
using GemHunterUGS.Scripts.PlayerHub;
using GemHunterUGS.Scripts.Utilities;
using Unity.Services.CloudCode.GeneratedBindings.GemHunterUGSCloud.Models;
using UnityEngine;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;
namespace GemHunterUGS.Scripts.AreaUpgradables
{
    /// <summary>
    /// Controls the "Area Progress Menu" UI (i.e. the menu that opens from the "Area" button, not the Hub), managing the display and interaction
    /// of upgradable items, their states, and progression tracking.
    /// </summary>
    public class AreaProgressMenuUIController : MonoBehaviour
    {
        [SerializeField]
        private AreaProgressMenuView m_AreaProgressView;
        [SerializeField]
        private AreaUpgradablesUIController m_AreaUpgradablesUIController;
        [SerializeField]
        private HubUIController m_HubUIController;
        
        [SerializeField]
        private Sprite m_UpgradeIcon;
        [SerializeField]
        private Sprite m_UnlockIcon;

        [SerializeField]
        private List<Sprite> m_AreaIcons; 
        
        private PlayerDataManager m_PlayerDataManager;
        private PlayerEconomyManager m_PlayerEconomy;
        private int m_NumberOfItems;
        private Action[] m_ButtonClickHandlers;

        private bool m_IsInitialized;
        
        public void Initialize()
        {
            m_IsInitialized = false;
            m_PlayerDataManager = GameSystemLocator.Get<PlayerDataManager>();
            m_PlayerEconomy = GameSystemLocator.Get<PlayerEconomyManager>();
            
            if (m_AreaProgressView == null)
            {
                m_AreaProgressView = GetComponent<AreaProgressMenuView>();
            }
            m_AreaProgressView.Initialize();
            m_AreaProgressView.HideMenu();
            
            SetupEventHandlers();
        }

        private void SetupEventHandlers()
        {
            m_AreaUpgradablesUIController.OnOpenAreaProgressMenu += HandleOpenProgressMenu;
            m_AreaProgressView.CloseMenuButton.clicked += HandleCloseProgressMenu;
            m_PlayerDataManager.LocalPlayerDataUpdated += HandlePlayerDataUpdate;
        }

        private void HandleOpenProgressMenu(PlayerData playerData)
        {
            InitializeUI(playerData);
            
            if (m_AreaProgressView.ItemUpgradeButtons == null)
            {
                Logger.LogError("View not properly initialized before opening menu");
                return;
            }
            
            m_AreaProgressView.ShowMenu();
            m_HubUIController.HideMainHub();
            m_HubUIController.HideBottomNavBar();
            RefreshUI(playerData);
        }
        
        private void InitializeUI(PlayerData playerData)
        {
            m_NumberOfItems = playerData.CurrentArea.TotalUpgradableSlots;
            Logger.LogVerbose($"Number of total upgrade items {m_NumberOfItems}");
            
            if (!m_IsInitialized)
            {
                m_AreaProgressView.SetupListOfAreas(m_NumberOfItems);
                m_IsInitialized = true;
            }
            
            UnsubscribeButtonHandlers();
            
            m_ButtonClickHandlers = new Action[m_NumberOfItems];
            
            for (int i = 0; i < playerData.CurrentArea.TotalUpgradableSlots; i++)
            {
                InitializeUpgradeButton(i);
            }
        }

        private void HandleCloseProgressMenu()
        {
            m_AreaProgressView.HideMenu();
            m_HubUIController.ShowMainHub();
        }
        
        private void InitializeUpgradeButton(int index)
        {
            if (m_AreaProgressView.ItemUpgradeButtons[index] == null)
            {
                Logger.LogError($"Button at index {index} is null");
                return;
            }
            
            if (m_ButtonClickHandlers[index] != null)
            {
                m_AreaProgressView.ItemUpgradeButtons[index].clicked -= m_ButtonClickHandlers[index];
            }
            
            m_ButtonClickHandlers[index] = () => HandleUpgradableButtonClicked(index);
            
            // Adding the click event
            m_AreaProgressView.ItemUpgradeButtons[index].clicked += m_ButtonClickHandlers[index];
        }

        private void HandleUpgradableButtonClicked(int index)
        {
            m_AreaProgressView.LockButton(index);
            m_AreaUpgradablesUIController.OnUpgradableButtonClicked(index);
        }

        private void HandlePlayerDataUpdate(PlayerData playerData)
        {
            if (m_PlayerDataManager.PlayerDataLocal != null)
            {
                InitializeUI(playerData);
                RefreshUI(playerData);
            }
        }

        private void RefreshUI(PlayerData playerData)
        {
            Logger.LogVerbose($"Current Area Progress: {playerData.CurrentArea.CurrentProgress}. " + $"Current Area Items {playerData.CurrentArea.UpgradableAreaItems.Count}");
            
            var currentArea = playerData.CurrentArea;
            m_AreaProgressView.UpdateTotalUpgradeProgress(currentArea.CurrentProgress, currentArea.MaxProgress);
            
            // Updating for all slots, not just existing items
            for (int index = 0; index < currentArea.TotalUpgradableSlots; index++)
            {
                var item = currentArea.UpgradableAreaItems
                    .FirstOrDefault(item => item.UpgradableId == (index + 1));
                
                if (item == null || !item.IsUnlocked)
                {
                    UpdateReadyUnlockUI(index, playerData);
                    continue;
                }
                if (item.CurrentLevel < item.MaxLevel)
                {
                    UpdateUpgradableUI(index, item);
                }
                else
                {
                    UpdateUpgradableMaxUI(index, item);
                }
            }
        }

        private void UpdateReadyUnlockUI(int index, PlayerData playerData)
        {
            Logger.LogVerbose("Updating ready unlock slot");
            
            bool canAfford = playerData.Stars >= playerData.CurrentArea.UnlockRequirement_Stars;
            
            string unknownUpgradable = "to progress";
            
            m_AreaProgressView.UpdateReadyUnlockAreaItem(
                index,
                unknownUpgradable,
                0,
                AreaManager.k_DefaultMaxLevel,
                playerData.CurrentArea.UnlockRequirement_Stars,
                canAfford,
                m_UnlockIcon
                );
        }
        
        private void UpdateUpgradableUI(int index, UpgradableAreaItem item)
        {
            if (item == null)
            {
                Logger.LogWarning($"UpgradableAreaItem is null");
                return;
            }
            
            bool canAfford = m_PlayerEconomy.PlayerEconomyDataLocal.Currencies[PlayerEconomyManager.k_Coin] >= item.PerLevelCoinUpgradeRequirement;
            
            m_AreaProgressView.UpdateUpgradableAreaItem(
                index,
                item.UpgradableName,
                item.CurrentLevel,
                item.MaxLevel,
                item.PerLevelCoinUpgradeRequirement,
                canAfford,
                m_UpgradeIcon
                );
        }
        
        private void UpdateUpgradableMaxUI(int index, UpgradableAreaItem item)
        {
            m_AreaProgressView.UpdateMaxAreaItem(
                index,
                item.UpgradableName,
                item.MaxLevel
                );
        }
        
        private void OnDisable()
        {
            // Prevents unnecessary errors if PlayerHub scene is accidentally loaded first in development
            if (m_PlayerDataManager == null)
            {
                return;
            }
            
            RemoveEventHandlers();
        }

        private void RemoveEventHandlers()
        {
            m_PlayerDataManager.LocalPlayerDataUpdated -= RefreshUI;   
            m_AreaUpgradablesUIController.OnOpenAreaProgressMenu -= HandleOpenProgressMenu;
            m_AreaProgressView.CloseMenuButton.clicked -= HandleCloseProgressMenu;
            
            UnsubscribeButtonHandlers();
        }

        private void UnsubscribeButtonHandlers()
        {
            if (m_AreaProgressView == null || m_ButtonClickHandlers == null)
            {
                return;
            }
            
            for (int i = 0; i < m_ButtonClickHandlers.Length; i++)
            {
                if (m_AreaProgressView.ItemUpgradeButtons[i] != null && m_ButtonClickHandlers[i] != null)
                {
                    m_AreaProgressView.ItemUpgradeButtons[i].clicked -= m_ButtonClickHandlers[i];
                }
            }
        }
    }
}
