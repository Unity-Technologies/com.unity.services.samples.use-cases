using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Authentication;
using Unity.Services.Core;
using GemHunterUGS.Scripts.Utilities;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;
namespace GemHunterUGS.Scripts.Core
{
    /// <summary>
    /// Manages player authentication lifecycle with Unity Gaming Services.
    /// Handles initial sign-in with cached credentials, access token expiry recovery,
    /// and authentication state changes through events. 
    /// </summary>
    public class PlayerAuthenticationManager : IDisposable
    {
        public bool IsSignedIn => AuthenticationService.Instance.IsSignedIn;
        private bool m_IsResumingFromExpiredToken = false;
        
        /// <summary>
        /// Triggered when sign-in is successful.
        /// </summary>
        public event Action SignedIn;
        /// <summary>
        /// Triggered when sign-in succeeds after recovering from an expired access token.
        /// This is distinct from initial sign-in and thus could be handled differently.
        /// </summary>
        public event Action SignedInAfterTokenExpiry;
        public event Action SignInFailed;
        private const string k_KeyEmoji = "🔑";
        
        public PlayerAuthenticationManager()
        {
            AuthenticationService.Instance.SignedIn += HandleSuccessfulSignIn;
            AuthenticationService.Instance.SignedOut += HandleSignedOut;
            AuthenticationService.Instance.Expired += HandleSessionExpired;
        }
        
        public async Task SignInCachedPlayerAsync()
        {
            if (!AuthenticationService.Instance.SessionTokenExists)
            {
                Logger.LogDemo($"{k_KeyEmoji} No cached session found");
                SignInFailed?.Invoke();
                return;
            }
            
            try 
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Logger.LogDemo($"{k_KeyEmoji} Existing player returned");
            }
            catch (AuthenticationException ex) 
            {
                Logger.LogWarning($"💡 Authentication failed - if testing, try enabling 'Delete Account On Start' in GameInitializer to reset state {ex.Message}");
                SignInFailed?.Invoke();
            }
            catch (RequestFailedException ex) 
            {
                Logger.LogWarning($"Network error during sign-in: {ex.Message}");
                SignInFailed?.Invoke();
            }
        }
        
        /// <summary>
        /// Unity Authentication's access tokens are valid for 1 hour and refreshed when necessary.
        /// If the token can't be refreshed (e.g. the player is offline), the token expires.
        /// In this case, when the player goes back online, they need to be signed in again to obtain authorization to call Unity services
        /// </summary>
        public async Task SignInResumeFromExpiredAccessTokenAsync()
        {
            if (!AuthenticationService.Instance.IsExpired)
            {
                Logger.LogWarning("Sign in not required, access token has not expired");
                return;
            }
            
            try
            {
                Logger.LogDemo($"{k_KeyEmoji} Signing in again due to expired access token");
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                m_IsResumingFromExpiredToken = true;
            }
            catch (RequestFailedException ex) 
            {
                Logger.LogWarning($"Network error during sign-in: {ex.Message}");
                SignInFailed?.Invoke();
            }
        }
        
        private void HandleSuccessfulSignIn()
        {
            // For simplicity, GemHunterUGS requires being online
            // In your projects, you'll likely want to handle start-up sign in and in-game "back online" sign-in separately
            if (m_IsResumingFromExpiredToken)
            {
                // An event for handling coming online after being offline for a while (e.g. player progress is validated in and saved to cloud)
                SignedInAfterTokenExpiry?.Invoke();
                m_IsResumingFromExpiredToken = false;
                return;
            }
            
            // PlayerDataManagerClient handles sign in by fetching cloud data, this flows to overwriting local data
            SignedIn?.Invoke();
            LogPlayerInfo();
        }

        private void HandleSignedOut()
        {
            Logger.LogDemo($"{k_KeyEmoji} Player signed out");
        }

        private void HandleSessionExpired()
        {
            Logger.LogDemo($"{k_KeyEmoji} Session expired! You'll need to sign in again when possible");
        }
        
        private void LogPlayerInfo()
        {
            var playerId = AuthenticationService.Instance.PlayerId;
            var accessToken = AuthenticationService.Instance.AccessToken;
            Logger.LogDemo($"{k_KeyEmoji} Authentication successful!" +
                $"\n{k_KeyEmoji} PlayerID: {playerId}" +
                $"\n{k_KeyEmoji} Token: {accessToken}");
        }
        
        public void Dispose()
        {
            AuthenticationService.Instance.SignedIn -= HandleSuccessfulSignIn;
            AuthenticationService.Instance.SignedOut -= HandleSignedOut;
            AuthenticationService.Instance.Expired -= HandleSessionExpired;
        }
    }
}
