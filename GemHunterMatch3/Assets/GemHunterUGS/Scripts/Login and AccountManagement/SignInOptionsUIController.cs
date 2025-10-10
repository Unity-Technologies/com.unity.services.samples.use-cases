using System;
using UnityEngine;
using GemHunterUGS.Scripts.Utilities;
using UnityEngine.Serialization;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;

namespace GemHunterUGS.Scripts.Login_and_AccountManagement
{
    public class SignInOptionsUIController : MonoBehaviour
    {
        [SerializeField]
        private SignInOptionsView m_SignInOptionsView;
        [SerializeField]
        private MainMenuLoginUIController m_MainMenuLoginController;
        
        [SerializeField]
        private UnityPlayerAccountSignIn m_UnityPlayerAccountSignIn;
        [SerializeField]
        private FacebookSignIn m_FacebookSignIn;
        
        #if UNITY_ANDROID
        [SerializeField]
        private GooglePlayGamesSignIn m_GooglePlayGamesSignIn;
        #endif
        
        private void Start()
        {
            m_SignInOptionsView.Initialize();
            m_SignInOptionsView.ButtonClose.clicked += CloseSignInOptionsUI;
            m_SignInOptionsView.ButtonUnityID.clicked += SignInWithUnityID;
            m_SignInOptionsView.ButtonFacebook.clicked += SignInWithFacebook;
            
            #if UNITY_ANDROID
            m_SignInOptionsView.ButtonGoogle.clicked += SignInWithGoogle;
            #endif
        }
        
        private void CloseSignInOptionsUI()
        {
            m_SignInOptionsView.HideSignInOptions();
            m_MainMenuLoginController.OpenMainMenu();
        }

        private void SignInWithUnityID()
        {
            m_UnityPlayerAccountSignIn.StartSignInOrLink();
            m_SignInOptionsView.HideSignInOptions();
        }

        private void SignInWithFacebook()
        {
            m_FacebookSignIn.StartSignInOrLink();
            m_SignInOptionsView.HideSignInOptions();
        }

        #if UNITY_ANDROID
        private void SignInWithGoogle()
        {
            m_GooglePlayGamesSignIn.StartSignInOrLink();
            m_SignInOptionsView.HideSignInOptions();
        }
        #endif  

        public void ShowSocialSignUpOptions()
        {
            m_SignInOptionsView.ShowSignInOptions();
        }
        
        private void OnDisable()
        {
            m_SignInOptionsView.ButtonClose.clicked -= CloseSignInOptionsUI;
            m_SignInOptionsView.ButtonUnityID.clicked -= SignInWithUnityID;
            m_SignInOptionsView.ButtonFacebook.clicked -= SignInWithFacebook;
            
            #if UNITY_ANDROID
            m_SignInOptionsView.ButtonGoogle.clicked -= SignInWithGoogle;
            #endif      
        }
    }
}
