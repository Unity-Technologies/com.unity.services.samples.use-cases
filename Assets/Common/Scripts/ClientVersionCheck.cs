using System;
using System.Threading.Tasks;
using Unity.RemoteConfig;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace GameOperationsSamples
{
    public class ClientVersionCheck : MonoBehaviour
    {
        static bool s_VersionWasChecked;

        const string k_NewerMinimumVersionMessage =
            "This working copy of the Game Operations Samples project is older than the " +
            "minimum version required by the backend. Please download a new version from " +
            "https://github.com/Unity-Technologies/com.unity.services.samples.game-operations";

        const string k_NewerLatestVersionMessage =
            "There is a newer version of the Game Operations Samples project available! You can download it from " +
            "https://github.com/Unity-Technologies/com.unity.services.samples.game-operations";

        async void Start()
        {
            // only check the version once per Play Mode
            if (s_VersionWasChecked)
            {
                DestroyImmediate(this);
                return;
            }

            // a use case manager is going to initialize the services and sign in, so wait for that
            while (UnityServices.State != ServicesInitializationState.Initialized
                   || !AuthenticationService.Instance.IsSignedIn)
            {
                await Task.Delay(1);
            }

            await GetConfigs();
        }

        async Task GetConfigs()
        {
            ConfigManager.SetCustomUserID(AuthenticationService.Instance.PlayerId);

            await ConfigManager.FetchConfigsAsync(new UserAttributes(), new AppAttributes());

            // Check that scene has not been unloaded while processing async wait to prevent throw.
            if (this == null) return;

            var clientVersion = new Version(Application.version);
            var clientVersionMinimumRaw = ConfigManager.appConfig.GetString("CLIENT_VERSION_MIN");
            var clientVersionMinimum = new Version(clientVersionMinimumRaw);
            var clientVersionLatestRaw = ConfigManager.appConfig.GetString("CLIENT_VERSION_LATEST");
            var clientVersionLatest = new Version(clientVersionLatestRaw);

            if (clientVersion < clientVersionMinimum)
            {
                Debug.LogError(k_NewerMinimumVersionMessage);

#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
            else if (clientVersion < clientVersionLatest)
            {
                Debug.Log(k_NewerLatestVersionMessage);
            }

            s_VersionWasChecked = true;
        }

        struct UserAttributes { }
        struct AppAttributes { }
    }
}
