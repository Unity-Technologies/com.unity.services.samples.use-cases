using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.UI;

namespace UnityGamingServicesUseCases
{
    namespace DailyRewards
    {
        public class DailyRewardsSceneManager : MonoBehaviour
        {
            public DailyRewardsSampleView sceneView;

            public Button openDailyRewardsButton;

            DailyRewardsEventManager eventManager;


            void Awake()
            {
                eventManager = GetComponent<DailyRewardsEventManager>();
            }

            async void Start()
            {
                try
                {
                    await UnityServices.InitializeAsync();

                    // Check that scene has not been unloaded while processing async wait to prevent throw.
                    if (this == null) return;

                    if (!AuthenticationService.Instance.IsSignedIn)
                    {
                        await AuthenticationService.Instance.SignInAnonymouslyAsync();
                        if (this == null) return;
                    }

                    Debug.Log($"Player id:{AuthenticationService.Instance.PlayerId}");

                    await Task.WhenAll(
                        EconomyManager.instance.InitializeCurrencySprites(),
                        EconomyManager.instance.RefreshCurrencyBalances(),
                        eventManager.RefreshDailyRewardsEventStatus());
                    if (this == null) return;

                    Debug.Log("Initialization and signin complete.");

                    if (eventManager.isEnded)
                    {
                        await eventManager.Demonstration_StartNextMonth();
                        if (this == null) return;
                    }

                    ShowStatus();

                    openDailyRewardsButton.interactable = true;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            void Update()
            {
                if (!eventManager.isEventReady)
                {
                    return;
                }

                // Only update if the event is actually active
                if (eventManager.isStarted && !eventManager.isEnded)
                {
                    // Request periodic update to update timers and start new day, if necessary.
                    if (eventManager.UpdateRewardsStatus(sceneView))
                    {
                        // Update call returned true to signal start of new day so full update is required.
                        sceneView.UpdateStatus(eventManager);
                    }
                    else
                    {
                        // Update call signaled that only timers require updating (new day did not begin yet).
                        sceneView.UpdateTimers(eventManager);
                    }
                }
            }

            void ShowStatus()
            {
                sceneView.UpdateStatus(eventManager);

                if (eventManager.firstVisit)
                {
                    eventManager.MarkFirstVisitComplete();
                }
            }

            public async void OnClaimButtonPressed()
            {
                try
                {
                    // Disable all claim buttons to prevent multiple collect requests.
                    // Button is reenabled when the state is refreshed after the claim has been fully processed.
                    sceneView.SetAllDaysUnclaimable();

                    await eventManager.ClaimDailyReward();
                    if (this == null) return;

                    ShowStatus();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            public async void OnOpenEventButtonPressed()
            {
                try
                {
                    if (!eventManager.isEventReady)
                    {
                        return;
                    }

                    if (eventManager.isEnded)
                    {
                        await eventManager.Demonstration_StartNextMonth();
                        if (this == null) return;
                    }

                    ShowStatus();
                 
                    sceneView.OpenEventWindow();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
    }
}   
