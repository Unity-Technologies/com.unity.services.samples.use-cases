using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace GameOperationsSamples
{
    namespace SeasonalEvents
    {
        public class SeasonalEventsSceneManager : MonoBehaviour
        {
            public SeasonalEventsSampleView sceneView;

            bool m_Updating = false;

            AsyncOperationHandle<IList<Sprite>> m_BackgroundImageHandle;
            AsyncOperationHandle<IList<GameObject>> m_PlayButtonPrefabHandle;
            AsyncOperationHandle<IList<GameObject>> m_PlayChallengeButtonPrefabHandle;

            void Start()
            {
                InitializeServices();
            }

            void OnEnable()
            {
                StartSubscribe();
            }

            void OnDisable()
            {
                StopSubscribe();
            }

            void StartSubscribe()
            {
                RemoteConfigManager.RemoteConfigValuesUpdated += OnRemoteConfigValuesUpdated;
                RemoteConfigManager.RemoteConfigFetchConfigAborted += UpdateFinished;
            }

            void StopSubscribe()
            {
                RemoteConfigManager.RemoteConfigValuesUpdated -= OnRemoteConfigValuesUpdated;
                RemoteConfigManager.RemoteConfigFetchConfigAborted -= UpdateFinished;
            }

            async void InitializeServices()
            {
                UpdateStarted();

                await UnityServices.InitializeAsync();

                // Check that scene has not been unloaded while processing async wait to prevent throw.
                if (this == null) return;

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    if (this == null) return;
                }
                EconomyManager.instance.GetUpdatedBalances();
                RemoteConfigManager.instance.StartSubscribe();
                RemoteConfigManager.instance.FetchConfigsIfServicesAreInitialized();
            }

            void UpdateStarted()
            {
                m_Updating = true;
                sceneView.Disable();
            }

            void UpdateFinished()
            {
                m_Updating = false;
                sceneView.Enable();
            }

            void LateUpdate()
            {
                if (!m_Updating)
                {
                    // Because our events are time-based and change so rapidly (every 2 - 3 minutes), we will check each
                    // update if it's time to refresh Remote Config's local data, and refresh it if the current
                    // last digit of the minutes equals the start of the next campaign time (See more info in the comments
                    // in GetUserAttributes). More typically you would probably fetch new configs at app launch and under
                    // other less frequent circumstances.
                    var currentMinuteLastDigit = DateTime.Now.Minute % 10;

                    if (currentMinuteLastDigit > RemoteConfigManager.instance.activeEventEndTime ||
                        (currentMinuteLastDigit == 0 && RemoteConfigManager.instance.activeEventEndTime == 9))
                    {
                        UpdateStarted();
                        RemoteConfigManager.instance.FetchConfigsIfServicesAreInitialized();
                    }
                }
            }

            async void OnRemoteConfigValuesUpdated()
            {
                // This method is only called when the season has changed. Since we're done with the last season's
                // assets, we'll release the Async handles to them before loading next season's assets.
                ReleaseHandlesIfValid();

                await LoadSeasonalAddressables();
                if (this == null) return;

                UpdateFinished();
            }

            void ReleaseHandlesIfValid()
            {
                if (m_BackgroundImageHandle.IsValid())
                {
                    Addressables.Release(m_BackgroundImageHandle);
                }

                if (m_PlayButtonPrefabHandle.IsValid())
                {
                    Addressables.Release(m_PlayButtonPrefabHandle);
                }

                if (m_PlayChallengeButtonPrefabHandle.IsValid())
                {
                    Addressables.Release(m_PlayChallengeButtonPrefabHandle);
                }
            }

            async Task LoadSeasonalAddressables()
            {
                m_BackgroundImageHandle = Addressables.LoadAssetsAsync<Sprite>(
                    new List<string>{ RemoteConfigManager.instance.activeEventKey, "Sprites/BackgroundImage" },
                    LoadSeasonalBackgroundImage,
                    Addressables.MergeMode.Intersection
                );
                m_PlayButtonPrefabHandle = Addressables.LoadAssetsAsync<GameObject>(
                    new List<string>{ RemoteConfigManager.instance.activeEventKey, "Prefabs/PlayButton" },
                    LoadSeasonalPlayButton,
                    Addressables.MergeMode.Intersection
                );
                m_PlayChallengeButtonPrefabHandle = Addressables.LoadAssetsAsync<GameObject>(
                    new List<string>{ RemoteConfigManager.instance.activeEventKey, "Prefabs/PlayChallengeButton" },
                    LoadSeasonalPlayChallengeButton,
                    Addressables.MergeMode.Intersection
                );

                await Task.WhenAll(m_BackgroundImageHandle.Task, 
                    m_PlayButtonPrefabHandle.Task, 
                    m_PlayChallengeButtonPrefabHandle.Task);
            }

            void LoadSeasonalBackgroundImage(Sprite backgroundImage)
            {
                sceneView.UpdateBackgroundImage(backgroundImage);
            }
        
            void LoadSeasonalPlayButton(GameObject playButtonPrefab)
            {
                sceneView.UpdatePlayButton(playButtonPrefab);
            }

            void LoadSeasonalPlayChallengeButton(GameObject playChallengeButtonPrefab)
            {
                sceneView.UpdatePlayChallengeButton(playChallengeButtonPrefab);
                sceneView.playChallengeButton.onClick.AddListener(PlayChallenge);
            }

            public void PlayChallenge()
            {
                var rewardPopup = sceneView.InstantiateRewardPopup();
                rewardPopup.Show(RemoteConfigManager.instance.challengeRewards);
            }
        }
    }
}
