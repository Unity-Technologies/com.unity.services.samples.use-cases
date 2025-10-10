using UnityEngine;
using UnityEngine.UIElements;
namespace GemHunterUGS.Scripts.Login_and_AccountManagement
{
    /// <summary>
    /// Handles the main menu login functionality.
    /// This class manages the UI elements for login options, including guest play and cloud save loading.
    /// </summary>
    public class MainMenuLoginView : MonoBehaviour
    {
        private UIDocument m_MainMenuUI;
        private VisualElement m_Root;
        private VisualElement m_MainMenuContainer;
        private VisualElement m_LoginButtonsElement;
        
        public Button SignUpInfoButton { get; private set; }
        private VisualElement m_SignUpInfoPopUp;
        public  Button ClosePopUpButton { get; private set; }
        
        public Button ConnectAccountButton { get; private set; }
        public Button GuestPlayButton { get; private set; }

        /// <summary>
        /// Sets up the game login UI by initializing UI elements and registering button click events.
        /// </summary>
        public void InitializeLoginUI()
        {
            m_MainMenuUI = GetComponent<UIDocument>();
            m_Root =  m_MainMenuUI.rootVisualElement;
            m_MainMenuContainer = m_Root.Q<VisualElement>("MainMenuContainer");
            m_LoginButtonsElement = m_MainMenuContainer.Q<VisualElement>("LoginButtonsElement");
            GuestPlayButton = m_LoginButtonsElement.Q<Button>("GuestPlayButton");
            ConnectAccountButton = m_LoginButtonsElement.Q<Button>("ConnectAccountButton");
            
            // Info Pop Up
            SignUpInfoButton = m_MainMenuContainer.Q<Button>("SignUpInfoButton");
            m_SignUpInfoPopUp = m_MainMenuContainer.Q<VisualElement>("SignUpInfoPopUp");
            ClosePopUpButton = m_SignUpInfoPopUp.Q<Button>("ClosePopUpButton");
        }
        
        public void HideMainMenuUI()
        {
            m_MainMenuContainer.style.display = DisplayStyle.None;
        }
        
        /// <summary>
        /// Displays the start login options and hides the sign-in options.
        /// </summary>
        public void ShowMainMenu()
        {
            m_Root.style.display = DisplayStyle.Flex;
            m_MainMenuContainer.style.display = DisplayStyle.Flex;
        }

        public void ShowInfoPopUp()
        {
            m_SignUpInfoPopUp.style.display = DisplayStyle.Flex;
        }

        public void HideInfoPopUp()
        {
            m_SignUpInfoPopUp.style.display = DisplayStyle.None;
        }
    }
}
