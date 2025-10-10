using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using UnityEngine;
using GemHunterUGS.Scripts.Core;
using Unity.Services.Core;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;
namespace GemHunterUGS.Scripts.Login_and_AccountManagement
{
#if UNITY_ANDROID
    using GooglePlayGames;
    using GooglePlayGames.BasicApi;
#endif
    public class GooglePlayGamesSignIn : MonoBehaviour
    {
#if UNITY_ANDROID
        private GameManagerUGS m_GameManagerUGS;
        public string GooglePlayGamesToken { get; private set; }
        private AccountManagementUIController m_AccountUIController;
        
        private void OnEnable()
        {
            InitializeSilentAuthentication();
        }

        private void InitializeSilentAuthentication()
        {
#if UNITY_EDITOR
            Logger.LogWarning("GooglePlayGamesSignIn component is present but will only function in Android builds.");
#endif
            
            m_GameManagerUGS = GameSystemLocator.Get<GameManagerUGS>();
            m_AccountUIController = GetComponent<AccountManagementUIController>();
            
            PlayGamesPlatform.DebugLogEnabled = true; // Enable debug logs
            PlayGamesPlatform.Activate();
            
            // Perform automatic silent authentication (recommended for Android)
            AuthenticateWithGooglePlayGames();
        }
        
        // Sign in with Google
        private void AuthenticateWithGooglePlayGames()
        {
            // Verify that Google Play Games is properly initialized
            if (PlayGamesPlatform.Instance == null)
            {
                Logger.LogError("PlayGamesPlatform.Instance is null! Platform not properly activated.");
                return;
            }
            
            // Double-check to avoid duplicate authentication attempts
            if (PlayGamesPlatform.Instance.IsAuthenticated())
            {
                Logger.LogDemo("Already authenticated with Google Play Games, skipping authentication");
                return;
            }
            
            PlayGamesPlatform.Instance.Authenticate((status) =>
            {
                if (status == SignInStatus.Success)
                {
                    Logger.Log("Login with Google Play games successful.");
                    PlayGamesPlatform.Instance.RequestServerSideAccess(true, code =>
                    {
                        Logger.Log("Authorization code: " + code);
                        GooglePlayGamesToken = code;
                        // Token will be used in SignInWithGooglePlayGames
                    });
                }
                else
                {
                    HandleSilentAuthenticationFailure(status);
                }
            });
        }

        private void HandleSilentAuthenticationFailure(SignInStatus status)
        {
            // Handle different failure cases appropriately
            switch (status)
            {
                case SignInStatus.Canceled:
                    Logger.LogWarning("Google Play Games login was cancelled");
                    // Don't treat cancellation as an error - just refresh UI
                    break;

                case SignInStatus.InternalError:
                    Logger.LogError("Google Play Games internal error");
                    break;

                default:
                    Logger.LogError($"Google Play Games login failed: {status}");
                    break;
            }
        }
        
        public void StartSignInOrLink()
        {
            if (!PlayGamesPlatform.Instance.IsAuthenticated())
            {
                Logger.LogWarning("Not yet authenticated with Google Play Games -- attempting login again");
                AuthenticateWithGooglePlayGames();
                return;
            }

            // Already authenticated with GPG, proceed with Unity Authentication
            SignInOrLinkWithGooglePlayGames();
        }
        
        /// <summary>
        /// Sign in or link with Unity Authentication using Google Play Games
        /// </summary>
        private async void SignInOrLinkWithGooglePlayGames()
        {
            if (string.IsNullOrEmpty(GooglePlayGamesToken))
            {
                Logger.LogWarning("Authorization code is null or empty!");
                m_AccountUIController?.OnAuthenticationError("Google", "No authorization code available");
                return;
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await SignInWithGooglePlayGamesAsync(GooglePlayGamesToken);
            }
            else
            {
                await LinkWithGooglePlayGamesAsync(GooglePlayGamesToken);
            }
        }

        private async Task SignInWithGooglePlayGamesAsync(string authCode)
        {
            try
            {
                await AuthenticationService.Instance.SignInWithGooglePlayGamesAsync(authCode);
                Logger.LogDemo("Sign in with Google Play Games is successful.");
                CompleteSignInProcess();
            }

            catch (AuthenticationException ex)
            {
                Logger.LogError($"Unity Authentication error: {ex.ErrorCode} - {ex.Message}");
                m_AccountUIController?.OnAuthenticationError("Google", ex.Message);
                await HandleSignInFailure();
            }

            catch (RequestFailedException ex)
            {
                Logger.LogError($"Request failed: {ex.ErrorCode} - {ex.Message}");
                m_AccountUIController?.OnAuthenticationError("Google", ex.Message);
                await HandleSignInFailure();
            }
        }
        
        private async Task LinkWithGooglePlayGamesAsync(string authCode)
        {
            try
            {
                Logger.LogDemo("Attempting to link Google Play Games account...");
                await AuthenticationService.Instance.LinkWithGooglePlayGamesAsync(authCode);
                Logger.LogDemo("Google Play Games link is successful.");
                m_AccountUIController?.UpdateAccounts("Google");
            }
            catch (AuthenticationException ex) when (ex.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
            {
                Logger.LogWarning("This user is already linked with another Google account.");
                m_AccountUIController?.UpdateAccounts("Google"); // Still refresh UI
            }
            catch (Exception ex)
            {
                Logger.LogError($"Linking failed: {ex.Message}");
                m_AccountUIController?.OnAuthenticationError("Google", ex.Message);
            }
        }

        private async void CompleteSignInProcess()
        {
            Logger.LogDemo("Google sign in success, loading hub");
            await m_GameManagerUGS.RequestLoadPlayerHub();
        }
        
        private async Task HandleSignInFailure()
        {
            Logger.LogDemo("Google Play Games sign-in failed, returning to main menu");
            await m_GameManagerUGS.RequestLoadMainMenu();
        }
        
        /// <summary>
        /// Check if authenticated with Google Play Games
        /// </summary>
        public bool IsAuthenticated()
        {
            return PlayGamesPlatform.Instance?.IsAuthenticated() == true;
        }
        
        #endif
    }
}