using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples.DailyRewards
{
    public abstract class DayView : MonoBehaviour
    {
        public Image rewardIcon;
        public TextMeshProUGUI rewardQuantity;
        public Animator animator;

        public int dayIndex { get; protected set; }

        protected void UpdateStatus(DailyRewardsEventManager eventManager,
            DailyRewardsEventManager.DailyReward reward)
        {
            var dayStatus = eventManager.GetDayStatus(dayIndex);

            animator.SetBool("isClaimable", dayStatus == DailyRewardsEventManager.DayStatus.DayClaimable);
            animator.SetBool("isClaimed", dayStatus == DailyRewardsEventManager.DayStatus.DayClaimed);

            rewardIcon.sprite = EconomyManager.instance.GetSpriteForCurrencyId(reward.id);

            rewardQuantity.text = $"+{reward.quantity}";
        }

        public void SetUnclaimable()
        {
            animator.SetBool("isClaimable", false);
        }
    }
}
