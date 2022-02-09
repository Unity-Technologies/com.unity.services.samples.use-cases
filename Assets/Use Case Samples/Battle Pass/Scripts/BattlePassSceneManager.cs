using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace GameOperationsSamples
{
    namespace BattlePass
    {
        public class BattlePassSceneManager : MonoBehaviour
        {
            public BattlePassSampleView sceneView;
            public BattlePassView battlePassView;
            public TierPopupView tierPopupView;
            public CountdownManager countdownManager;

            bool m_Updating = false;

            public BattlePassProgress battlePassProgress { get; private set; }

            async void Start()
            {
                try
                {
                    await InitializeServices();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            async void LateUpdate()
            {
                try
                {
                    if (!m_Updating)
                    {
                        await UpdateSeason();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            async Task InitializeServices()
            {
                UpdateStarted();

                try
                {
                    await UnityServices.InitializeAsync();

                    // Check that scene has not been unloaded while processing async wait to prevent throw.
                    if (this == null) return;
                    
                    Debug.Log("Services Initialized.");

                    if (!AuthenticationService.Instance.IsSignedIn)
                    {
                        Debug.Log("Signing in...");
                        await AuthenticationService.Instance.SignInAnonymouslyAsync();
                        if (this == null) return;
                    }
                    Debug.Log($"Player id: {AuthenticationService.Instance.PlayerId}");

                    await Task.WhenAll(
                        EconomyManager.instance.RefreshCurrencyBalances(),
                        GetRemoteConfigUpdates(),
                        GetBattlePassProgress());
                }
                finally
                {
                    // The finally statement will attempt to execute no matter what happens in the try block,
                    // so our check for whether the scene has been unloaded while processing the last async wait
                    // of the try block has to happen in the finally. Since we can't exit a finally block early
                    // we will only call UpdateFinished if the scene hasn't been unloaded.
                    if (this != null)
                    {
                        UpdateFinished();
                    }
                }
            }

            void UpdateStarted()
            {
                m_Updating = true;
                sceneView.SetInteractable(false);
            }

            void UpdateFinished()
            {
                m_Updating = false;
                sceneView.SetInteractable(true);
            }

            async Task GetRemoteConfigUpdates()
            {
                await RemoteConfigManager.instance.FetchConfigs();
                if (this == null) return;
                UpdateSeasonView();
            }

            void UpdateSeasonView()
            {
                sceneView.UpdateWelcomeText();
                countdownManager.StartCountdownFromNow();
            }

            async Task GetBattlePassProgress()
            {
                var result = await CloudCodeManager.instance.CallGetProgressEndpoint();

                if (this == null) return;

                if (result.seasonTierStates == null) return;

                UpdateCachedBattlePassProgress(result.seasonXp, result.ownsBattlePass, result.seasonTierStates);

                battlePassView.Refresh(battlePassProgress);
            }

            public void OnTierButtonClicked(int tierIndex)
            {
                tierPopupView.Show(tierIndex);
            }

            public async void OnTierPopupClaimButtonClicked(int tierIndexToClaim)
            {
                sceneView.SetInteractable(false);

                var result = await CloudCodeManager.instance.CallClaimTierEndpoint(tierIndexToClaim);

                if (this == null) return;

                if (result.validationResult == "valid")
                {
                    UpdateCachedBattlePassProgress(battlePassProgress.seasonXP, battlePassProgress.ownsBattlePass, result.seasonTierStates);

                    battlePassView.Refresh(battlePassProgress);

                    await EconomyManager.instance.RefreshCurrencyBalances();

                    if (this == null) return;

                    battlePassView.Refresh(battlePassProgress);
                }
                else
                {
                    Debug.LogWarning($"Battle Pass purchase was not successful. Reason given: {result.validationResult}");
                }

                sceneView.SetInteractable(true);
            }

            public async void OnGainGemsButtonPressed()
            {
                sceneView.SetInteractable(false);

                await EconomyManager.instance.GainCurrency("GEM", 30);

                sceneView.SetInteractable(true);
            }

            public async void OnPlayGameButtonPressed()
            {
                sceneView.SetInteractable(false);

                var result = await CloudCodeManager.instance.CallGainSeasonXpEndpoint(85);

                if (this == null) return;

                UpdateCachedBattlePassProgress(result.seasonXp, battlePassProgress.ownsBattlePass, result.seasonTierStates);

                battlePassView.Refresh(battlePassProgress);

                sceneView.SetInteractable(true);
            }

            public async void OnBuyBattlePassButtonPressed()
            {
                sceneView.SetInteractable(false);

                var result = await CloudCodeManager.instance.CallPurchaseBattlePassEndpoint();

                if (this == null) return;

                if (result.purchaseResult == "success")
                {
                    UpdateCachedBattlePassProgress(battlePassProgress.seasonXP, true, result.seasonTierStates);
                    battlePassView.Refresh(battlePassProgress);
                }
                else
                {
                    Debug.LogWarning($"Battle Pass purchase was not successful. Reason given: {result.purchaseResult}");
                }

                await EconomyManager.instance.RefreshCurrencyBalances();

                if (this == null) return;

                battlePassView.Refresh(battlePassProgress);

                sceneView.SetInteractable(true);
            }

            void UpdateCachedBattlePassProgress(int seasonXp, bool ownsBattlePass, int[] seasonTierStates)
            {
                if (battlePassProgress?.tierStates == null)
                {
                    battlePassProgress = new BattlePassProgress
                    {
                        tierStates = new TierState[seasonTierStates.Length]
                    };
                }

                battlePassProgress.seasonXP = seasonXp;
                battlePassProgress.ownsBattlePass = ownsBattlePass;

                for (var i = 0; i < seasonTierStates?.Length; i++)
                {
                    battlePassProgress.tierStates[i] = (TierState)seasonTierStates[i];
                }
            }

            async Task UpdateSeason()
            {
                // Because our events are time-based and change so rapidly (every 2 - 3 minutes), we will check each
                // update if it's time to refresh Remote Config's local data, and refresh it if the current
                // last digit of the minutes equals the start of the next campaign time (See more info in the comments
                // in GetUserAttributes). More typically you would probably fetch new configs at app launch and under
                // other less frequent circumstances.
                var currentMinuteLastDigit = DateTime.Now.Minute % 10;

                if (currentMinuteLastDigit > RemoteConfigManager.instance.activeEventEndTime
                    || currentMinuteLastDigit == 0 && RemoteConfigManager.instance.activeEventEndTime == 9)
                {
                    tierPopupView.Close();

                    try
                    {
                        UpdateStarted();

                        Debug.Log(
                            "Getting next season from Remote Config and refreshing progress data...");

                        await Task.WhenAll(
                            EconomyManager.instance.RefreshCurrencyBalances(),
                            GetRemoteConfigUpdates(),
                            GetBattlePassProgress());
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                    finally
                    {
                        if (this != null)
                        {
                            UpdateFinished();
                        }
                    }
                }
            }
        }
    }
}
