using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace UnityGamingServicesUseCases
{
    namespace RewardedAds
    {
        public class RewardedAdsSceneManager : MonoBehaviour
        {
            public RewardedAdsSampleView sceneView;
            public RewardedAdBoosterArrowManager rewardedAdBoosterArrow;

            public enum RewardedAdBoosterWedge
            {
                Left,
                LeftCenter,
                Center,
                RightCenter,
                Right
            }

            internal bool m_IsAdClosed;

            // In this sample, for simplicity, the rewards, multipliers, and frequency of rewarded ad booster
            // are hardcoded. Alternatively, you could use Remote Config to dynamically define these,
            // this would allow a single source of truth for the values used in both cloud and client code.
            // See CommandBatch or Seasonal Events samples for examples of how this could be done.
            const int k_BaseRewardAmount = 25;
            const int k_StandardRewardedAdMultiplier = 2;
            const int k_FrequencyOfRewardedAdBoosterOccurrence = 3;

            int m_LevelEndCount;
            int m_RewardedAdBoosterActiveMultiplier;
            bool m_EconomyHudUpdatedWhileWaiting;
            bool m_IsWaitingForRewardDistribution;

            Dictionary<RewardedAdBoosterWedge, int> m_RewardedAdBoosterWedgeMultipliers = 
                new Dictionary<RewardedAdBoosterWedge, int> 
                {
                    { RewardedAdBoosterWedge.Left, 2 },
                    { RewardedAdBoosterWedge.LeftCenter, 3 },
                    { RewardedAdBoosterWedge.Center, 5 },
                    { RewardedAdBoosterWedge.RightCenter, 3 },
                    { RewardedAdBoosterWedge.Right, 2 }
                };

            async void Start()
            {
                try
                {
                    sceneView.InitializeScene();
                    await InitializeUnityServices();

                    // Check that scene has not been unloaded while processing async wait to prevent throw.
                    if (this == null) return;

                    await FetchUpdatedServicesData();
                    if (this == null) return;

                    m_LevelEndCount = CloudSaveManager.instance.GetCachedLevelEndCount();
                    sceneView.SetInteractable(true);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            async Task InitializeUnityServices()
            {
                await UnityServices.InitializeAsync();

                if (this == null) return;

                Debug.Log("Services Initialized.");

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    Debug.Log("Signing in...");
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    if (this == null) return;
                }

                Debug.Log($"Player id: {AuthenticationService.Instance.PlayerId}");
            }

            async Task FetchUpdatedServicesData()
            {
                MediationManager.instance.LoadRewardedAd();
                await Task.WhenAll(
                    EconomyManager.instance.RefreshCurrencyBalances(),
                    CloudSaveManager.instance.LoadAndCacheData()
                );
            }

            public void OnCompleteLevelButtonPressed()
            {
                if (m_LevelEndCount % k_FrequencyOfRewardedAdBoosterOccurrence == 0)
                {
                    rewardedAdBoosterArrow.Start();

                    sceneView.ShowCompleteLevelPopupWithRewardedAdBooster(
                        m_RewardedAdBoosterWedgeMultipliers, k_BaseRewardAmount);
                }
                else
                {
                    sceneView.ShowCompleteLevelPopup(k_BaseRewardAmount * k_StandardRewardedAdMultiplier);
                }

                m_LevelEndCount++;
                sceneView.SetCompleteLevelButtonInteractable(false);
            }

            public async void OnClaimLevelEndRewardsButtonPressed()
            {
                try
                {
                    sceneView.SetInteractable(false);
                    await DistributeBaseRewards(false);
                    if (this == null) return;

                    sceneView.CloseCompleteLevelPopup();
                }
                catch (Exception e)
                {
                    Debug.Log("A problem occurred while trying to distribute level end rewards: " + e);
                }
                finally
                {
                    sceneView.SetInteractable(true);
                }
            }

            async Task DistributeBaseRewards(bool waitForSecondRewardDistribution)
            {
                await CloudCodeManager.instance.CallGrantLevelEndRewardsEndpoint(waitForSecondRewardDistribution);
            }

            public async void OnWatchRewardedAdButtonPressed()
            {
                try
                {
                    sceneView.SetInteractable(false);

                    // We'll distribute the base rewards now with no multiplier, then if players successfully complete
                    // watching the rewarded ad, MediationManager will tell Cloud Code to distribute the multiplier rewards.
                    await DistributeBaseRewards(true);
                    if (this == null) return;

                    MediationManager.instance.ShowAd(k_StandardRewardedAdMultiplier);
                    sceneView.CloseCompleteLevelPopup();
                }
                catch (Exception e)
                {
                    Debug.Log("A problem occurred while trying to show level end rewarded ad: " + e);
                }
                finally
                {
                    sceneView.SetInteractable(true);
                }
            }

            public async void OnRewardedAdBoosterWatchAdButtonPressed()
            {
                try
                {
                    sceneView.SetInteractable(false);
                    rewardedAdBoosterArrow.Stop();

                    // Pause for one second so player can see where the rewarded ad booster's arrow stopped.
                    await Task.Delay(1000);
                    if (this == null) return;

                    // We'll distribute the base rewards now with no multiplier, then if players successfully complete
                    // watching the rewarded ad, MediationManager will tell Cloud Code to distribute the multiplier rewards.
                    await DistributeBaseRewards(true);
                    if (this == null) return;

                    MediationManager.instance.ShowAd(m_RewardedAdBoosterActiveMultiplier);
                    sceneView.CloseCompleteLevelPopup();
                }
                catch (Exception e)
                {
                    Debug.Log("A problem occurred while trying to show ad after rewarded ad booster: " + e);
                }
                finally
                {
                    sceneView.SetInteractable(true);
                }
            }

            public void UpdateEconomyHudWhenAppropriate(bool waitForSecondRewardDistribution, string rewardId, 
                int rewardBalance)
            {
                if (waitForSecondRewardDistribution)
                {
                    // waitForSecondRewardDistribution is true when base rewards have been distributed before showing
                    // a rewarded ad. In that case we don't want to update the HUD until we know whether or not the
                    // rewarded ad has been successfully watched and the second distribution of rewards completed.

                    StartCoroutine(WaitForSecondDistributionAndUpdateHudIfNone(rewardId, rewardBalance));
                }
                else
                {
                    // waitForSecondRewardDistribution will be false if no second reward distribution is expected or
                    // this is the second reward distribution. In this case we update the HUD and, if a path is waiting
                    // for this reward distribution, we change m_EconomyHudUpdatedWhileWaiting to indicate completion.

                    UpdateEconomyHud(rewardId, rewardBalance);

                    if (m_IsWaitingForRewardDistribution)
                    {
                        m_EconomyHudUpdatedWhileWaiting = true;
                    }
                }
            }

            IEnumerator WaitForSecondDistributionAndUpdateHudIfNone(string rewardId, int rewardBalance)
            {
                m_EconomyHudUpdatedWhileWaiting = false;
                yield return WaitForAdCompletion();

                if (!m_EconomyHudUpdatedWhileWaiting)
                {
                    // the Rewarded Ad we were waiting for was not successfully completed so we will update the
                    // HUD with the reward balance that Cloud Code returned when we distributed the base rewards.
                    UpdateEconomyHud(rewardId, rewardBalance);
                }
            }

            IEnumerator WaitForAdCompletion()
            {
                m_IsAdClosed = false;
                yield return new WaitUntil(() => m_IsAdClosed);
                m_IsWaitingForRewardDistribution = true;

                // Wait one additional second to give time for MediationManager.OnUserRewarded to call
                // CloudCode.GrantLevelEndRewardsEndpoint, and for the Cloud Code script to complete it's work. This is
                // risky because it relies on Cloud Code not experiencing delays in execution for it to finish on time.
                // To reduce the risk one could increase the number of seconds it waits for, but this will also increase
                // how long players wait to see their rewards updated.
                yield return new WaitForSeconds(1);
                m_IsWaitingForRewardDistribution = false;
            }

            void UpdateEconomyHud(string rewardId, int rewardBalance)
            {
                EconomyManager.instance.SetCurrencyBalance(rewardId, rewardBalance);
            }

            public void ChangeRewardedAdBoosterMultiplier(RewardedAdBoosterWedge newActiveSection)
            {
                m_RewardedAdBoosterActiveMultiplier = m_RewardedAdBoosterWedgeMultipliers[newActiveSection];
                sceneView.ChangeRewardedAdBoosterClaimRewardAmount(
                    k_BaseRewardAmount * m_RewardedAdBoosterActiveMultiplier);
            }
        }
    }
}
