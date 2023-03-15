using System;
using TMPro;
using UnityEngine;

namespace Unity.Services.Samples.DailyRewards
{
    public class DailyRewardsSampleView : MonoBehaviour
    {
        public Canvas dailyRewardsWindowCanvas;

        public GameObject endedEventGameObject;

        public TextMeshProUGUI daysLeftText;
        public TextMeshProUGUI comeBackInText;
        public TextMeshProUGUI secondsPerDayText;

        public CalendarView calendar;
        public BonusDayView bonusDay;

        public void UpdateStatus(DailyRewardsEventManager eventManager)
        {
            calendar.UpdateStatus(eventManager);
            bonusDay.UpdateStatus(eventManager);

            UpdateTimers(eventManager);
        }

        public void UpdateTimers(DailyRewardsEventManager eventManager)
        {
            secondsPerDayText.text = $"1 day = {eventManager.secondsPerDay} sec";

            if (eventManager.daysRemaining <= 0)
            {
                endedEventGameObject.SetActive(true);
                daysLeftText.text = "Days Left: 0";
                comeBackInText.text = "Event Over";
            }
            else
            {
                endedEventGameObject.SetActive(false);
                daysLeftText.text = $"Days Left: {eventManager.daysRemaining}";
                if (eventManager.secondsTillClaimable > 0)
                {
                    comeBackInText.text = $"Come Back in: {eventManager.secondsTillClaimable:0.0} seconds";
                }
                else
                {
                    comeBackInText.text = "Claim Now!";
                }
            }
        }

        public void OpenEventWindow()
        {
            dailyRewardsWindowCanvas.enabled = true;
        }

        public void OnCloseEventButtonPressed()
        {
            dailyRewardsWindowCanvas.enabled = false;
        }

        public void SetAllDaysUnclaimable()
        {
            calendar.SetUnclaimable();
            bonusDay.SetUnclaimable();
        }
    }
}
