using System;
using TMPro;

namespace Unity.Services.Samples.DailyRewards
{
    public class CalendarDayView : DayView
    {
        public TextMeshProUGUI dayText;

        public void SetDayIndex(int dayIndex)
        {
            this.dayIndex = dayIndex;

            dayText.text = $"Day {dayIndex}";
        }

        public void UpdateStatus(DailyRewardsEventManager eventManager)
        {
            // To keep this use case simple, we only use 1 reward, but this could be expanded
            // to iterate the array of rewards and show and/or grant them all, if desired.
            var reward = eventManager.GetDailyRewards(dayIndex - 1)[0];

            // Call base class to handle common status updating such as Currency sprite.
            UpdateStatus(eventManager, reward);
        }
    }
}
