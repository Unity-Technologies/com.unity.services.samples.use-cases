using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace GameOperationsSamples
{
    namespace ABTestLevelDifficulty
    {
        public class ABTestLevelDifficultySceneManager : MonoBehaviour
        {
            public ABTestLevelDifficultySampleView sceneView;

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
                CloudCodeManager.LeveledUp += OpenLeveledUpPopup;
                RemoteConfigManager.ConfigValuesUpdated += OnRemoteConfigValuesUpdated;
            }

            void StopSubscribe()
            {
                CloudCodeManager.LeveledUp -= OpenLeveledUpPopup;
                RemoteConfigManager.ConfigValuesUpdated -= OnRemoteConfigValuesUpdated;
            }

            async void Start()
            {
                await InitializeServices();

                // Check that scene has not been unloaded while processing async wait to prevent throw.
                if (this == null) return;

                SignIn();
            }

            async Task InitializeServices()
            {
                await UnityServices.InitializeAsync();
                if (this == null) return;

                RemoteConfigManager.instance.StartSubscribe();
            }

            public async void SignInAsNewUser()
            {
                await SignOut();
                if (this == null) return;

                SignIn();
            }

            async Task SignOut()
            {
                // Note that signing out here signs you out of this player ID across all the use case samples.
                if (AuthenticationService.Instance.IsSignedIn)
                {
                    Debug.Log("Signing out current player...");
                    CloudSaveManager.instance.ClearCachedData();
                    RemoteConfigManager.instance.ClearCachedData();

                    // The ClearCachedView method in EconomyManager is getting the list of Currencies to be cleared
                    // from the server, so we must call that method before signing out in the Authentication service
                    await EconomyManager.instance.ClearCachedView();
                    if (this == null) return;

                    AuthenticationService.Instance.SignOut();
                    sceneView.OnSignedOut();
                    sceneView.UpdateScene();
                }
            }

            async void SignIn()
            {
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    Debug.Log("Signing in...");
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    if (this == null) return;
                    
                    Debug.Log($"Player id: {AuthenticationService.Instance.PlayerId}");

                    await CloudSaveManager.instance.LoadAndCacheData();
                    if (this == null) return;

                    await EconomyManager.instance.GetUpdatedBalances();
                    if (this == null) return;

                    // Remote Config Fetch Configs is not awaitable, and instead triggers an event.
                    // Our RemoteConfigManager will handle that event and trigger a ConfigValuesUpdated event when
                    // it's done saving the newly fetched configs.
                    RemoteConfigManager.instance.FetchConfigsIfServicesAreInitialized();
                }
            }

            void OnRemoteConfigValuesUpdated()
            {
                // We only fetch Remote Config values at sign-in for this implementation. So, we know that when the
                // ConfigValuesUpdated event invokes this method, the last step for updating the scene to its
                // signed-in state has been completed.
                sceneView.OnSignedIn();
                sceneView.EnableAndUpdate();
            }

            public async void GainXP()
            {
                await CloudCodeManager.instance.CallGainXPAndLevelIfReadyEndpoint();
                if (this == null) return;

                sceneView.UpdateScene();
            }

            public void OpenLeveledUpPopup()
            {
                sceneView.DisableAndUpdate();
                sceneView.OpenLevelUpPopup();
            }

            public void CloseLeveledUpPopup()
            {
                sceneView.CloseLevelUpPopup();
                sceneView.EnableAndUpdate();
            }
        }
    }
}
