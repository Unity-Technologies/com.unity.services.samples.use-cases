using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.Services.Samples.DailyRewards
{
    public class DailyRewardsEventManager : MonoBehaviour
    {
        public enum DayStatus
        {
            DayClaimed,
            DayClaimable,
            DayUnclaimable
        }

        GetStatusResult m_Status;

        public bool isEventReady => m_Status.success;

        public bool firstVisit => m_Status.firstVisit;

        public int daysClaimed => m_Status.daysClaimed;

        public int daysRemaining => m_Status.daysRemaining;

        public int totalCalendarDays => m_Status.dailyRewards.Count;

        public bool isStarted => m_Status.isStarted;

        public bool isEnded => m_Status.isEnded;

        public float secondsTillClaimable => m_Status.secondsTillClaimable;

        public bool isClaimableNow => secondsTillClaimable <= 0 && !isEnded;

        public float secondsTillNextDay => m_Status.secondsTillNextDay;

        public float secondsPerDay => m_Status.secondsPerDay;

        public List<DailyReward> GetDailyRewards(int dayIndex) => m_Status.dailyRewards[dayIndex];

        public List<DailyReward> bonusReward => m_Status.bonusReward;

        public List<DailyReward> rewardsGranted { get; private set; }

        float lastUpdateTime;

        public async Task RefreshDailyRewardsEventStatus()
        {
            try
            {
                m_Status = await CloudCodeManager.instance.CallGetStatusEndpoint();
                if (this == null) return;

                lastUpdateTime = Time.time;

                Debug.Log($"Daily Reward Status: {m_Status}");
            }
            catch (CloudCodeResultUnavailableException)
            {
                // Exception already handled by CloudCodeManager
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public bool UpdateRewardsStatus(DailyRewardsSampleView sceneView)
        {
            var time = Time.time;
            var timePassed = time - lastUpdateTime;
            lastUpdateTime = time;

            m_Status.secondsTillNextDay -= timePassed;

            // If it's time to start the next day.
            if (m_Status.secondsTillNextDay <= 0)
            {
                m_Status.secondsTillNextDay += m_Status.secondsPerDay;

                // If next day was not claimable then it is now.
                // Note: This 'if' also prevents us decrementing the daysRemaining field since it was
                //       already decremented when we claimed the previous day.
                if (m_Status.secondsTillClaimable > 0)
                {
                    m_Status.secondsTillClaimable = 0;
                }

                // If next day was already claimable then reduce number of days remaining.
                else
                {
                    m_Status.daysRemaining--;

                    // If out of days then event is over.
                    if (m_Status.daysRemaining <= 0)
                    {
                        m_Status.daysRemaining = 0;

                        m_Status.isEnded = true;
                    }
                }

                // True signals Scene Manager that a full refresh is required
                return true;
            }

            // If current day is not claimable update when it will be claimable (i.e. start of next day).
            if (m_Status.secondsTillClaimable > 0)
            {
                m_Status.secondsTillClaimable = secondsTillNextDay;
            }

            // False signals Scene Manager that only timers need to be updated.
            return false;
        }

        // Helper method for demonstrating Daily Rewards. Normally this would be set manually to the beginning of
        // the month on Remote Config so all users experience the event over the course of a month, but here we reset
        // it manually to demonstrate how Daily Rewards works.
        public async Task Demonstration_StartNextMonth()
        {
            try
            {
                // Call Cloud Code script to reset the event so it will begin immediately
                await CloudCodeManager.instance.CallResetEventEndpoint();
                if (this == null) return;

                Debug.Log("Daily Reward reset. Retrieving updated status.");

                await RefreshDailyRewardsEventStatus();
            }
            catch (CloudCodeResultUnavailableException)
            {
                // Exception already handled by CloudCodeManager
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public async Task ClaimDailyReward()
        {
            try
            {
                Debug.Log("Calling Cloud Code 'DailyRewards_Claim' to claim the Daily Reward.");

                var claimResult = await CloudCodeManager.instance.CallClaimEndpoint();
                if (this == null) return;

                lastUpdateTime = Time.time;

                // Save updated status so we can return it later, when requested
                SaveStatus(claimResult);

                if (claimResult.rewardsGranted != null)
                {
                    Debug.Log("CloudCode script rewarded: " +
                        $"{string.Join(",", claimResult.rewardsGranted.Select(rewardGranted => rewardGranted.ToString()).ToArray())}.");
                }

                Debug.Log($"Claim Result: {claimResult}");

                await EconomyManager.instance.RefreshCurrencyBalances();
            }
            catch (CloudCodeResultUnavailableException)
            {
                // Exception already handled by CloudCodeManager
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public void MarkFirstVisitComplete()
        {
            m_Status.firstVisit = false;
        }

        public DayStatus GetDayStatus(int dayIndex)
        {
            // If day has already been claimed
            if (dayIndex <= daysClaimed)
            {
                // If day is the bonus day, and it's available again then return 'claimable'
                if (dayIndex > totalCalendarDays && isClaimableNow)
                {
                    return DayStatus.DayClaimable;
                }

                // This day has already been claimed
                return DayStatus.DayClaimed;
            }

            if (dayIndex > daysClaimed + 1)
            {
                return DayStatus.DayUnclaimable;
            }

            return isClaimableNow ? DayStatus.DayClaimable : DayStatus.DayUnclaimable;
        }

        void SaveStatus(ClaimResult claimResult)
        {
            m_Status.success = claimResult.success;
            m_Status.firstVisit = claimResult.firstVisit;
            m_Status.daysClaimed = claimResult.daysClaimed;
            m_Status.daysRemaining = claimResult.daysRemaining;
            m_Status.totalDays = claimResult.totalDays;
            m_Status.isStarted = claimResult.isStarted;
            m_Status.isEnded = claimResult.isEnded;
            m_Status.secondsTillClaimable = claimResult.secondsTillClaimable;
            m_Status.secondsTillNextDay = claimResult.secondsTillNextDay;
            m_Status.secondsPerDay = claimResult.secondsPerDay;
            m_Status.dailyRewards = claimResult.dailyRewards;
            m_Status.bonusReward = claimResult.bonusReward;

            rewardsGranted = claimResult.rewardsGranted;
        }

        // Struct matches response from the Cloud Code get-status call to retrieve the current state of
        // Daily Rewards event, including days claimed/left, time till next day, rewards, etc.
        public struct GetStatusResult
        {
            public bool success;
            public bool firstVisit;
            public int daysClaimed;
            public int daysRemaining;
            public int totalDays;
            public bool isStarted;
            public bool isEnded;
            public float secondsTillClaimable;
            public float secondsTillNextDay;
            public float secondsPerDay;
            public List<List<DailyReward>> dailyRewards;
            public List<DailyReward> bonusReward;

            public override string ToString()
            {
                return $"success:{success} days:{daysClaimed}/{totalDays} started:{isStarted} " +
                    $"ended:{isEnded} seconds:{secondsTillClaimable:0.#}/{secondsTillNextDay:0.#} " +
                    $"dailyRewardsCount:{dailyRewards.Count} " +
                    $"bonusRewards:{string.Join(",", bonusReward.Select(reward => reward.ToString()).ToArray())}";
            }
        }

        // Struct matches response from the Cloud Code claim-reward call to receive the list of currencies granted.
        // Note: This response contains all the current state information from the above structure, and also
        //       contains the rewards granted by the Daily Rewards claim script.
        public struct ClaimResult
        {
            public bool success;
            public bool firstVisit;
            public int daysClaimed;
            public int daysRemaining;
            public int totalDays;
            public bool isStarted;
            public bool isEnded;
            public float secondsTillClaimable;
            public float secondsTillNextDay;
            public float secondsPerDay;
            public List<List<DailyReward>> dailyRewards;
            public List<DailyReward> bonusReward;
            public List<DailyReward> rewardsGranted;

            public override string ToString()
            {
                var outputString = $"success:{success} days:{daysClaimed}/{totalDays} started:{isStarted} " +
                    $"ended:{isEnded} seconds:{secondsTillClaimable:0.#}/{secondsTillNextDay:0.#} " +
                    $"dailyRewardsCount:{dailyRewards.Count} " +
                    $"bonusRewards:{string.Join(",", bonusReward.Select(reward => reward.ToString()).ToArray())}";

                if (rewardsGranted == null)
                {
                    return outputString;
                }

                return outputString + $" rewardsGranted:{string.Join(",", rewardsGranted.Select(reward => reward.ToString()).ToArray())}";
            }
        }

        // Struct used to receive the result of the Daily Reward from Cloud Code
        public struct DailyReward
        {
            public string id;
            public int quantity;

            public override string ToString()
            {
                return $"({id} {quantity})";
            }
        }
    }
}
