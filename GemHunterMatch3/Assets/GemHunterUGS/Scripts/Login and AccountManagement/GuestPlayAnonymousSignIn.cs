using System;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using GemHunterUGS.Scripts.Core;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;
namespace GemHunterUGS.Scripts.Login_and_AccountManagement
{
    /// <summary>
    /// Manages anonymous guest authentication with Unity Gaming Services
    /// </summary>
    public class GuestPlayAnonymousSignIn : MonoBehaviour
    {
        // Not using -- profiles can be used to have multiple accounts on a single device
        private string m_ProfileNameInput = "Cousin Steve";
        private PlayerInfo m_PlayerInfo;
        
        private GameManagerUGS m_GameManagerUGS;
        private NetworkConnectivityHandler m_NetworkConnectivityHandler;
        private bool m_IsSigningInAnonymously = false;
        
        private void Start()
        {
            m_GameManagerUGS = GameSystemLocator.Get<GameManagerUGS>();
            
            AuthenticationService.Instance.SignedIn += HandleSignedIn;
            AuthenticationService.Instance.SignInFailed += HandleSignInFailed;
            AuthenticationService.Instance.SignedOut += HandleSignedOut;
        }
        
        public async void SignInAnonymousAccount()
        {
            try
            {
                if (AuthenticationService.Instance.IsSignedIn)
                {
                    Logger.LogWarning("Player is already signed in");
                    return;
                }
                m_IsSigningInAnonymously = true;
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
            catch (RequestFailedException ex)
            {
                Logger.LogWarning($"Sign in anonymously failed with error code: {ex.ErrorCode}");
            }
        }

        private async void HandleSignedIn()
        {
            if (m_IsSigningInAnonymously)
            {
                await m_GameManagerUGS.RequestLoadPlayerHub();
                m_IsSigningInAnonymously = false;
            }
        }

        private void HandleSignedOut()
        {
            Logger.LogDemo("Authentication SignedOut!");
        }

        private async void HandleSignInFailed(RequestFailedException ex)
        {
            Logger.LogWarning($"Sign in anonymously failed with error code: {ex.ErrorCode}");
            await m_GameManagerUGS.RequestLoadPlayerHub();
            m_NetworkConnectivityHandler.HandleNetworkException(ex);
        }

        void PlayerPrefsLog()
        {
            var sessionToken = PlayerPrefs.GetString($"{Application.cloudProjectId}.{AuthenticationService.Instance.Profile}.unity.services.authentication.session_token");
            var playerPrefsMessageResult = string.IsNullOrEmpty(sessionToken) ? "No session token for this profile" : $"Session token: {sessionToken}";
            Logger.Log(playerPrefsMessageResult);
        }
        
        public void OnClickSignOut()
        {
            AuthenticationService.Instance.SignOut();
        }
        
        public void OnClickSwitchProfile()
        {
            try
            {
                AuthenticationService.Instance.SwitchProfile(m_ProfileNameInput);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                m_ProfileNameInput = AuthenticationService.Instance.Profile;
            }
            Logger.Log($"Current Profile: {AuthenticationService.Instance.Profile}");
            PlayerPrefsLog();
        }
        
        private void OnDisable()
        {
            AuthenticationService.Instance.SignedIn -= HandleSignedIn;
            AuthenticationService.Instance.SignInFailed -= HandleSignInFailed;
            AuthenticationService.Instance.SignedOut -= HandleSignedOut;
        }
    }
}
