using UnityEngine;
using UnityEngine.UIElements;
namespace GemHunterUGS.Scripts.Login_and_AccountManagement
{
    public class AccountManagementView : MonoBehaviour
    {
        // Main Elements
        [SerializeField]
        private UIDocument m_Document;
        private VisualElement m_Root;
        private VisualElement m_AccountMenu;
        private VisualElement m_AccountsContainer;

        // Testing Bar
        private VisualElement m_TestTop;
        public Button TestInfoButton { get; private set; }
        public TextField TestingTextField { get; private set; }
        public Button TestSaveButton { get; private set; }
        public Button TestLoadButton { get; private set; }
        public Button ClosePopupButton { get; private set; }
        private VisualElement TestSaveInfoPopUp;

        private VisualElement m_ConfirmationDialogDarken;
        private VisualElement m_ConfirmationDialog;
        private Label m_ConfirmationDialogLabel;
        
        public Button ConfirmDialogButton { get; private set; }
        public Button CancelDialogButton { get; private set; }
        
        // Account Management
        [SerializeField]
        private Sprite m_LinkedStatusBackground;
        [SerializeField]
        private Sprite m_UnlinkedStatusBackground;
        
        public Button CloseAccountMenuButton { get; private set; }
        public Button UnityIDButton { get; private set; }
        public Button FacebookButton { get; private set; }
        public Button GoogleButton { get; private set; }
        
        public Button UnlinkUnityButton { get; private set; }
        public Button UnlinkFacebookButton { get; private set; }
        public Button UnlinkGoogleButton { get; private set; }

        private VisualElement m_LinkGoogleAccountContainer;
        
        private VisualElement m_UnityIDStatus;
        private VisualElement m_FacebookStatus;
        private VisualElement m_GoogleStatus;

        private VisualElement m_UnityIDLinkedCheck;
        private VisualElement m_FacebookLinkedCheck;
        private VisualElement m_GoogleLinkedCheck;
        private VisualElement m_AppleLinkedCheck;

        private VisualElement m_AccountActionContainer;
        public Button DeleteAllAccountsButton { get; private set; }
        public Button AccountActionCancelButton { get; private set; }
        private Label m_PlayerIDLabel;
        
        public void Initialize()
        {
            SetupMainElements();
            SetupAccountTesting();
            SetupAccountLinking();
            SetupConfirmationDialog();
            SetupDeleteAccountElements();
        }

        private void SetupMainElements()
        {
            m_Root = m_Document.rootVisualElement;
            m_AccountMenu = m_Root.Q<VisualElement>("AccountManagementMenu");
            m_AccountsContainer = m_Root.Q<VisualElement>("AccountsContainer");
            CloseAccountMenuButton = m_AccountMenu.Q<Button>("CloseAccountMenuButton");
        }
        
        private void SetupAccountTesting()
        {
            m_TestTop = m_AccountMenu.Q<VisualElement>("TestTop");
            TestInfoButton  = m_TestTop.Q<Button>("InfoButton");
            TestingTextField = m_TestTop.Q<TextField>("TestingTextField");
            TestSaveButton = m_TestTop.Q<Button>("SaveButton");
            TestLoadButton = m_TestTop.Q<Button>("LoadButton");
            TestSaveInfoPopUp = m_AccountMenu.Q<VisualElement>("TestSaveInfoPopUp");
            ClosePopupButton = TestSaveInfoPopUp.Q<Button>("ClosePopUpButton");
        }
        
        private void SetupAccountLinking()
        {
            UnityIDButton = m_AccountsContainer.Q<Button>("UnityIDButton");
            FacebookButton = m_AccountsContainer.Q<Button>("FacebookButton");
            GoogleButton = m_AccountsContainer.Q<Button>("GoogleButton");
            
            m_UnityIDStatus = m_AccountsContainer.Q<VisualElement>("UnityIDStatus");
            m_FacebookStatus = m_AccountsContainer.Q<VisualElement>("FacebookStatus");
            m_GoogleStatus = m_AccountsContainer.Q<VisualElement>("GoogleStatus");
            
            m_UnityIDLinkedCheck = m_UnityIDStatus.Q<VisualElement>("LinkedCheck");
            m_FacebookLinkedCheck = m_FacebookStatus.Q<VisualElement>("LinkedCheck");
            m_GoogleLinkedCheck = m_GoogleStatus.Q<VisualElement>("LinkedCheck");
            // m_AppleLinkedCheck = m_AppleStatus.Q<VisualElement>("LinkedCheck");
            
            UnlinkUnityButton = m_AccountsContainer.Q<Button>("UnlinkUnityIDButton");
            UnlinkFacebookButton = m_AccountsContainer.Q<Button>("UnlinkFacebookButton");
            UnlinkGoogleButton = m_AccountsContainer.Q<Button>("UnlinkGoogleButton");
            
            m_LinkGoogleAccountContainer = m_AccountsContainer.Q<VisualElement>("LinkGoogleAccountContainer");
            
            #if UNITY_ANDROID
            if (m_LinkGoogleAccountContainer != null)
            {
                m_LinkGoogleAccountContainer.style.display = DisplayStyle.Flex;
            }
            #else
            // Hide Google button on non-Android platforms
            if (m_LinkGoogleAccountContainer != null)
            {
                m_LinkGoogleAccountContainer.style.display = DisplayStyle.None;
            }
            #endif
        }

        private void SetupConfirmationDialog()
        {
            m_ConfirmationDialog = m_AccountMenu.Q<VisualElement>("ConfirmationDialog");
            m_ConfirmationDialogDarken = m_AccountMenu.Q<VisualElement>("ConfirmationDialogDarken");
            m_ConfirmationDialogLabel = m_AccountMenu.Q<Label>("ConfirmationDialogLabel");
            ConfirmDialogButton = m_AccountMenu.Q<Button>("ConfirmDialogButton");
            CancelDialogButton = m_AccountMenu.Q<Button>("CancelDialogButton");
        }

        private void SetupDeleteAccountElements()
        {
            m_AccountActionContainer = m_AccountMenu.Q<VisualElement>("AccountActionContainer");
            DeleteAllAccountsButton = m_AccountActionContainer.Q<Button>("DeleteAllAccountsButton");
            AccountActionCancelButton = m_AccountActionContainer.Q<Button>("CancelButton");
            m_PlayerIDLabel = m_AccountActionContainer.Q<Label>("PlayerIDLabel");
        }

        public void UnlinkStatusForAllAccounts()
        {
            UpdateAccountStatusVisuals(m_UnityIDStatus,m_UnityIDLinkedCheck, false);
            UpdateAccountStatusVisuals(m_UnityIDStatus, m_FacebookLinkedCheck, false);
            UpdateAccountStatusVisuals(m_UnityIDStatus,m_GoogleLinkedCheck, false);
            
            UnlinkUnityButton.SetEnabled(false);
            UnlinkFacebookButton.SetEnabled(false);
            UnlinkGoogleButton.SetEnabled(false);
        }
        
        public void UpdateButtonState(Button linkButton, string buttonText, Button unlinkButton, bool isLinked)
        {
            linkButton.SetEnabled(!isLinked);
            unlinkButton.SetEnabled(isLinked);
            linkButton.text = isLinked ? $"{buttonText} Linked" : $"Link {buttonText}";
            unlinkButton.style.unityBackgroundImageTintColor = isLinked ? new StyleColor(Color.white): new StyleColor(Color.grey);
        }
        
        public void SetAccountStatus(LinkType accountType, bool isLinked)
        {
            switch (accountType)
            {
                case LinkType.UnityPlayerAccount:
                    UpdateAccountStatusVisuals(m_UnityIDStatus,m_UnityIDLinkedCheck, isLinked);
                    break;
                case LinkType.Facebook:
                    UpdateAccountStatusVisuals(m_FacebookStatus,m_FacebookLinkedCheck, isLinked);
                    break;
                case LinkType.GooglePlayGames:
                    UpdateAccountStatusVisuals(m_GoogleStatus, m_GoogleLinkedCheck, isLinked);
                    break;
                
                // case LinkType.Apple:
                //     UpdateAccountStatusVisuals(m_AppleStatus,m_AppleLinkedCheck, isLinked);
                //     break;
            }
        }
        
        private void UpdateAccountStatusVisuals(VisualElement statusContainer, VisualElement checkmark, bool isLinked)
        {
            statusContainer.style.backgroundImage = new StyleBackground(isLinked ? m_LinkedStatusBackground : m_UnlinkedStatusBackground);
            
            checkmark.style.display = isLinked ? DisplayStyle.Flex : DisplayStyle.None;
        }
        
        public void SetPlayerID(string playerID)
        {
            m_PlayerIDLabel.text = $"PLAYER ID: {playerID}";
        }
        
        public void ShowTestSaveInfoPopUp()
        {
            TestSaveInfoPopUp.style.display = DisplayStyle.Flex;
        }
        
        public void CloseTestSaveInfoPopUp()
        {
            TestSaveInfoPopUp.style.display = DisplayStyle.None;
        }
        
        public void ShowConfirmationDialog(string message)
        {
            m_ConfirmationDialogLabel.text = message;
            m_ConfirmationDialog.style.display = DisplayStyle.Flex;
            m_ConfirmationDialogDarken.style.display = DisplayStyle.Flex;
        }
        
        public void CloseConfirmationDialog()
        {
            m_ConfirmationDialog.style.display = DisplayStyle.None;
            m_ConfirmationDialogDarken.style.display = DisplayStyle.None;
        }
    }
}
