using System;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace SeasonalEvents
{
    public class SeasonalEventsSceneManager : MonoBehaviour
    {
        public SceneView sceneView;

        bool m_Updating = false;

        AsyncOperationHandle<Sprite> m_BackgroundImageHandle;

        void Start()
        {
            InitializeServices();
        }

        void OnEnable()
        {
            StartSubscribe();
        }

        private void OnDisable()
        {
            StopSubscribe();
        }

        void StartSubscribe()
        {
            RemoteConfigManager.RemoteConfigValuesUpdated += LoadSeasonalAddressableFiles;
            RemoteConfigManager.RemoteConfigFetchConfigAborted += UpdateFinished;
        }

        void StopSubscribe()
        {
            RemoteConfigManager.RemoteConfigValuesUpdated -= LoadSeasonalAddressableFiles;
            RemoteConfigManager.RemoteConfigFetchConfigAborted -= UpdateFinished;
        }

        async void InitializeServices()
        {
            UpdateStarted();

            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
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
                // Because our events are time based and change so rapidly (every 2 - 3 minutes) we will check each
                // update if it's time to refresh Remote Config's local data, and actually refresh it if the current
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

        void LoadSeasonalAddressableFiles()
        {
            if (m_BackgroundImageHandle.IsValid())
            {
                // This method is only called when the season has changed, so since we're done with the last season's
                // image we'll release the Async handle to it, before loading next season's image.
                Addressables.Release(m_BackgroundImageHandle);
            }

            m_BackgroundImageHandle = Addressables.LoadAssetAsync<Sprite>(RemoteConfigManager.instance.activeEventKey);
            m_BackgroundImageHandle.Completed += handle =>
            {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    sceneView.UpdateBackgroundImage(handle.Result);
                }
                else
                {
                    Debug.Log($"A sprite could not be found for the label " +
                              $"{RemoteConfigManager.instance.activeEventKey}." +
                              $" Addressables exception: {handle.OperationException}");
                }
                UpdateFinished();
            };
        }

        public void PlayChallenge()
        {
            var rewardPopup = sceneView.InstantiateRewardPopup();
            rewardPopup.Show(RemoteConfigManager.instance.challengeRewards);
        }
    }
}
