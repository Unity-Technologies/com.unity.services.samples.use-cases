using System;
using UnityEngine;
using GemHunterUGS.Scripts.PlayerHub;
using Unity.Services.CloudCode.GeneratedBindings.GemHunterUGSCloud.Models;
using GemHunterUGS.Scripts.Utilities;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;

namespace GemHunterUGS.Scripts.LootBoxWithCooldown
{
    /// <summary>
    /// Handles the UI representation and user interactions for the loot box system.
    /// Controls the visual states (available/cooldown), timer display, and reward presentations.
    /// A bridge between the UI elements and the LootBoxManagerClient.
    /// </summary>
    public class LootBoxUIController : MonoBehaviour
    {
        [Header("Visual Assets")]
        [SerializeField] private Sprite m_LootBoxAvailableSprite;
        [SerializeField] private Sprite m_LootBoxOnCooldownSprite;
        
        [Header("UI Text")]
        [SerializeField] private string m_AvailableText = "Claim\n<i>Ready!</i>";
        [SerializeField] private string m_CooldownText = "Until next Loot Box";
        [SerializeField] private string m_ClaimingText = "Opening...";
        [SerializeField] private string m_OfflineText = "Offline";
        [SerializeField] private string m_ErrorText = "Unavailable";
        
        // Dependencies
        private HubView m_HubView;
        private LootBoxManagerClient m_LootBoxManager;
        
        // Events
        public event Action OnClickClaimLootBox;
        
        // Constants (for debugging)
        private const string k_GiftEmoji = "🎁";

        private void OnEnable()
        {
            m_LootBoxManager = GetComponent<LootBoxManagerClient>();
            SubscribeToEvents();
        }
        
        private void SubscribeToEvents()
        {
            m_LootBoxManager.CooldownChanged += RefreshUIState;
            m_LootBoxManager.CooldownTick += UpdateCooldownDisplay;
            m_LootBoxManager.ClaimSucceeded += ShowClaimSuccess;
            m_LootBoxManager.ClaimFailed += ShowClaimError;
        }
        
        public void Initialize(HubView hubView)
        {
            m_HubView = hubView;
            m_HubView.ClaimLootBoxButton.clicked += HandleClaimButtonClicked;

            RefreshUIState();
        }
        
        #region Event Handlers
        
        private void UpdateCooldownDisplay(long secondsRemaining)
        {
            if (m_HubView == null) return;

            string timeDisplay = FormatTimeRemaining(secondsRemaining);
            m_HubView.SetLootBoxCountdownText(timeDisplay);
        }
        
        private void ShowClaimSuccess(LootBoxResult result)
        {
            var rewardsMessage = FormatRewardsMessage(result);
            Logger.LogDemo($"{k_GiftEmoji} Success! {rewardsMessage}");
            
            if (m_HubView != null)
            {
                m_HubView.ShowLootboxRewardsPopup();
                m_HubView.PlayLootBoxClaimedVFX();
            }
        }
        
        private void ShowClaimError(string error)
        {
            Logger.LogError($"Claim failed: {error}");
            
            if (m_HubView != null)
            {
                m_HubView.ShowErrorPopup($"Failed to claim loot box: {error}");
            }
        }
        
        private void HandleClaimButtonClicked()
        {
            Logger.LogDemo($"{k_GiftEmoji} Claiming Loot Box...");
            OnClickClaimLootBox?.Invoke();
        }
        
        #endregion
        
        #region UI State Management
        private void RefreshUIState()
        {
            if (m_HubView == null) return;
            
            if (!m_LootBoxManager.IsInitialized())
            {
                SetInitializingState();
            }
            else if (m_LootBoxManager.IsOffline())
            {
                SetOfflineState();
            }
            else if (m_LootBoxManager.IsClaiming())
            {
                SetClaimingState();
            }
            else if (m_LootBoxManager.CanClaimLootBox())
            {
                SetAvailableState();
            }
            else if (m_LootBoxManager.IsOnCooldown())
            {
                SetCooldownState();
            }
            else
            {
                SetErrorState();
            }
        }
        
        private void SetAvailableState()
        {
            m_HubView.SetLootBoxState(true);
            m_HubView.SetLootBoxLabelText(m_AvailableText);
            m_HubView.SetCountdownVisibility(false);
        }

        private void SetCooldownState()
        {
            m_HubView.SetLootBoxState(false);
            m_HubView.SetLootBoxLabelText(m_CooldownText);
            m_HubView.SetCountdownVisibility(true);
            
            UpdateCooldownDisplay(m_LootBoxManager.GetCooldownRemaining());
        }

        private void SetClaimingState()
        {
            // TODO Loot Box claiming sprite?
            // var claimingSprite = m_LootBoxClaimingSprite != null ? m_LootBoxClaimingSprite : m_LootBoxAvailableSprite;
            m_HubView.SetLootBoxState(false);
            m_HubView.SetLootBoxLabelText(m_ClaimingText);
            m_HubView.SetCountdownVisibility(false);
        }

        private void SetOfflineState()
        {
            m_HubView.SetLootBoxState(false);
            m_HubView.SetLootBoxLabelText($"{m_OfflineText}");
            m_HubView.SetCountdownVisibility(false);
        }

        private void SetErrorState()
        {
            m_HubView.SetLootBoxState(false);
            m_HubView.SetLootBoxLabelText($"{m_ErrorText}");
            m_HubView.SetCountdownVisibility(false);
        }

        private void SetInitializingState()
        {
            m_HubView.SetLootBoxState(false);
            m_HubView.SetLootBoxLabelText($"Loading...");
            m_HubView.SetCountdownVisibility(false);
        }
        #endregion
        
        #region Helper Methods
        private string FormatTimeRemaining(long seconds)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);
            if (timeSpan.TotalHours >= 1)
            {
                return $"{timeSpan.Hours}h {timeSpan.Minutes}m";
            }
            if (timeSpan.TotalMinutes >= 1)
            {
                return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
            }
            return $"{timeSpan.Seconds}s";
        }

        private string FormatRewardsMessage(LootBoxResult result)
        {
            var message = "You received: ";
        
            foreach (var currency in result.Currencies)
            {
                message += $"\n{currency.Key}: {currency.Value:N0}";
            }
        
            foreach (var item in result.InventoryItems)
            {
                message += $"\n{item.Key}: {item.Value}x";
            }
            return message;
        }
        
        #endregion
        
        
        private void OnDisable()
        {
            UnsubscribeFromAllEvents();
        }
        
        private void UnsubscribeFromAllEvents()
        {
            // Prevents unnecessary subscription errors if PlayerHub scene is loaded first
            if (m_HubView != null)
            {
                m_HubView.ClaimLootBoxButton.clicked -= HandleClaimButtonClicked;
            }
            
            if (m_LootBoxManager != null)
            {
                m_LootBoxManager.CooldownChanged -= RefreshUIState;
                m_LootBoxManager.CooldownTick -= UpdateCooldownDisplay;
                m_LootBoxManager.ClaimSucceeded -= ShowClaimSuccess;
                m_LootBoxManager.ClaimFailed -= ShowClaimError;
            }
        }
    }
}