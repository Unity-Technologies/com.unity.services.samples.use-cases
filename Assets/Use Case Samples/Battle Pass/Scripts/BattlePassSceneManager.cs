using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace UnityGamingServicesUseCases
{
    namespace BattlePass
    {
        public class BattlePassSceneManager : MonoBehaviour
        {
            const int k_GainFreeGemsAmount = 30;
            const int k_PlayGamePointsAmount = 85;

            public BattlePassSampleView sceneView;
            public BattlePassView battlePassView;
            public TierPopupView tierPopupView;
            public CountdownView countdownView;

            bool m_Updating = false;
            float m_EventSecondsRemaining;

            public BattlePassConfig battlePassConfig { get; private set; }
            public BattlePassState battlePassState { get; private set; }

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

            void Update()
            {
                if (!m_Updating)
                {
                    m_EventSecondsRemaining -= Time.deltaTime;

                    if (m_EventSecondsRemaining <= 0)
                    {
                        m_EventSecondsRemaining = 0;

                        OnCountdownEnded();
                    }

                    countdownView.SetTotalSeconds(m_EventSecondsRemaining);
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

                    // Economy configuration should be refreshed every time the app initializes.
                    // Doing so updates the cached configuration data and initializes for this player any items or
                    // currencies that were recently published.
                    // 
                    // It's important to do this update before making any other calls to the Economy or Remote Config
                    // APIs as both use the cached data list. (Though it wouldn't be necessary to do if only using Remote
                    // Config in your project and not Economy.)
                    await EconomyManager.instance.RefreshEconomyConfiguration();
                    if (this == null) return;

                    await UpdateEconomyAndBattlePassProgress();
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

            async Task UpdateEconomyAndBattlePassProgress()
            {
                try
                {
                    Debug.Log("Getting updated Battle Pass configs and progress...");

                    await Task.WhenAll(EconomyManager.instance.RefreshCurrencyBalances(), GetBattlePassProgress());

                    if (this == null) return;

                    sceneView.UpdateWelcomeText(battlePassConfig.eventName);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            async Task GetBattlePassProgress()
            {
                var result = await CloudCodeManager.instance.CallGetProgressEndpoint();

                if (this == null) return;

                if (result.seasonTierStates == null) return;

                m_EventSecondsRemaining = result.eventSecondsRemaining;

                UpdateCachedBattlePassConfig(result.remoteConfigs);
                UpdateCachedBattlePassProgress(result.seasonXp, result.ownsBattlePass, result.seasonTierStates);

                battlePassView.Refresh(battlePassState);
            }

            public void OnTierButtonClicked(int tierIndex)
            {
                tierPopupView.Show(tierIndex);
            }

            public async void OnTierPopupClaimButtonClicked(int tierIndexToClaim)
            {
                try
                {
                    sceneView.SetInteractable(false);

                    var result = await CloudCodeManager.instance.CallClaimTierEndpoint(tierIndexToClaim);

                    if (this == null) return;

                    UpdateCachedBattlePassProgress(battlePassState.seasonXP, battlePassState.ownsBattlePass, result.seasonTierStates);

                    battlePassView.Refresh(battlePassState);

                    await EconomyManager.instance.RefreshCurrencyBalances();

                    if (this == null) return;
                }
                catch (CloudCodeResultUnavailableException)
                {
                    // Exception already handled by CloudCodeManager
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                sceneView.SetInteractable(true);
            }

            public async void OnGainGemsButtonPressed()
            {
                try
                {
                    sceneView.SetInteractable(false);

                    await EconomyManager.instance.GainCurrency("GEM", k_GainFreeGemsAmount);
                }
                catch (CloudCodeResultUnavailableException)
                {
                    // Exception already handled by CloudCodeManager
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                sceneView.SetInteractable(true);
            }

            public async void OnPlayGameButtonPressed()
            {
                try
                {
                    sceneView.SetInteractable(false);

                    var result = await CloudCodeManager.instance.CallGainSeasonXpEndpoint(k_PlayGamePointsAmount);

                    if (this == null) return;

                    UpdateCachedBattlePassProgress(result.seasonXp, battlePassState.ownsBattlePass,
                        result.seasonTierStates);

                    battlePassView.Refresh(battlePassState);
                }
                catch (CloudCodeResultUnavailableException)
                {
                    // Exception already handled by CloudCodeManager
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                sceneView.SetInteractable(true);
            }

            public async void OnBuyBattlePassButtonPressed()
            {
                try
                {
                    sceneView.SetInteractable(false);

                    var result = await CloudCodeManager.instance.CallPurchaseBattlePassEndpoint();

                    if (this == null) return;

                    UpdateCachedBattlePassProgress(battlePassState.seasonXP, true, result.seasonTierStates);

                    battlePassView.Refresh(battlePassState);

                    await EconomyManager.instance.RefreshCurrencyBalances();

                    if (this == null) return;
                }
                catch (CloudCodeResultUnavailableException)
                {
                    // Exception already handled by CloudCodeManager
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                sceneView.SetInteractable(true);
            }

            async void OnCountdownEnded()
            {
                try
                {
                    UpdateStarted();

                    tierPopupView.Close();

                    await UpdateEconomyAndBattlePassProgress();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
                finally
                {
                    UpdateFinished();
                }
            }

            void UpdateCachedBattlePassConfig(CloudCodeManager.GetStateRemoteConfigs remoteConfigs)
            {
                var freeTiersCount = remoteConfigs.battlePassRewardsFree.Length;
                var premiumTiersCount = remoteConfigs.battlePassRewardsPremium.Length;

                battlePassConfig = new BattlePassConfig
                {
                    eventName = remoteConfigs.eventName,
                    seasonXpPerTier = remoteConfigs.battlePassSeasonXpPerTier,
                    tierCount = freeTiersCount,
                    rewardsFree = new RewardDetail[freeTiersCount],
                    rewardsPremium = new RewardDetail[premiumTiersCount]
                };

                for (var i = 0; i < freeTiersCount; i++)
                {
                    battlePassConfig.rewardsFree[i] = new RewardDetail
                    {
                        id = remoteConfigs.battlePassRewardsFree[i].id,
                        quantity = remoteConfigs.battlePassRewardsFree[i].quantity,
                        spriteAddress = remoteConfigs.battlePassRewardsFree[i].spriteAddress
                    };
                }

                for (var i = 0; i < premiumTiersCount; i++)
                {
                    battlePassConfig.rewardsPremium[i] = new RewardDetail
                    {
                        id = remoteConfigs.battlePassRewardsPremium[i].id,
                        quantity = remoteConfigs.battlePassRewardsPremium[i].quantity,
                        spriteAddress = remoteConfigs.battlePassRewardsPremium[i].spriteAddress
                    };
                }
            }

            void UpdateCachedBattlePassProgress(int seasonXp, bool ownsBattlePass, int[] seasonTierStates)
            {
                if (battlePassState?.tierStates == null)
                {
                    battlePassState = new BattlePassState
                    {
                        tierStates = new TierState[seasonTierStates.Length]
                    };
                }

                battlePassState.seasonXP = seasonXp;
                battlePassState.ownsBattlePass = ownsBattlePass;

                for (var i = 0; i < seasonTierStates?.Length; i++)
                {
                    battlePassState.tierStates[i] = (TierState)seasonTierStates[i];
                }
            }
        }
    }
}
