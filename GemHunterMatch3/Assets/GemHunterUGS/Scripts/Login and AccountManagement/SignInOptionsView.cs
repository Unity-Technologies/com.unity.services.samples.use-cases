using UnityEngine;
using UnityEngine.UIElements;
namespace GemHunterUGS.Scripts.Login_and_AccountManagement
{
    /// <summary>
    /// Handles the sign-in options UI and interactions for the Gem Hunter game.
    /// This class manages the display of sign-in options and delegates sign-in actions to specific handlers.
    /// </summary>
    public class SignInOptionsView : MonoBehaviour
    {
        [SerializeField]
        private UIDocument m_SignInOptionsDocument;
        
        [SerializeField]
        private FacebookSignIn m_FacebookSignIn;
        [SerializeField]
        private UnityPlayerAccountSignIn m_UnityPlayerAccountSignIn;

        private VisualElement m_Root;
        private VisualElement m_SignInOptions;

        private Button m_ButtonClose;
        private Button m_ButtonUnityID;
        private Button m_ButtonFacebook;
        private Button m_ButtonGoogle;
        
        public Button ButtonClose => m_ButtonClose;
        public Button ButtonUnityID => m_ButtonUnityID;
        public Button ButtonFacebook => m_ButtonFacebook;
        public Button ButtonGoogle => m_ButtonGoogle;
        
        public void Initialize()
        {
            m_SignInOptionsDocument = GetComponent<UIDocument>();
            m_SignInOptionsDocument.enabled = true;

            m_Root = m_SignInOptionsDocument.rootVisualElement;
            m_SignInOptions = m_Root.Q<VisualElement>("SignInElement");
            var signInBackground = m_SignInOptions.Q<VisualElement>("SignInBackground");
            
            m_ButtonClose = signInBackground.Q<Button>("ButtonClose");
            
            var signUpButtonContainer = signInBackground.Q<VisualElement>("SignUpButtonContainer");
            
            m_ButtonUnityID = signUpButtonContainer.Q<Button>("ButtonUnityID");
            m_ButtonFacebook = signUpButtonContainer.Q<Button>("ButtonFacebookLogin");
            m_ButtonGoogle = signUpButtonContainer.Q<Button>("ButtonGoogleLogin");
            
            #if UNITY_ANDROID
            m_ButtonGoogle = signUpButtonContainer.Q<Button>("ButtonGoogleLogin");
            if (m_ButtonGoogle != null)
            {
                m_ButtonGoogle.style.display = DisplayStyle.Flex;
            }
            #else
            // Hide Google button on non-Android platforms
            var googleButton = signUpButtonContainer.Q<Button>("ButtonGoogleLogin");
            if (googleButton != null)
            {
                googleButton.style.display = DisplayStyle.None;
            }
            #endif
            
            m_Root.style.display = DisplayStyle.None;
        }

        public void ShowSignInOptions()
        {
            m_Root.style.display = DisplayStyle.Flex;
        }

        public void HideSignInOptions()
        {
            m_Root.style.display = DisplayStyle.None;
        }
    }
}
