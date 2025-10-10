using System;
using GemHunterUGS.Scripts.PlayerHub;
using Unity.Services.CloudCode.GeneratedBindings.GemHunterUGSCloud.Models;
using UnityEngine;
using UnityEngine.UIElements;
namespace GemHunterUGS.Scripts.DailyRewards
{
    public class DailyRewardsUIController : MonoBehaviour
    {
        [SerializeField] private Texture2D m_RewardAvailableTexture;
        [SerializeField] private Texture2D m_RewardClaimedTexture;
        
        [SerializeField] private DailyRewardsManager m_DailyRewardsManager;
        [SerializeField] private HubUIController m_HubUIController;

        private DailyRewardsView m_DailyRewardsView;
        
        private bool m_IsCountingDown;
        private long m_LastKnownCooldown;
        private float m_TickTimer;
        private const float k_TickRate = 1;
        
        public event Action ClaimDailyRewardRequested;
        public event Action OpeningDailyRewardMenu;
        
        public void Initialize()
        {
            m_DailyRewardsView = GetComponent<DailyRewardsView>();
            m_DailyRewardsView.HideDailyRewardsMenu();
            SetupEventHandlers();
        }

        private void SetupEventHandlers()
        {
            m_DailyRewardsManager.DailyRewardsResultUpdated += UpdateCooldownState;
            m_DailyRewardsManager.ClaimedDailyReward += HandleRewardClaimed;
            
            m_DailyRewardsView.CloseAcceptScreenButton.clicked += m_DailyRewardsView.HideAcceptDailyRewardScreen;
            m_DailyRewardsView.AcceptRewardsButton.clicked += m_DailyRewardsView.HideAcceptDailyRewardScreen;
            m_DailyRewardsView.DailyRewardButton.clicked += RequestClaimDailyReward;
            m_DailyRewardsView.ClaimedRewardsButton.clicked += ShowDailyRewardsMenu;
            m_DailyRewardsView.CloseMenu.clicked += CloseDailyRewardsMenu;
        }
        
        private void Update()
        {
            if (!m_IsCountingDown) return;
            UpdateCooldownTick();
        }

        private void UpdateCooldownTick()
        {
            m_TickTimer += Time.deltaTime;

            if (m_TickTimer >= k_TickRate)
            {
                m_TickTimer = 0;
                m_LastKnownCooldown = Math.Max(0, m_LastKnownCooldown - 1);

                if (m_LastKnownCooldown <= 0)
                {
                    m_IsCountingDown = false;
                    SetClaimButtonState(true, "Claim", "Ready!");
                }
                else
                {
                    SetClaimButtonState(false, "Wait", FormatTimeRemaining(m_LastKnownCooldown));
                }
            }
        }

        private void UpdateCooldownState(DailyRewardsResult result)
        {
            m_LastKnownCooldown = result.SecondsTillClaimable;
            m_IsCountingDown = m_LastKnownCooldown > 0;
            
            if (m_IsCountingDown)
            {
                SetClaimButtonState(false, "Wait", FormatTimeRemaining(m_LastKnownCooldown));
            }
            else
            {
                SetClaimButtonState(true, "Claim", "Ready!");
            }
        }

        private void SetClaimButtonState(bool canClaim, string buttonText, string countdown)
        {
            m_DailyRewardsView.SetClaimDailyRewardButton(canClaim, buttonText, countdown);
        }
        
        private void ShowDailyRewardsMenu()
        {
            m_HubUIController.HideMainHub();
            m_HubUIController.HideBottomNavBar();
            m_DailyRewardsView.ShowDailyRewardsMenu();
            OpeningDailyRewardMenu?.Invoke();
        }

        private void CloseDailyRewardsMenu()
        {
            m_HubUIController.ShowMainHub();
            m_DailyRewardsView.HideDailyRewardsMenu();
        }

        private void HandleRewardClaimed(DailyRewardClaimEventArgs args)
        {
            m_DailyRewardsView.ShowAcceptDailyRewardScreen(args.RewardAmount.ToString());
            m_DailyRewardsView.LockClaimDailyRewardButton();
        }
        
        private void RequestClaimDailyReward()
        {
            ClaimDailyRewardRequested?.Invoke();
        }
        
        private string FormatTimeRemaining(long totalSeconds)
        {
            if (totalSeconds <= 0)
            {
                return "Ready!";
            }

            TimeSpan timeSpan = TimeSpan.FromSeconds(totalSeconds);
    
            if (timeSpan.Hours > 0)
            {
                return $"{timeSpan.Hours}h {timeSpan.Minutes}m";
            }
    
            if (timeSpan.Minutes > 0)
            {
                return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
            }
    
            return $"{timeSpan.Seconds}s";
        }
        
        private void OnDisable()
        {
            // Prevents unnecessary subscription errors if PlayerHub scene is loaded first
            if (m_DailyRewardsView == null || m_DailyRewardsView.CloseAcceptScreenButton == null)
            {
                return;
            }
            
            RemoveEventHandlers();
        }

        private void RemoveEventHandlers()
        {
            m_DailyRewardsManager.DailyRewardsResultUpdated -= UpdateCooldownState;
            m_DailyRewardsManager.ClaimedDailyReward -= HandleRewardClaimed;
            
            m_DailyRewardsView.CloseAcceptScreenButton.clicked -= m_DailyRewardsView.HideAcceptDailyRewardScreen;
            m_DailyRewardsView.AcceptRewardsButton.clicked -= m_DailyRewardsView.HideAcceptDailyRewardScreen;
            m_DailyRewardsView.ClaimedRewardsButton.clicked -= ShowDailyRewardsMenu;
            m_DailyRewardsView.CloseMenu.clicked -= CloseDailyRewardsMenu;
            m_DailyRewardsView.DailyRewardButton.clicked -= RequestClaimDailyReward;
        }
    }
}