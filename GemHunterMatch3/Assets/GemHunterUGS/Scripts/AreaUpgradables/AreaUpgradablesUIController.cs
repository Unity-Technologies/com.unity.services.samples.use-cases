using System;
using System.Collections;
using UnityEngine;
using System.Linq;
using GemHunterUGS.Scripts.Core;
using GemHunterUGS.Scripts.PlayerDataManagement;
using GemHunterUGS.Scripts.PlayerEconomyManagement;
using GemHunterUGS.Scripts.PlayerHub;
using Unity.Services.CloudCode.GeneratedBindings.GemHunterUGSCloud.Models;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;

namespace GemHunterUGS.Scripts.AreaUpgradables
{
    /// <summary>
    /// Controls the hub area upgradable items, managing item states, interactions,
    /// and progression visualization. Coordinates between player data and area management.
    /// </summary>
    /// <remarks>
    /// The command batching system is only lightly implemented for end of area reward validation in Cloud Code.
    /// Each upgrade action is validated in Cloud Code; the delay or wait fits the UX of the action.
    /// See the "UGS Use Case Examples" for a command batching implementation
    /// </remarks>
    public class AreaUpgradablesUIController : MonoBehaviour
    {
        [SerializeField]
        private AreaUpgradablesView m_View;
        [SerializeField]
        private HubUIController m_HubUIController;
        [SerializeField]
        private Camera m_Camera;
        
        private AreaManager m_AreaManager;
        private PlayerDataManager m_PlayerDataManager;
        private PlayerEconomyManagerClient m_PlayerEconomyManagerClient;

        private CommandBatchSystem m_CommandBatchSystem;
        private AreaData m_CurrentArea;
        
        [Header("State Icons")]
        [SerializeField] private Texture2D m_UpgradableTexture;
        [SerializeField] private Texture2D m_UnlockableTexture;
        [SerializeField] private Texture2D m_LockedTexture;
        [SerializeField] private Texture2D m_MaxUpgradedTexture;

        [Header("Upgrades prefabs")]
        [SerializeField] private GameObject[] m_UpgradableObjects;
        
        [Header("Area Complete Reward Item Icons")]
        [SerializeField] private Texture2D m_BombTexture;
        [SerializeField] private Texture2D m_ColorBonusTexture;
        [SerializeField] private Texture2D m_VerticalRocketTexture;
        
        private Action[] ButtonClickHandlers { get; set; }
        private bool m_IsUIInitialized;
        
        public event Action<int> OnUnlockClicked;
        public event Action<int> OnUpgradeClicked;
        public event Action<PlayerData> OnOpenAreaProgressMenu;

        private void Start()
        {
            if (m_View == null)
            {
                m_View = gameObject.GetComponent<AreaUpgradablesView>();
            }
            m_View.Initialize();
            
            m_AreaManager = GameSystemLocator.Get<AreaManager>();
            m_PlayerDataManager = GameSystemLocator.Get<PlayerDataManager>();
            m_CommandBatchSystem = GameSystemLocator.Get<CommandBatchSystem>();
            m_PlayerEconomyManagerClient = GameSystemLocator.Get<PlayerEconomyManagerClient>();

            if (m_Camera == null)
            {
                m_Camera = Camera.main;
            }
            
            SetupEventHandlers();
            
            var areaManagerClient = GameSystemLocator.Get<AreaManagerClient>();
            areaManagerClient.SetupEventHandlers(this);
            
            m_View.HideAreaItems();

            if (m_PlayerDataManager.PlayerDataLocal != null)
            {
                HandleAreaDataUpdate(m_PlayerDataManager.PlayerDataLocal);
            }
        }

        private void SetupEventHandlers()
        {
            m_PlayerDataManager.LocalPlayerDataUpdated += HandleAreaDataUpdate;
            m_PlayerEconomyManagerClient.EconomyDataUpdated += HandleEconomyUpdate;
            m_AreaManager.AreaActionFailed += ShowPopUp;
            m_AreaManager.AreaCompleteLocal += ShowAreaComplete;
            m_CommandBatchSystem.AreaCompleteRewardsReceived += DisplayAreaRewards;
        }
        
        private void HandleAreaDataUpdate(PlayerData playerData)
        {
            if (playerData.CurrentArea == null)
            {
                Logger.LogWarning("playerData.CurrentArea is null");
                return;
            }
            m_CurrentArea = playerData.CurrentArea;

            InitializeUI();
            RefreshAreaUI(playerData);
        }

        private void HandleEconomyUpdate(PlayerEconomyData playerEconomyData)
        {
            RefreshAreaUI(m_PlayerDataManager.PlayerDataLocal);
        }
        
        private void InitializeUI()
        {
            if (m_CurrentArea == null)
            {
                Logger.LogWarning("m_CurrentArea == null");
                return;
            }
            
            if (m_CurrentArea?.TotalUpgradableSlots <= 0)
            {
                Logger.LogError("Cannot initialize UI - TotalUpgradableSlots not set or invalid");
                return;
            }
            
            if (!m_IsUIInitialized)
            {
                SetupUIElements();
                SetupAllButtons();
                m_View.ShowAreaItems();
                m_IsUIInitialized = true;
            }
        }

        private void SetupUIElements()
        {
            m_View.SetupUpgradableElements(m_CurrentArea.TotalUpgradableSlots);
            m_View.PopUp_TimedButton.clicked += m_View.HidePopUpTimed;
            m_View.CurrentAreaProgressButton.clicked += OpenAreaProgressMenu;
            m_View.AcceptRewardsButton.clicked += HideAreaComplete;
            m_View.CloseAreaCompleteButton.clicked += HideAreaComplete;
        }

        private void SetupAllButtons()
        {
            Logger.Log($"Setting up all buttons for area {m_CurrentArea.AreaName}");
            UnsubscribeAreaItemButtonHandlers();
            ButtonClickHandlers = new Action[m_CurrentArea.TotalUpgradableSlots];
            
            for (int index = 0; index < m_CurrentArea.TotalUpgradableSlots; index++)
            {
                if (!IsButtonValid(index)) continue;
                
                int idToFind = index + 1;

                // Set up the click handler for all buttons regardless of item state
                SetupButtonClickHandler(index);

                PositionButton(index);

                RefreshButtonState(index);
            }
        }

        private bool IsButtonValid(int index)
        {
            if (index < 0 || index >= m_View.UpgradableButtons.Length)
            {
                Logger.LogError($"Button index {index} out of range");
                return false;
            }
    
            if (m_View.UpgradableButtons[index] == null)
            {
                Logger.LogError($"Button at index {index} is null");
                return false;
            }
    
            if (index >= m_UpgradableObjects.Length || m_UpgradableObjects[index] == null)
            {
                Logger.LogError($"Upgradable object at index {index} is null or index out of range");
                return false;
            }
    
            return true;
        }

        private void PositionButton(int index)
        {
            m_View.UpdateUpgradableButtonPosition(index, m_UpgradableObjects[index].transform.position);;
        }

        private void RefreshButtonState(int index)
        {
            int itemId = index + 1;
            var item = m_CurrentArea.UpgradableAreaItems?.FirstOrDefault(item => item.UpgradableId == itemId);
    
            if (item != null)
            {
                // Item exists - update its state
                UpdateExistingItemState(index, item);
            }
            else
            {
                // Item doesn't exist - show as locked/unlockable
                UpdateLockedItemState(index);
            }
        }
        
        private void SetupButtonClickHandler(int index)
        {
            // Clean up previous handler if it exists
            if (ButtonClickHandlers[index] != null)
            {
                m_View.UpgradableButtons[index].clicked -= ButtonClickHandlers[index];
            }
    
            // Create and assign new handler
            ButtonClickHandlers[index] = () => OnUpgradableButtonClicked(index);
            m_View.UpgradableButtons[index].clicked += ButtonClickHandlers[index];
        }
        
        private void UpdateExistingItemState(int index, UpgradableAreaItem item)
        {
            int itemId = index + 1;
            bool canUpgrade = item.CurrentLevel < item.MaxLevel && m_AreaManager.CanPlayerAffordUpgrade(itemId);;
            bool isEnabled = item.IsUnlocked || canUpgrade;
            
            string buttonText = GetButtonText(item);
            
            m_View.UpdateProgressBar(index, item.CurrentLevel, item.MaxLevel, item.IsUnlocked);
            m_View.UpdateUpgradableButton(index, isEnabled, buttonText);
            
            UpdateUpgradableObjectVisual(index, true);
        }
        
        private void UpdateLockedItemState(int index)
        {
            bool isEnabled = m_AreaManager.CanPlayerAffordUnlock();
            string buttonText = $"{m_CurrentArea.UnlockRequirement_Stars}";
            
            m_View.UpdateUpgradableButton(index, isEnabled, buttonText);
            m_View.UpdateProgressBar(index, 0, 0, false);
            
            UpdateUpgradableObjectVisual(index, false);
        }
        
        private void RefreshAreaUI(PlayerData playerData)
        {
            Logger.LogVerbose($"RefreshAreaUI called with player data. Current Area Progress: {{playerData.CurrentArea.CurrentProgress}}.");
            
            m_CurrentArea = playerData.CurrentArea;
            
            for (int i = 0; i < m_CurrentArea.TotalUpgradableSlots; i++)
            {
                if (!IsButtonValid(i)) continue;
                
                RefreshButtonState(i);
            }

            UpdateCurrentAreaProgressButton();
        }
        
        private void UpdateUpgradableObjectVisual(int index, bool isVisible)
        {
            if (isVisible)
            {
                m_UpgradableObjects[index].GetComponent<SpriteRenderer>().color= Color.white;
            } 
            else 
            {
                m_UpgradableObjects[index].GetComponent<SpriteRenderer>().color= new Color(0.2f,0.2f,0.2f,0.8f);
            }
        }
        
        private string GetButtonText(UpgradableAreaItem item)
        {
            if (!item.IsUnlocked)
            {
                return $"{m_CurrentArea.UnlockRequirement_Stars}";
            }
            if (item.CurrentLevel < item.MaxLevel)
            {
                int upgradeCost = item.PerLevelCoinUpgradeRequirement;
                //return $"({upgradeCost} Coins) for level {item.CurrentLevel+1}";
                return $"{upgradeCost} Coins";
            }

            return "Max Level";
        }
        
        private void UpdateCurrentAreaProgressButton()
        {
            var area = m_PlayerDataManager.PlayerDataLocal.CurrentArea;
            Logger.LogVerbose($"Updating area progress button: Progress={area.CurrentProgress}, MaxProgress={area.MaxProgress}");
            
            m_View.UpdateCurrentAreaProgress(area.CurrentProgress, area.MaxProgress, m_CurrentArea.AreaName);
        }

        private void OpenAreaProgressMenu()
        {
            m_View.ShowAreaProgressMenu();
            m_HubUIController.HideMainHub();
            OnOpenAreaProgressMenu?.Invoke(m_PlayerDataManager.PlayerDataLocal);
        }
        
        public void OnUpgradableButtonClicked(int index)
        {
            m_View.SetButtonProcessingState(index, true);
            int itemId = index + 1;
            Logger.LogVerbose($"Button Clicked: for Upgradable Item ID: {itemId}");
            
            if (!m_AreaManager.IsItemUnlocked(itemId))
            {
                // Try to unlock - the method will handle affordability and error messages
                if (m_AreaManager.UnlockItem(itemId))
                {
                    Logger.LogVerbose("Unlocking...");
                    OnUnlockClicked?.Invoke(itemId);
                    var unlockCommand = new UnlockAreaItemCommand(itemId);
                    unlockCommand.Execute();
                    m_CommandBatchSystem.EnqueueCommand(unlockCommand);
                }
                return;
            }
    
            // Try to upgrade - the method will handle affordability and error messages
            if (m_AreaManager.UpgradeItem(itemId))
            {
                Logger.LogVerbose("Upgrading...");
                OnUpgradeClicked?.Invoke(itemId);
                var upgradeCommand = new UpgradeAreaItemCommand(itemId);
                upgradeCommand.Execute();
                m_CommandBatchSystem.EnqueueCommand(upgradeCommand);
            }
        }
        
        private void ShowPopUp(string text)
        {
            m_View.ShowPopUpTimed(text);
            StartCoroutine(HidePopUpAfterWait());
        }

        private void ShowAreaComplete()
        {
            m_View.ShowAreaComplete();
        }

        private void HideAreaComplete()
        {
            m_View.HideAreaComplete();
            m_View.HideAreaProgressMenu();
            m_HubUIController.ShowMainHub();
        }
        
        private IEnumerator HidePopUpAfterWait()
        {
            yield return new WaitForSeconds(1f);
            HidePopUp();
        }

        private void HidePopUp()
        {
            m_View.HidePopUpTimed();
        }

        private void DisplayAreaRewards(CommandReward rewards)
        {
            Logger.LogVerbose($"Displaying {rewards.Rewards.Count} rewards");
            string[] rewardAmountText = new string[rewards.Rewards.Count];
            Texture2D[] rewardTexture = new Texture2D[rewards.Rewards.Count];
            
            for (int i = 0; i < rewards.Rewards.Count; i++)
            {
                rewardAmountText[i] = rewards.Rewards[i].Amount.ToString();
                string id = rewards.Rewards[i].Id;
                rewardTexture[i] = id switch
                {
                    "COLOR_BONUS" => m_ColorBonusTexture,
                    "VERTICAL_ROCKET" => m_VerticalRocketTexture,
                    "LARGE_BOMB" => m_BombTexture,
                    _ => rewardTexture[i]
                };
            }
            
            m_View.ShowAreaCompleteRewards(rewardTexture[0], rewardTexture[1], rewardTexture[2], rewardAmountText[0], rewardAmountText[1], rewardAmountText[2]);
        }
        
        private void OnDestroy()
        {
            RemoveEventHandlers();
        }

        private void RemoveEventHandlers()
        {
            if (m_PlayerDataManager == null)
            {
                Logger.LogError("PlayerDataManager is not initialized");
                return;
            }
            
            if (m_View != null && m_View.PopUp_TimedButton != null)
            {
                m_View.PopUp_TimedButton.clicked -= m_View.HidePopUpTimed;
                m_View.CurrentAreaProgressButton.clicked -= OpenAreaProgressMenu;
                m_View.AcceptRewardsButton.clicked -= HideAreaComplete;
                m_View.CloseAreaCompleteButton.clicked -= HideAreaComplete;
            }
            
            m_PlayerDataManager.LocalPlayerDataUpdated -= HandleAreaDataUpdate;
            m_PlayerEconomyManagerClient.EconomyDataUpdated -= HandleEconomyUpdate;
            m_AreaManager.AreaActionFailed -= ShowPopUp;
            m_AreaManager.AreaCompleteLocal -= ShowAreaComplete;
            m_CommandBatchSystem.AreaCompleteRewardsReceived -= DisplayAreaRewards;
            
            UnsubscribeAreaItemButtonHandlers();
        }
        
        private void UnsubscribeAreaItemButtonHandlers()
        {
            if (m_View == null || ButtonClickHandlers == null)
            {
                return;
            }
            
            for (int i = 0; i < ButtonClickHandlers.Length; i++)
            {
                if (m_View.UpgradableButtons[i] != null && ButtonClickHandlers[i] != null)
                {
                    m_View.UpgradableButtons[i].clicked -= ButtonClickHandlers[i];
                }
            }
        }
    }
}
