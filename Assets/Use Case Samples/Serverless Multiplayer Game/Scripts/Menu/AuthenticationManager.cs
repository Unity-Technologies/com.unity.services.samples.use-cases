using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace Unity.Services.Samples.ServerlessMultiplayerGame
{
    public static class AuthenticationManager
    {
        // This Sign In method is called at startup and anytime the player chooses to switch profiles. The profiles
        // used here are needed to permit testing a multiplayer game using only 1 anonymous sign in account. By
        // switching to a different profile, UGS sees the current user as a completely different one from other
        // profile names. We pass the profile we wish to use and this method will initialize Unity Services if
        // necessary and also request that the Authentication Service switch the current user to a different
        // profile, thus accessing different data on UGS, including Cloud Save data and Player Id.
        public static async Task SignInAnonymously(string profileName, int profileIndex)
        {
            try
            {
                SwitchProfileIfNecessary(profileName);

                await InitialzeUnityServices(profileName);

                await AuthenticationService.Instance.SignInAnonymouslyAsync();

                // Save off the last profile index used so we can default to this profile index at startup.
                ProfileManager.SaveLatestProfileIndexForProjectPath(profileIndex);

                Debug.Log($"Profile: {profileName} PlayerId: {AuthenticationService.Instance.PlayerId} " +
                    $"playerStats: [{CloudSaveManager.instance.playerStats}]");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        static void SwitchProfileIfNecessary(string profileName)
        {
            try
            {
                if (UnityServices.State == ServicesInitializationState.Initialized)
                {
                    if (AuthenticationService.Instance.IsSignedIn)
                    {
                        AuthenticationService.Instance.SignOut();
                    }

                    AuthenticationService.Instance.SwitchProfile(profileName);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        static async Task InitialzeUnityServices(string profileName)
        {
            try
            {
                var unityAuthenticationInitOptions = new InitializationOptions();
                unityAuthenticationInitOptions.SetProfile(profileName);
                await UnityServices.InitializeAsync(unityAuthenticationInitOptions);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
