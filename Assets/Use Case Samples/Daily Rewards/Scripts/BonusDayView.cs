using System;

namespace Unity.Services.Samples.DailyRewards
{
    public class BonusDayView : DayView
    {
        public void UpdateStatus(DailyRewardsEventManager eventManager)
        {
            // Set up the day index so DayView can properly show the claimed/claimable/unclaimable status.
            dayIndex = eventManager.totalCalendarDays + 1;

            // To keep this use case simple, we only use 1 reward, but this could be expanded
            // to iterate the array of rewards and show and/or grant them all, if desired.
            var reward = eventManager.bonusReward[0];

            // Call base class to handle common status updating such as Currency sprite.
            UpdateStatus(eventManager, reward);
        }
    }
}
