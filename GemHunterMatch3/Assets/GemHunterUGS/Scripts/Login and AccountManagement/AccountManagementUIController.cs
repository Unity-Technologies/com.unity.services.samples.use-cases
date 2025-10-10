using System;
using System.Threading.Tasks;
#if FACEBOOK_SDK
using Facebook.Unity;
#endif
using Unity.Services.Authentication;
using Unity.Services.Authentication.PlayerAccounts;
using UnityEngine;
using UnityEngine.UIElements;
using GemHunterUGS.Scripts.Core;
using GemHunterUGS.Scripts.PlayerDataManagement;
using GemHunterUGS.Scripts.PlayerHub;
using GemHunterUGS.Scripts.Utilities;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;

namespace GemHunterUGS.Scripts.Login_and_AccountManagement
{
    public enum LinkType
    {
        UnityPlayerAccount,
        Facebook,
        GooglePlayGames,
        Apple
    }
    /// <summary>
    /// Controls the account management UI for linking and unlinking authentication providers.
    /// Handles Unity ID, Facebook, and platform-specific authentication (Google for Android, see note below for Apple for iOS).
    /// Provides functionality for account linking, unlinking, and deletion with appropriate UI feedback.
    /// </summary>
    public class AccountManagementUIController : MonoBehaviour
    {
        [SerializeField]
        private AccountManagementView m_View;
        [SerializeField]
        private AccountMenuTestSaveLoad m_AccountMenuTestSaveLoad;
        [SerializeField]
        private HubUIController m_HubUIController;
        
        private PlayerDataManager m_PlayerDataManager;
        private GameManagerUGS m_GameManagerUGS;
        
        private UnityPlayerAccountSignIn m_UnityPlayerAccountSignIn;
        private FacebookSignIn m_FacebookSignIn;
        
        #if UNITY_ANDROID
        private GooglePlayGamesSignIn m_GooglePlayGamesSignIn;
        private Action m_GoogleButtonLink;
        private Action m_GoogleButtonUnlink;
        #endif
        
        #if UNITY_IOS
        private AppleGameCenterSignIn m_AppleGameCenterSignIn;
        private Action m_AppleButtonLink;
        private Action m_AppleButtonUnlink;
        #endif
        
        // public static Action LinkedAccountsChanged;
        
        private Action m_UnityIDLink;
        private Action m_FacebookLink;
        
        private Action m_UnityIDUnlink;
        private Action m_FacebookUnlink;
        
        public void Initialize()
        {
            if (m_View == null)
            {
                m_View = GetComponent<AccountManagementView>();
            }
            
            m_View.Initialize();
            
            m_PlayerDataManager = GameSystemLocator.Get<PlayerDataManager>();
            m_GameManagerUGS = GameSystemLocator.Get<GameManagerUGS>();
            
            m_UnityPlayerAccountSignIn = GetComponent<UnityPlayerAccountSignIn>();
            m_FacebookSignIn = GetComponent<FacebookSignIn>();

            #if UNITY_ANDROID
            m_GooglePlayGamesSignIn = GetComponent<GooglePlayGamesSignIn>();
            #endif
            
            SetUpEventListeners();
            RefreshAccountUI();

            if (m_AccountMenuTestSaveLoad == null)
            {
                Logger.LogWarning("AccountMenuTestSaveLoad is null");
                return;
            }
            
            m_AccountMenuTestSaveLoad.Initialize(m_View);
        }
        
        private void SetUpEventListeners()
        {
            m_UnityIDLink = () => LinkAccount(LinkType.UnityPlayerAccount, m_View.UnityIDButton);
            m_FacebookLink = () => LinkAccount(LinkType.Facebook, m_View.FacebookButton);
            
            #if UNITY_ANDROID
            m_GoogleButtonLink = () => LinkAccount(LinkType.GooglePlayGames, m_View.GoogleButton);
            m_GoogleButtonUnlink = () => UnlinkAccount(LinkType.GooglePlayGames, m_View.GoogleButton);
            m_View.GoogleButton.clicked += m_GoogleButtonLink;
            m_View.UnlinkGoogleButton.clicked += m_GoogleButtonUnlink;
            #endif
            
            m_UnityIDUnlink = () => UnlinkAccount(LinkType.UnityPlayerAccount, m_View.UnityIDButton);
            m_FacebookUnlink = () => UnlinkAccount(LinkType.Facebook, m_View.FacebookButton);

            
            m_View.UnityIDButton.clicked += m_UnityIDLink;
            m_View.FacebookButton.clicked += m_FacebookLink;
            
            m_View.UnlinkUnityButton.clicked += m_UnityIDUnlink;
            m_View.UnlinkFacebookButton.clicked += m_FacebookUnlink;
            
            m_View.DeleteAllAccountsButton.clicked += OpenConfirmationDialog;
            m_View.ConfirmDialogButton.clicked += DeleteAllAccounts;
            m_View.CancelDialogButton.clicked += CancelConfirmDialog;

            m_View.TestInfoButton.clicked += m_View.ShowTestSaveInfoPopUp;
            m_View.ClosePopupButton.clicked += m_View.CloseTestSaveInfoPopUp;
            
            m_View.AccountActionCancelButton.clicked += HandleCloseAccountManagement;
            m_View.CloseAccountMenuButton.clicked += HandleCloseAccountManagement;
        }

        public void UpdateAccounts(string platform)
        {
            Logger.LogDemo($"Refreshing UI with change to {platform}");
            RefreshAccountUI();
        }
        
        public void OnAuthenticationError(string platform, string error)
        {
            Logger.LogError($"Authentication failed for {platform}: {error}");
            // Show an error message in UI
            // Re-enable any disabled buttons
            RefreshAccountUI();
        }
        
        private async void RefreshAccountUI()
        {
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                Logger.LogWarning("AUTHENTICATION NOT SIGNED IN");
                SetAllAccountButtonsToUnlinkedState(); 
                m_View.SetPlayerID("NOT SIGNED IN"); 
                return;
            }

            string playerID = AuthenticationService.Instance.PlayerId;
            m_View.SetPlayerID(playerID);

            var playerInfo = await AuthenticationService.Instance.GetPlayerInfoAsync();

            bool isUnityLinked = playerInfo.GetUnityId() != null;
            bool isFacebookLinked = playerInfo.GetFacebookId() != null;
            
            #if UNITY_ANDROID
            bool isGooglePlayGamesLinked = playerInfo.GetGooglePlayGamesId() != null;
            #endif

            #if UNITY_IOS
            bool isAppleGameCenterLinked = playerInfo.GetAppleGameCenterId() != null;
            #endif      

            m_View.SetAccountStatus(LinkType.UnityPlayerAccount, isUnityLinked);
            m_View.SetAccountStatus(LinkType.Facebook, isFacebookLinked);
            
            #if UNITY_ANDROID
            m_View.SetAccountStatus(LinkType.GooglePlayGames, isGooglePlayGamesLinked);
            #endif
            
            #if UNITY_IOS
            // Note: Kept some implementation here to help get you started
            m_View.SetAccountStatus(LinkType.Apple, isAppleGameCenterLinked);
            
            m_View.UpdateButtonState(m_View.AppleButton, "Apple", m_View.UnlinkAppleButton, isAppleGameCenterLinked);
            #endif  
            
            m_View.UpdateButtonState(m_View.UnityIDButton, "Unity", m_View.UnlinkUnityButton, isUnityLinked);
            m_View.UpdateButtonState(m_View.FacebookButton, "Facebook", m_View.UnlinkFacebookButton, isFacebookLinked);
            
            #if UNITY_ANDROID
            m_View.UpdateButtonState(m_View.GoogleButton, "Google Play", m_View.UnlinkGoogleButton, isGooglePlayGamesLinked);
            #endif
        }
        
        private void SetAllAccountButtonsToUnlinkedState()
        {
            m_View.UnlinkStatusForAllAccounts();
            m_View.UpdateButtonState(m_View.UnityIDButton, "Unity Player", m_View.UnlinkUnityButton, false);
            m_View.UpdateButtonState(m_View.FacebookButton, "Facebook", m_View.UnlinkFacebookButton,false);
            m_View.UpdateButtonState(m_View.GoogleButton, "Google Play",m_View.UnlinkGoogleButton, false);
        }
        
         private void LinkAccount(LinkType linkType, Button linkButton)
         { 
             linkButton.SetEnabled(false);
            
            switch (linkType)
            {
                case LinkType.UnityPlayerAccount:
                    m_UnityPlayerAccountSignIn.StartSignInOrLink();
                    break;
                case LinkType.Facebook:
                    m_FacebookSignIn.StartSignInOrLink();
                    break;
                #if UNITY_ANDROID
                case LinkType.GooglePlayGames:
                    m_GooglePlayGamesSignIn.StartSignInOrLink();
                    break;
                #endif
                
                #if UNITY_IOS
                case LinkType.Apple:
                    await AuthenticationService.Instance.LinkWithAppleAsync("apple_id_token");
                    break;
                #endif
                }
         }
        
         
#if UNITY_IOS
        private async Task LinkWithAppleAsync()
        {
            // Get Apple token from sign-in component (when implemented)
            string appleToken = await m_AppleSignIn.GetAppleTokenAsync();
            if (string.IsNullOrEmpty(appleToken))
            {
                throw new Exception("Failed to get Apple token");
            }
            
            await AuthenticationService.Instance.LinkWithAppleAsync(appleToken);
            Logger.LogDemo("Successfully linked with Apple");
        }
#endif
         

        private async void UnlinkAccount(LinkType linkType, Button unlinkButton)
        {
            unlinkButton.SetEnabled(false);
            try
            {
                switch (linkType)
                {
                    case LinkType.UnityPlayerAccount:
                        await AuthenticationService.Instance.UnlinkUnityAsync();
                        break;
                    case LinkType.Facebook:
                        await AuthenticationService.Instance.UnlinkFacebookAsync();
                        break;
                    #if UNITY_ANDROID
                    case LinkType.GooglePlayGames:
                        await AuthenticationService.Instance.UnlinkGooglePlayGamesAsync();
                        break;
                    #endif
                    #if UNITY_IOS
                    case LinkType.Apple:
                        await AuthenticationService.Instance.UnlinkAppleGameCenterAsync();
                        break;
                    #endif
                    default:
                        throw new ArgumentOutOfRangeException(nameof(linkType), linkType, null);
                }
                
                Logger.Log($"Unlinking {linkType} Account");
                UpdateAccounts(linkType.ToString());
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to unlink {linkType}: {ex.Message}");
                OnAuthenticationError(linkType.ToString(), ex.Message);
            }
        }

        private void OpenConfirmationDialog()
        {
            m_View.ShowConfirmationDialog("Are you sure you want to delete your account?");  
        }
        
        private void CancelConfirmDialog()
        {
            m_View.CloseConfirmationDialog();
        }

        private void HandleCloseAccountManagement()
        {
            m_View.CloseConfirmationDialog();
            m_View.CloseTestSaveInfoPopUp();
            m_HubUIController.ShowMainHub();
            m_HubUIController.HandleToggleAccountManagementMenu();
        }

        private async void DeleteAllAccounts()
        {
            await DeleteAllAccountsAsync();
        }
        
        private async Task DeleteAllAccountsAsync()
        {
            try
            {
                if (AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.DeleteAccountAsync();
                }
                
                if (AuthenticationService.Instance.SessionTokenExists)
                {
                    AuthenticationService.Instance.ClearSessionToken();
                }
                
                m_PlayerDataManager.DeleteLocalPlayerData();
                
                Logger.LogDemo("Account successfully deleted and all data cleared. Loading main menu--stop and replay the project.");
                
                await m_GameManagerUGS.RequestLoadMainMenu();
            }
            
            catch (AuthenticationException e)
            {
                Logger.LogException(e);
                await m_GameManagerUGS.RequestLoadMainMenu();
            }
        }
        
        /// <summary>
        /// Utility method to clear all linked accounts. 
        /// This can be useful during testing or when implementing account management features.
        /// </summary>
        private async void ClearAllLinkedAccounts()
        {
            try
            {
                await AuthenticationService.Instance.UnlinkFacebookAsync();
            }
            catch (AuthenticationException e)
            {
                Logger.LogWarning($"Failed to unlink Facebook account: {e.Message}");
            }
            
            try
            {
                await AuthenticationService.Instance.UnlinkUnityAsync();
            }
            catch (AuthenticationException e)
            {
                Logger.LogWarning($"Failed to unlink Unity account: {e.Message}");
            }

            try
            {
                await AuthenticationService.Instance.UnlinkAppleGameCenterAsync();
            }
            catch (AuthenticationException e)
            {
                Logger.LogWarning($"Failed to unlink Apple account: {e.Message}");
            }
        }
        
        private void OnDisable()
        {
            // Prevents unnecessary subscription errors if PlayerHub scene is loaded first
            if (m_View == null || m_View.UnityIDButton == null)
            {
                return;
            }
            
            m_View.UnityIDButton.clicked -= m_UnityIDLink;
            m_View.FacebookButton.clicked -= m_FacebookLink;
            
            #if UNITY_ANDROID
            m_View.GoogleButton.clicked -= m_GoogleButtonLink;
            m_View.UnlinkGoogleButton.clicked -= m_GoogleButtonUnlink;
            #endif
            
            m_View.UnlinkUnityButton.clicked -= m_UnityIDUnlink;
            m_View.UnlinkFacebookButton.clicked -= m_FacebookUnlink;

            m_View.DeleteAllAccountsButton.clicked -= OpenConfirmationDialog;
            m_View.ConfirmDialogButton.clicked -= DeleteAllAccounts;
            m_View.CancelDialogButton.clicked -= CancelConfirmDialog;
            
            m_View.TestInfoButton.clicked -= m_View.ShowTestSaveInfoPopUp;
            m_View.ClosePopupButton.clicked -= m_View.CloseTestSaveInfoPopUp;
            
            m_View.AccountActionCancelButton.clicked -= HandleCloseAccountManagement;
        }
    }
}
