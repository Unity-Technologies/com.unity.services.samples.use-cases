using System;
using GemHunterUGS.Scripts.Core;
using UnityEngine;
namespace GemHunterUGS.Scripts.Login_and_AccountManagement
{
    public class MainMenuLoginUIController : MonoBehaviour
    {
        [SerializeField]
        private MainMenuLoginView m_MainMenuLoginView;
        [SerializeField]
        private GuestPlayAnonymousSignIn m_GuestPlayAnonymousSignIn;
        [SerializeField]
        private SignInOptionsUIController m_SignInOptionsController;
        
        private NetworkConnectivityHandler m_NetworkConnectivityHandler;
        private bool m_IsUIInitialized = false;
        
        private void OnEnable()
        {
            if (m_MainMenuLoginView != null && m_IsUIInitialized)
            {
                m_MainMenuLoginView.HideInfoPopUp();
                m_MainMenuLoginView.ShowMainMenu();
            }
        }

        private void Start()
        {
            m_NetworkConnectivityHandler = GameSystemLocator.Get<NetworkConnectivityHandler>();

            if (!m_IsUIInitialized)
            {
                m_MainMenuLoginView.InitializeLoginUI();
                m_MainMenuLoginView.HideInfoPopUp();
                m_MainMenuLoginView.ShowMainMenu();
                m_IsUIInitialized = true;
            }
            SetupEventHandlers();
        }

        private void SetupEventHandlers()
        {
            m_MainMenuLoginView.GuestPlayButton.clicked += HandleClickGuestPlay;
            m_MainMenuLoginView.ConnectAccountButton.clicked += HandleConnectSocialAccount;
            m_MainMenuLoginView.SignUpInfoButton.clicked += HandleOpenInfoPopUp;
            m_MainMenuLoginView.ClosePopUpButton.clicked += HandleCloseInfoPopUp;
            m_NetworkConnectivityHandler.OnlineStatusChanged += ToggleConnectAccountButton;
        }
        
        private void HandleClickGuestPlay()
        {
            m_MainMenuLoginView.HideMainMenuUI();
            m_GuestPlayAnonymousSignIn.SignInAnonymousAccount();
        }

        private void HandleConnectSocialAccount()
        {
            m_MainMenuLoginView.HideMainMenuUI();
            m_SignInOptionsController.ShowSocialSignUpOptions();
        }

        private void HandleOpenInfoPopUp()
        {
            m_MainMenuLoginView.ShowInfoPopUp();
        }

        private void HandleCloseInfoPopUp()
        {
            m_MainMenuLoginView.HideInfoPopUp();
        }

        public void OpenMainMenu()
        {
            m_MainMenuLoginView.ShowMainMenu();
            m_MainMenuLoginView.HideInfoPopUp();
        }
        
        private void ToggleConnectAccountButton(bool isOnline)
        {
            if (isOnline)
            {
                m_MainMenuLoginView.ConnectAccountButton.SetEnabled(true);
            }
            else 
            {
                m_MainMenuLoginView.ConnectAccountButton.SetEnabled(false);
            }
        }
        
        private void OnDisable()
        {
            if (m_MainMenuLoginView != null)
            {
                m_MainMenuLoginView.GuestPlayButton.clicked -= HandleClickGuestPlay;
                m_MainMenuLoginView.ConnectAccountButton.clicked -= HandleConnectSocialAccount;
                m_MainMenuLoginView.SignUpInfoButton.clicked -= HandleOpenInfoPopUp;
                m_MainMenuLoginView.ClosePopUpButton.clicked -= HandleCloseInfoPopUp;
            }
            
            if (m_NetworkConnectivityHandler != null)
            {
                m_NetworkConnectivityHandler.OnlineStatusChanged -= ToggleConnectAccountButton;
            }
        }
    }
}
