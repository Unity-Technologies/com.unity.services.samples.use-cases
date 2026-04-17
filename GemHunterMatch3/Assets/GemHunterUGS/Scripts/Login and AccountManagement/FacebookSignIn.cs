using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using GemHunterUGS.Scripts.Core;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;

namespace GemHunterUGS.Scripts.Login_and_AccountManagement
{
    public class FacebookSignIn : MonoBehaviour
    {
        [Header("Testing")]
        [SerializeField]
        private bool m_IsTesting = false;
        [SerializeField]
        private string m_TestToken;
        
        private FacebookManager m_FacebookManager;
        private GameManagerUGS m_GameManagerUGS;
        private AccountManagementUIController m_AccountUIController;
        
        private void Start()
        {
            m_FacebookManager = GameSystemLocator.Get<FacebookManager>();
            m_GameManagerUGS = GameSystemLocator.Get<GameManagerUGS>();
            
            m_AccountUIController = GetComponent<AccountManagementUIController>();
        }
        
        /// <summary>
        /// Unified entry point - handles both sign-in and linking based on current authentication state
        /// Called from both main menu (sign-in) and account management (linking)
        /// </summary>
        
        public async void StartSignInOrLink()
        {
#if UNITY_EDITOR
            Logger.LogDemo("Using testing mode in Editor");
            m_IsTesting = true;
            // Add a test token here for Editor testing
#endif
            
            if (m_IsTesting && !string.IsNullOrEmpty(m_TestToken))
            {
                Logger.LogDemo("Testing Facebook login with test token");
                await ProcessFacebookToken(m_TestToken);
                return;
            }
    
#if UNITY_IOS
    await SignInWithAttHandling();
#else
            try
            {
                var (success, token, userId) = await m_FacebookManager.LoginAsync();
                if (success)
                {
                    Logger.LogDemo($"Facebook login successful. User ID: {userId}");
                    await ProcessFacebookToken(token);
                }
                else
                {
                    Logger.LogWarning("Facebook login failed or was cancelled.");
                    HandleAuthenticationFailure("Facebook login failed or was cancelled");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error during Facebook authentication: {ex.Message}");
                HandleAuthenticationFailure(ex.Message);
            }
#endif
        }
        
#if UNITY_IOS
        private async Task SignInWithAttHandling()
        {
            var tcs = new TaskCompletionSource<bool>();
            
            FB.LogInWithReadPermissions(null, async result =>
            {
                try
                {
                    if (FB.IsLoggedIn)
                    {
                        // Get the appropriate token based on ATT status
                        string accessToken = GetFacebookTokenForIOS(result);
                        await HandleTokenAsync(accessToken);
                        tcs.SetResult(true);
                    }
                    else
                    {
                        Logger.LogDemo("Facebook login cancelled by user");
                        HandleSignInOrLinkFailure("User cancelled Facebook login");
                        tcs.SetResult(false);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error in iOS Facebook flow: {ex.Message}");
                    HandleSignInOrLinkFailure(ex.Message);
                    tcs.SetException(ex);
                }
            });
            
            await tcs.Task;
        }

        private string GetFacebookTokenForIOS(ILoginResult result)
        {
            // Check ATT status to determine which token to use
            if (ATTrackingStatusBinding.GetAuthorizationTrackingStatus() ==
                ATTrackingStatusBinding.AuthorizationTrackingStatus.AUTHORIZED)
            {
                Logger.LogDemo("Using standard access token");
                return AccessToken.CurrentAccessToken.TokenString;
            }
            else
            {
                Logger.LogDemo("Using Limited Login authentication token");
                if (result.AuthenticationToken != null)
                {
                    return result.AuthenticationToken.TokenString;
                }
                else
                {
                    throw new Exception("Authentication token is null");
                }
            }
        }
#endif
        /// <summary>
        /// Routes Facebook token to the appropriate method based on authentication state
        /// </summary>
        private async Task ProcessFacebookToken(string accessToken)
        {
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await SignInWithFacebookAsync(accessToken);
            }
            else
            {
                await LinkWithFacebookAsync(accessToken);
            }
        }
        
        /// <summary>
        /// Sign in with Facebook for new users
        /// </summary>
        private async Task SignInWithFacebookAsync(string accessToken)
        {
            try
            {
                Logger.LogDemo("Signing in with Facebook...");
                await AuthenticationService.Instance.SignInWithFacebookAsync(accessToken);
                Logger.LogDemo("Successfully signed in with Facebook!");
        
                // Navigate to hub after sign-in
                await m_GameManagerUGS.RequestLoadPlayerHub();
            }
            catch (AuthenticationException ex)
            {
                Logger.LogError($"Facebook sign-in failed: {ex.Message}");
                HandleAuthenticationFailure(ex.Message);
            }
            catch (RequestFailedException ex)
            {
                Logger.LogError($"Failed Facebook sign-in request. Error code: {ex.ErrorCode}");
                HandleAuthenticationFailure(ex.Message);
            }
        }

        /// <summary>
        /// Link Facebook account for existing users
        /// </summary>
        private async Task LinkWithFacebookAsync(string accessToken)
        {
            try
            {
                Logger.LogDemo("Linking Facebook account...");
                await AuthenticationService.Instance.LinkWithFacebookAsync(accessToken);
                Logger.LogDemo("Successfully linked with Facebook!");
        
                // Update account management UI
                m_AccountUIController?.UpdateAccounts("Facebook");
            }
            catch (AuthenticationException ex) when (ex.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
            {
                Logger.LogWarning("Account is already linked with another Facebook account");
                m_AccountUIController?.UpdateAccounts("Facebook"); // Still refresh UI
            }
            catch (Exception ex)
            {
                Logger.LogError($"Facebook linking failed: {ex.Message}");
                m_AccountUIController?.OnAuthenticationError("Facebook", ex.Message);
            }
        }
        
        /// <summary>
        /// Handle authentication failures for both sign-in and linking
        /// </summary>
        private void HandleAuthenticationFailure(string error)
        {
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                // Failed during sign-in - return to main menu
                Logger.LogDemo("Facebook sign-in failed, returning to main menu");
                _ = m_GameManagerUGS.RequestLoadMainMenu();
            }
            else
            {
                // Failed during linking - update account management UI
                m_AccountUIController?.OnAuthenticationError("Facebook", error);
            }
        }
    }
}