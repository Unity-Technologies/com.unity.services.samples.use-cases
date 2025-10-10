using System.Collections.Generic;
using Unity.Services.CloudCode.GeneratedBindings.GemHunterUGSCloud.Models;
using UnityEngine;
using UnityEngine.UIElements;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;
namespace GemHunterUGS.Scripts.DailyRewards
{
    /// <summary>
    /// Controls the UI for displaying daily rewards in a scrollable list format.
    /// Handles the visual states for claimed, claimable, and locked rewards.
    /// </summary>
    public class DailyRewardClaimsListViewController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private UIDocument m_Document;
        [SerializeField] private DailyRewardsManager m_DailyRewardsManager;
        [SerializeField] private DailyRewardsClient m_DailyRewardsClient;
        [SerializeField] private DailyRewardsUIController m_DailyRewardsUIController;
        
        [Header("UI Assets - Backgrounds")]
        [SerializeField] private Sprite m_ClaimedRewardsIconBackground;
        [SerializeField] private Sprite m_ClaimRewardsIconBackground;
        [SerializeField] private Sprite m_ClaimedRewardsBackgroundDark;
        [SerializeField] private Sprite m_ClaimRewardBackground;
        [SerializeField] private Sprite m_CantClaimRewardsBackground;
        
        [Header("UI Assets - Icons")]
        [SerializeField] private Sprite m_CantClaimIcon;
        [SerializeField] private Sprite m_CantClaimAmountIcon;
        [SerializeField] private Sprite m_CanClaimIcon;
        [SerializeField] private Sprite m_CanClaimAmountIcon;
        
        private ListView m_ListView;
        private const float k_ItemHeight = 200f;
        private readonly List<DailyRewardListViewModel> m_RewardItems = new List<DailyRewardListViewModel>();
        
        private void Start()
        {
            if (m_DailyRewardsClient == null)
            {
                m_DailyRewardsClient = GetComponent<DailyRewardsClient>();
            }

            if (m_DailyRewardsUIController == null)
            {
                m_DailyRewardsUIController.GetComponent<DailyRewardsUIController>();
            }

            if (m_DailyRewardsManager == null)
            {
                m_DailyRewardsManager = GetComponent<DailyRewardsManager>();
            }
            
            m_DailyRewardsManager.DailyRewardsResultUpdated += UpdateRewardsList;
            m_DailyRewardsManager.ClaimedDailyReward += HandleRewardClaimed;
            
            InitializeListView();
        }
        
        private void InitializeListView()
        {
            var claimedBackgroundStyle = new StyleBackground(m_ClaimedRewardsBackgroundDark);
            var canClaimBackgroundStyle = new StyleBackground(m_ClaimRewardBackground);
            var claimedRewardIconBackgroundStyle = new StyleBackground(m_ClaimedRewardsIconBackground);
            var cantClaimIconStyle = new StyleBackground(m_CantClaimIcon);
            var cantClaimAmountStyle = new StyleBackground(m_CantClaimAmountIcon);
            var cantClaimRewardsBackgroundStyle = new StyleBackground(m_CantClaimRewardsBackground);
            var canClaimIconStyle = new StyleBackground(m_CanClaimIcon);
            var canClaimAmountStyle = new StyleBackground(m_CanClaimAmountIcon);
            var canClaimIconBackgroundStyle = new StyleBackground(m_ClaimRewardsIconBackground);
            
            m_ListView = m_Document.rootVisualElement.Q<ListView>("ClaimListView");
            m_ListView.style.display = DisplayStyle.Flex;

            if (m_ListView == null)
            {
                Logger.LogError("Failed to find ClaimListView in the UI Document");
                return;
            }
            m_ListView.fixedItemHeight = k_ItemHeight;
            m_ListView.itemsSource = m_RewardItems;
            
            m_ListView.style.justifyContent = Justify.Center;
            m_ListView.makeItem = () => m_ListView.itemTemplate.Instantiate();
                
            m_ListView.bindItem = (element, index) =>
            {
                if (index >= m_RewardItems.Count) return;
                
                var item = m_RewardItems[index];
                
                var dayClaimLabel = element.Q<Label>("DayClaimLabel");
                dayClaimLabel.text = $"Day {index + 1}";
                
                var rewardIcon = element.Q<VisualElement>("RewardIcon");
                var rewardAmountIcon = element.Q<VisualElement>("RewardAmountIcon");
                
                var rewardIconBackground = element.Q<VisualElement>("RewardIconBackground");
                
                var greenCheck = element.Q<VisualElement>("GreenCheck");
                var rewardsBackground = element.Q<VisualElement>("RewardsBackground");
                
                var claimButton = element.Q<Button>("ClaimRewardButton");
                if (claimButton != null)
                {
                    claimButton.SetEnabled(item.IsClaimable);
                    if (item.IsClaimed)
                    {
                        claimButton.style.display = DisplayStyle.None;
                        greenCheck.style.display = DisplayStyle.Flex;
                        
                        claimButton.text = "Claimed";
                        claimButton.SetEnabled(false);
                        
                        rewardIcon.style.backgroundImage = canClaimIconStyle;
                        rewardsBackground.style.backgroundImage = claimedBackgroundStyle;
                        rewardIconBackground.style.backgroundImage = claimedRewardIconBackgroundStyle;
                        rewardAmountIcon.style.backgroundImage = canClaimAmountStyle;
                    }
                    else if (item.IsClaimable && !item.IsClaimed)
                    {
                        claimButton.style.display = DisplayStyle.Flex;
                        greenCheck.style.display = DisplayStyle.None;
                        
                        claimButton.text = "Claim";
                        claimButton.SetEnabled(true);
                        
                        rewardIcon.style.backgroundImage = canClaimIconStyle;
                        rewardIconBackground.style.backgroundImage = canClaimIconBackgroundStyle;
                        rewardAmountIcon.style.backgroundImage = canClaimAmountStyle;
                        rewardsBackground.style.backgroundImage = canClaimBackgroundStyle;
                    }
                    // Can't yet claim
                    else if (!item.IsClaimed && !item.IsClaimable)
                    {
                        claimButton.style.display = DisplayStyle.Flex;
                        greenCheck.style.display = DisplayStyle.None;
                        
                        claimButton.text = "Locked";
                        claimButton.SetEnabled(false);

                        rewardsBackground.style.backgroundImage = cantClaimRewardsBackgroundStyle;
                        rewardIcon.style.backgroundImage = cantClaimIconStyle;
                        rewardIconBackground.style.backgroundImage = canClaimIconBackgroundStyle;
                        rewardAmountIcon.style.backgroundImage = cantClaimAmountStyle;
                    }
                    // Remove any existing click handlers to prevent duplicates
                    claimButton.clickable = new Clickable(() => ClaimDailyReward(index));
                }
                
                var rewardAmountLabel = element.Q<Label>("RewardLabel");
                rewardAmountLabel.text = item.Reward.Quantity.ToString();
            };
            
            m_ListView.Rebuild();
        }
        
        private void UpdateRewardsList(DailyRewardsResult status)
        {
            // Logger.Log($"Updating reward list with status: Success={status.Success}, " +
            //     $"DaysClaimed={status.DaysClaimed}, " +
            //     $"SecondsTillClaimable={status.SecondsTillClaimable}");

            m_RewardItems.Clear();
            
            if (status.ConfigData?.DailyRewards == null)
            {
                Logger.LogError("Daily rewards config is null!");
                return;
            }

            for (int i = 0; i < status.ConfigData.DailyRewards.Count; i++)
            {
                var reward = status.ConfigData.DailyRewards[i];
                // Logger.Log($"Processing reward {i}: {reward.Quantity}x {reward.Id}");

                var isClaimable = i == status.DaysClaimed && status.SecondsTillClaimable <= 0;
                var isClaimed = i < status.DaysClaimed;

                m_RewardItems.Add(new DailyRewardListViewModel
                {
                    DayNumber = i + 1,
                    Reward = reward,
                    IsClaimable = isClaimable,
                    IsClaimed = isClaimed
                });
            }
            
            m_ListView?.Rebuild();
        }

        private void HandleRewardClaimed(DailyRewardClaimEventArgs args)
        {
            int nextClaimableDay = args.DayIndex;
            if (nextClaimableDay >= m_RewardItems.Count)
            {
                Logger.LogWarning($"Current day {nextClaimableDay} is out of range for reward items count {m_RewardItems.Count}");
                return;
            }

            for (int i = 0; i < m_RewardItems.Count; i++)
            {
                var item = m_RewardItems[i];
                if (i < nextClaimableDay)
                {
                    // Previous days are claimed
                    item.IsClaimable = false;
                    item.IsClaimed = true;
                }
                else if (i == nextClaimableDay)
                {
                    // Current day is claimable
                    item.IsClaimable = true;
                    item.IsClaimed = false;
                }
                else
                {
                    // Future days are locked
                    item.IsClaimable = false;
                    item.IsClaimed = false;
                }
            }
            m_ListView?.Rebuild();
        }
        
        private void ClaimDailyReward(int index)
        {
            if (m_DailyRewardsClient != null)
            {
                m_DailyRewardsManager.ProcessRewardClaim();
            }
        }
        
        private void OnDisable()
        {
            m_DailyRewardsClient.FetchedDailyRewardsStatus -= UpdateRewardsList;
            m_DailyRewardsManager.ClaimedDailyReward -= HandleRewardClaimed;
        }
    }
    
    public class DailyRewardListViewModel
    {
        public int DayNumber { get; set; }
        public DailyReward Reward { get; set; }
        public bool IsClaimable { get; set; }
        public bool IsClaimed { get; set; }
    }
}
