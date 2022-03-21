using UnityEngine;

namespace UnityGamingServicesUseCases
{
    namespace DailyRewards
    {
        public class CalendarView : MonoBehaviour
        {
            CalendarDayView[] calendarDays;


            void Awake()
            {
                calendarDays = GetComponentsInChildren<CalendarDayView>();

                for (int dayOn = 0; dayOn < calendarDays.Length; dayOn++)
                { 
                    calendarDays[dayOn].SetDayIndex(dayOn + 1);
                }
            }

            public void UpdateStatus(DailyRewardsEventManager eventManager)
            {
                var daysClaimed = eventManager.daysClaimed;

                for (int dayOn = 0; dayOn < calendarDays.Length; dayOn++)
                {
                    var dayView = calendarDays[dayOn];

                    dayView.UpdateStatus(eventManager);
                }
            }

            public void SetUnclaimable()
            {
                for (int dayOn = 0; dayOn < calendarDays.Length; dayOn++)
                {
                    calendarDays[dayOn].SetUnclaimable();
                }
            }
        }
    }
}
