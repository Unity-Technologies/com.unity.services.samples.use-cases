using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
#if FACEBOOK_SDK
using Facebook.Unity;
#endif
using Logger = GemHunterUGS.Scripts.Utilities.Logger;
namespace GemHunterUGS.Scripts.Login_and_AccountManagement
{
    public class FacebookManager : MonoBehaviour, IDisposable
    {
        private static bool s_IsInitialized = false;
        private static TaskCompletionSource<bool> s_InitializationTask;

        private async void Awake()
        {
            Logger.LogDemo("Initializing Facebook Manager...");
            await InitializeAsync();
        }
        
        /// <summary>
        /// Initialize Facebook SDK
        /// </summary>
        private static async Task InitializeAsync()
        {
#if FACEBOOK_SDK
            if (s_IsInitialized) return;

            if (s_InitializationTask == null)
            {
                s_InitializationTask = new TaskCompletionSource<bool>();

                if (!FB.IsInitialized)
                {
                    Logger.LogDemo("Starting Facebook SDK initialization...");
                    FB.Init(InitCallback);
                }
                else
                {
                    Logger.LogDemo("Facebook SDK already initialized");
                    s_IsInitialized = true;
                    s_InitializationTask.TrySetResult(true);
                    FB.ActivateApp();
                }
            }

            await s_InitializationTask.Task;
#endif
        }
        
        private static void InitCallback()
        {
#if FACEBOOK_SDK
            if (FB.IsInitialized)
            {
                FB.ActivateApp();
                Logger.LogDemo("Facebook SDK initialized");
                s_IsInitialized = true;
                s_InitializationTask.TrySetResult(true);
            }
            else
            {
                Logger.LogWarning("Failed to initialize the Facebook SDK");
                s_InitializationTask.TrySetException(new Exception("Failed to initialize the Facebook SDK"));
            }
#endif
        }

        private void OnHideUnity(bool isGameShown)
        {
            Logger.LogDemo($"OnHideUnity called. Game shown: {isGameShown}");
            Time.timeScale = isGameShown ? 1 : 0;
        }

        public async Task<(bool success, string token, string userId)> LoginAsync()
        {
#if FACEBOOK_SDK
            await EnsureInitializedAsync();

            // Check if Facebook App ID is configured
            if (string.IsNullOrEmpty(FB.AppId))
            {
                Logger.LogError("Facebook App ID is not configured! Please configure it in Facebook > Edit Settings");
                return (false, null, null);
            }
            
            if (!FB.IsInitialized)
            {
                Logger.LogError("Facebook SDK is not initialized");
                return (false, null, null);
            }
            
            var tcs = new TaskCompletionSource<(bool success, string token, string userId)>();
            var perms = new List<string>() { "public_profile", "email" };
            
            Logger.LogDemo("Starting Facebook login with permissions: " + string.Join(", ", perms));
            
            FB.LogInWithReadPermissions(perms, result =>
            {
                Logger.LogDemo($"Facebook login callback received. Success: {result != null && string.IsNullOrEmpty(result.Error)}");
                
                if (result == null)
                {
                    Logger.LogError("Facebook login result is null");
                    tcs.SetResult((false, null, null));
                    return;
                }

                if (!string.IsNullOrEmpty(result.Error))
                {
                    Logger.LogError($"Facebook login error: {result.Error}");
                    tcs.SetResult((false, null, null));
                    return;
                }
                
                if (FB.IsLoggedIn)
                {
                    var aToken = AccessToken.CurrentAccessToken;
                    
                    Logger.LogDemo($"Logged in. User ID: {aToken.UserId}");
                    Logger.LogDemo($"Token: {aToken.TokenString}");
                    Logger.LogDemo($"Token Length: {aToken.TokenString?.Length}");
                    
                    var grantedPermissions = aToken.Permissions.ToList();
                    var missingPermissions = perms.Except(grantedPermissions).ToList();
                    
                    if (missingPermissions.Any())
                    {
                        Logger.LogWarning($"Missing permissions: {string.Join(", ", missingPermissions)}");
                    }
                    
                    tcs.SetResult((true, aToken.TokenString, aToken.UserId));
                }
                else
                {
                    Logger.LogError(result.Error != null ? $"Login failed: {result.Error}" : "User cancelled login");
                    tcs.SetResult((false, null, null));
                }
            });

            return await tcs.Task;
#else
            return (false, null, null);
#endif
        }
        
        /// <summary>
        /// Check if user is currently logged in to Facebook
        /// </summary>
        public bool IsLoggedIn =>
            #if FACEBOOK_SDK
            FB.IsLoggedIn;
            #else
            false;
            #endif
        
        /// <summary>
        /// Get current access token if logged in
        /// </summary>
        public string GetCurrentToken()
        {
#if FACEBOOK_SDK
            return FB.IsLoggedIn ? AccessToken.CurrentAccessToken?.TokenString : null;
#else
            return null;
            #endif
        }
        
        private async Task EnsureInitializedAsync()
        {
            if (!s_IsInitialized)
            {
                await InitializeAsync();
            }
        }
        
        public async Task RefreshAccessTokenAsync()
        {
#if FACEBOOK_SDK
            await EnsureInitializedAsync();

            var tcs = new TaskCompletionSource<bool>();

            FB.Mobile.RefreshCurrentAccessToken(result =>
            {
                if (result.Error != null)
                {
                    Logger.LogError($"Error refreshing access token: {result.Error}");
                    tcs.SetResult(false);
                }
                else
                {
                    Logger.LogDemo("Access token refreshed successfully");
                    tcs.SetResult(true);
                }
            });
            await tcs.Task;
#endif
        }

        public void Dispose()
        {
            // Perform any cleanup if necessary
        }
    }
}
